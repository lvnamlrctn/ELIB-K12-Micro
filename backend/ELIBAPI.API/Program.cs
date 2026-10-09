using System.Text;
using ELIBAPI.API;
using Hangfire;
using ELIBAPI.API.Infrastructure;
using ELIBAPI.Infrastructure.RateLimiting;
using ELIBAPI.API.Middleware;
using ELIBAPI.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
var builder = WebApplication.CreateBuilder(args);
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

// ── Infrastructure (DbContext + Repositories + Services) ──────────────────────
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMemoryCache();

// Đợt 10 — khoá ký HMAC cho cơ chế xem trước/xác nhận tác vụ nền (AdminMutationGuard). Cùng nguồn secret
// với AdminTaskCrypto (AdminTasks:Key, fallback Jwt:Key) nhưng salt khác — 2 khoá dẫn xuất không hoán đổi.
ELIBAPI.Infrastructure.Services.AdminMutationGuard.ConfigureKey(
    builder.Configuration["AdminTasks:Key"] ?? builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Cần cấu hình AdminTasks:Key hoặc Jwt:Key."));

// ── Localization ──────────────────────────────────────────────────────────────
builder.Services.AddLocalization();

// ── Controllers ───────────────────────────────────────────────────────────────
builder.Services.AddControllers(options =>
    {
        options.ModelBinderProviders.Insert(0, new BoolToIntModelBinderProvider());
        // Đợt 18: API công khai khóa đơn vị theo host (xem PublicHostTenantFilter).
        options.Filters.Add<ELIBAPI.API.Filters.PublicHostTenantFilter>();
    })
    .AddJsonOptions(opts =>
    {
        var auditFields = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "IsDelete", "CreatedRowBy", "UpdateRowBy", "CreatedRowDate", "UpdatedRowDate"
        };
        opts.JsonSerializerOptions.TypeInfoResolver =
            new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    typeInfo =>
                    {
                        if (typeInfo.Kind != System.Text.Json.Serialization.Metadata.JsonTypeInfoKind.Object) return;
                        foreach (var prop in typeInfo.Properties)
                            if (auditFields.Contains(prop.Name))
                                prop.ShouldSerialize = (_, _) => false;
                    }
                }
            };
    });

// ── Swagger ───────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ELIBAPI Internal", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Name         = "Authorization",
        Type         = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description  = "Nhập: Bearer {token}"
    });
    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
    c.MapType<Microsoft.AspNetCore.Http.IFormFile>(() =>
        new Microsoft.OpenApi.Models.OpenApiSchema { Type = "string", Format = "binary" });
    c.CustomSchemaIds(t => t.FullName!.Replace("+", "."));
    c.ResolveConflictingActions(apiDescs => apiDescs.First());
});

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer           = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidateAudience         = true,
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.Zero
        };
        opts.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["token"];
                if (!string.IsNullOrEmpty(token))
                    ctx.Token = token;
                return Task.CompletedTask;
            },
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                var loc = ctx.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();
                ctx.Response.StatusCode  = 401;
                ctx.Response.ContentType = "application/json";
                var msg = System.Text.Json.JsonSerializer.Serialize(loc["Unauthorized"].Value);
                return ctx.Response.WriteAsync(
                    $"{{\"success\":false,\"message\":{msg},\"statusCode\":401}}");
            },
            OnForbidden = ctx =>
            {
                var loc = ctx.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();
                ctx.Response.StatusCode  = 403;
                ctx.Response.ContentType = "application/json";
                var msg = System.Text.Json.JsonSerializer.Serialize(loc["Forbidden"].Value);
                return ctx.Response.WriteAsync(
                    $"{{\"success\":false,\"message\":{msg},\"statusCode\":403}}");
            }
        };
    });

builder.Services.AddAuthorization();

// ── Rate limiting (Đợt 22) ──────────────────────────────────────────────────────
builder.Services.AddScoped<HostTenantResolver>();
builder.Services.AddAppRateLimiting();

// ── Health checks (Đợt 22): /healthz (liveness) và /ready (readiness — kiểm tra DB, Elasticsearch, MinIO) ──
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
    .AddCheck<ElasticsearchHealthCheck>("elasticsearch", tags: ["ready"])
    .AddCheck<MinioHealthCheck>("minio", tags: ["ready"]);

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

var locOpts = new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures("en", "vi-VN", "vi")
    .AddSupportedUICultures("en", "vi-VN", "vi");
locOpts.FallBackToParentCultures   = true;
locOpts.FallBackToParentUICultures = true;
locOpts.RequestCultureProviders.Clear();
locOpts.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());
app.UseRequestLocalization(locOpts);

app.UseMiddleware<GlobalExceptionMiddleware>();

if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ELIBAPI Internal v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseStaticFiles();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantContextMiddleware>();
app.UseRateLimiter();
app.MapControllers();
app.MapGet("/health", () => Results.Ok("healthy"));

// Đợt 22 — /healthz (liveness, không kiểm dịch vụ ngoài — chỉ trả lời "process còn sống") và /ready
// (readiness, tag "ready" — DB/Elasticsearch/MinIO; dùng cho k8s/orchestrator quyết định có đưa traffic vào
// hay không). Toàn cục, không theo tenant.
app.MapHealthChecks("/healthz", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (httpContext, report) =>
    {
        httpContext.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString(), description = e.Value.Description }),
        };
        await httpContext.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(payload));
    },
});

// Hangfire dashboard (chỉ nội bộ, chỉ khi Hangfire được đăng ký)
if (app.Services.GetService<Hangfire.JobStorage>() != null)
{
    var hangfirePassword = app.Configuration["Hangfire:Password"];
    var dashOptions = new DashboardOptions();
    if (!string.IsNullOrEmpty(hangfirePassword))
    {
        var hangfireUser = app.Configuration["Hangfire:User"] ?? "admin";
        dashOptions.Authorization = [new HangfireAuthFilter(hangfireUser, hangfirePassword)];
    }
    app.UseHangfireDashboard("/hangfire", dashOptions);

    // Recurring job: xóa vĩnh viễn bản ghi soft-deleted sau 30 ngày, chạy lúc 2:00 AM mỗi ngày
    Hangfire.RecurringJob.AddOrUpdate<ELIBAPI.Infrastructure.Jobs.PurgeDeletedRecordsJob>(
        "purge-deleted-records",
        job => job.RunAsync(),
        Hangfire.Cron.Daily(2));

    // Recurring job: quét cấu hình báo cáo định kỳ đến hạn, gửi email đính kèm Excel, mỗi giờ
    Hangfire.RecurringJob.AddOrUpdate<ELIBAPI.Infrastructure.Jobs.ScheduledReportEmailJob>(
        "scheduled-report-email",
        job => job.RunAsync(),
        Hangfire.Cron.Hourly());

    // Recurring job: huỷ yêu cầu giữ chỗ sách in quá hạn (DueDate đã qua), mỗi 6 giờ
    Hangfire.RecurringJob.AddOrUpdate<ELIBAPI.Infrastructure.Jobs.BookRequestExpiryJob>(
        "book-request-expiry",
        job => job.RunAsync(),
        "0 */6 * * *");

    // Recurring job: tự hết hạn khoản mượn tài liệu số quá hạn + thăng hạng hàng đợi đặt trước, mỗi giờ
    Hangfire.RecurringJob.AddOrUpdate<ELIBAPI.Infrastructure.Jobs.EbookLoanExpiryJob>(
        "ebook-loan-expiry",
        job => job.RunAsync(),
        Hangfire.Cron.Hourly());

    // Recurring job: nhắc nhở sách in/tài liệu số sắp đến hạn hoặc vừa quá hạn, mỗi ngày 7h sáng
    Hangfire.RecurringJob.AddOrUpdate<ELIBAPI.Infrastructure.Jobs.DueSoonReminderJob>(
        "due-soon-reminder",
        job => job.RunAsync(),
        Hangfire.Cron.Daily(7));

    // Recurring job: tự từ chối/no-show/hoàn tất đặt phòng học nhóm quá giờ — 5 phút một lần để phòng không người nhận được
    // mở lại sớm cho bạn đọc khác (port ELIB-LRC 10-04, trước đây mỗi giờ).
    Hangfire.RecurringJob.AddOrUpdate<ELIBAPI.Infrastructure.Jobs.RoomBookingExpiryJob>(
        "room-booking-expiry",
        job => job.RunAsync(),
        "*/5 * * * *");

    // Recurring job: hết hạn giao dịch thanh toán QR (VietQR/VNPAY) còn Pending quá ExpiresAt, mỗi 5 phút (port ELIB-LRC 09-25).
    // Đối soát: VNPAY qua IPN, VietQR qua webhook Sepay (PaymentWebhookController) — không cần job polling.
    Hangfire.RecurringJob.AddOrUpdate<ELIBAPI.Infrastructure.Jobs.PaymentExpiryJob>(
        "payment-expiry",
        job => job.RunAsync(),
        "*/5 * * * *");

    // Recurring job: quét hoạt động đọc/mượn để cấp huy hiệu (Gamification), mỗi ngày 3h sáng
    Hangfire.RecurringJob.AddOrUpdate<ELIBAPI.Infrastructure.Jobs.BadgeEvaluationJob>(
        "badge-evaluation",
        job => job.RunAsync(),
        Hangfire.Cron.Daily(3));

    // Recurring job: kiểm tra tìm kiếm đã lưu có kết quả mới, mỗi 30 phút (Đợt 9)
    Hangfire.RecurringJob.AddOrUpdate<ELIBAPI.Infrastructure.Jobs.SavedSearchAlertJob>(
        "saved-search-alert-check",
        job => job.RunAsync(),
        "*/30 * * * *");

    // Recurring job: dọn SearchObservation quá 90 ngày, mỗi ngày 4h sáng (Đợt 9)
    Hangfire.RecurringJob.AddOrUpdate<ELIBAPI.Infrastructure.Jobs.SearchObservationPurgeJob>(
        "search-observation-purge",
        job => job.RunAsync(),
        Hangfire.Cron.Daily(4));

    // Recurring job: xuất lại TOÀN BỘ biểu ghi đã duyệt của MỌI đơn vị ra thư mục Zebra (Z39.50/SRU), mỗi
    // đêm 3h — lưới an toàn cho job theo-sự-kiện ở CatalogueBookController.Save/DeleteByMfn (Đợt 22.3, port
    // "zebra-export-nightly-rebuild" từ ELIB-LRC).
    Hangfire.RecurringJob.AddOrUpdate<ELIBAPI.Infrastructure.Jobs.ZebraExportJob>(
        "zebra-export-nightly-rebuild",
        job => job.RunBulkAsync(null, null, null, 500),
        Hangfire.Cron.Daily(3));

    // Recurring job: đối soát MinIO với EbookFile (file mồ côi + tổng dung lượng) cho dashboard DevOps, mỗi
    // ngày 4h sáng — có thể quét lâu với bucket lớn nên chạy giờ thấp điểm (Đợt 22.4, port từ ELIB-LRC).
    Hangfire.RecurringJob.AddOrUpdate<ELIBAPI.Infrastructure.Jobs.DigitalStorageAuditJob>(
        "digital-storage-audit",
        job => job.RunAsync(),
        Hangfire.Cron.Daily(4));

    // Recurring job: dọn Payload/Result của AdminTask đã Completed/Cancelled quá hạn lưu, mỗi ngày 5h sáng
    // (Đợt 13) — job luôn đăng ký, tự tôn trọng cờ AdminTasks:Retention:Enabled/DryRun bên trong service.
    Hangfire.RecurringJob.AddOrUpdate<ELIBAPI.Infrastructure.Jobs.AdminTaskRetentionJob>(
        "admin-task-retention-sweep",
        job => job.RunAsync(),
        Hangfire.Cron.Daily(5));
}

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ELIBAPI.Infrastructure.Data.ELIBAPIDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInit");

    try
    {
        if (db.Database.IsSqlServer())
        {
            db.Database.Migrate();
            db.Database.ExecuteSqlRaw(
                "IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'HangFire') EXEC('CREATE SCHEMA [HangFire]')");
        }
        else
        {
            // Di chuyển bảng Review từ "Ebook" (PascalCase) sang "ebook" (lowercase) nếu cần
            db.Database.ExecuteSqlRaw(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'Ebook' AND table_name = 'Review')
                       AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'ebook' AND table_name = 'Review')
                    THEN
                        ALTER TABLE ""Ebook"".""Review"" SET SCHEMA ebook;
                    END IF;
                END $$;
            ");

            // Đợt 2 RBAC (port ELIB-LRC 09-27): Users.PermissionStamp — claim JWT "pstamp" so với cột này để biết claim quyền còn
            // dùng được không. Nullable, không backfill: tự sinh ở lần đăng nhập kế tiếp. Chạy lại nhiều lần không sao.
            db.Database.ExecuteSqlRaw(@"ALTER TABLE public.""Users"" ADD COLUMN IF NOT EXISTS ""PermissionStamp"" varchar(64) NULL;");

            // Số bắt đầu của mẫu đánh số kỳ ấn phẩm (form đăng ký gửi startX/Y/Z) — trước đây không có cột nên bị bỏ qua
            // và kỳ dự kiến luôn đánh số từ 1 (port ELIB-LRC 10-03). Chỉ thêm cột nullable, chạy lại nhiều lần không sao.
            db.Database.ExecuteSqlRaw(@"
                ALTER TABLE printbook.""Serial"" ADD COLUMN IF NOT EXISTS ""StartX"" integer NULL;
                ALTER TABLE printbook.""Serial"" ADD COLUMN IF NOT EXISTS ""StartY"" integer NULL;
                ALTER TABLE printbook.""Serial"" ADD COLUMN IF NOT EXISTS ""StartZ"" integer NULL;
            ");

            // Đặt phòng nâng cao (port ELIB-LRC 10-04 đợt A–D): cột mới của RoomBooking/RoomBookingConfig/Reader.CardUid và các bảng
            // danh sách chặn, thành viên nhóm, giờ mở cửa, ngày đặc biệt, kiểm soát cửa — đều có TenantId. Chỉ thêm, chạy lại an toàn.
            db.Database.ExecuteSqlRaw(ELIBAPI.Infrastructure.Data.RoomBookingSchema.PostgreSql);

            // Thanh toán QR VietQR/VNPAY (port ELIB-LRC 09-25): schema payment + bảng PaymentTransaction có TenantId. Chỉ thêm.
            db.Database.ExecuteSqlRaw(ELIBAPI.Infrastructure.Data.PaymentSchema.PostgreSql);

            // Bảng/cột entity đã khai báo nhưng chưa từng được tạo trên PostgreSQL (smoke test 2026-10-07 báo 500):
            // - ebook."ItemReservation": hàng đợi đặt trước tài liệu số (docs/add-ebook-item-reservation-table.sql, phần PostgreSQL).
            // - public."ReaderDelete"."ProfId": entity có, bảng thiếu → Search/SearchAll bạn đọc đã xóa lỗi.
            // - printbook."SystemInfo"."Id": khóa EF; bảng cũ chỉ có PublicId. Đều chỉ thêm, chạy lại an toàn.
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ebook.""ItemReservation""
                (
                    ""Id""             BIGSERIAL PRIMARY KEY,
                    ""EbookItemId""    BIGINT NOT NULL,
                    ""ReaderId""       BIGINT NOT NULL,
                    ""RequestedAt""    TIMESTAMP NOT NULL,
                    ""Status""         INT NOT NULL,
                    ""ReadyAt""        TIMESTAMP,
                    ""ReadyExpiresAt"" TIMESTAMP,
                    ""FulfilledAt""    TIMESTAMP,
                    ""CancelledAt""    TIMESTAMP,
                    ""IsDelete""       INT,
                    ""CreatedRowBy""   BIGINT,
                    ""UpdateRowBy""    BIGINT,
                    ""CreatedRowDate"" TIMESTAMP,
                    ""UpdatedRowDate"" TIMESTAMP,
                    ""TenantId""       BIGINT,
                    ""PublicId""       UUID NOT NULL DEFAULT gen_random_uuid()
                );
                CREATE INDEX IF NOT EXISTS ""IX_ItemReservation_EbookItemId"" ON ebook.""ItemReservation"" (""EbookItemId"");
                CREATE INDEX IF NOT EXISTS ""IX_ItemReservation_ReaderId""    ON ebook.""ItemReservation"" (""ReaderId"");
                CREATE INDEX IF NOT EXISTS ""IX_ItemReservation_TenantId""    ON ebook.""ItemReservation"" (""TenantId"");

                ALTER TABLE public.""ReaderDelete"" ADD COLUMN IF NOT EXISTS ""ProfId"" bigint NULL;
                ALTER TABLE printbook.""SystemInfo"" ADD COLUMN IF NOT EXISTS ""Id"" integer GENERATED BY DEFAULT AS IDENTITY;
            ");

            // Normalize PublicId to lowercase — only for text/varchar columns, not uuid
            db.Database.ExecuteSqlRaw(@"
                DO $$
                DECLARE r RECORD;
                BEGIN
                    FOR r IN
                        SELECT table_schema, table_name
                        FROM information_schema.columns
                        WHERE column_name = 'PublicId'
                          AND data_type IN ('character varying', 'text')
                          AND table_schema NOT IN ('pg_catalog','information_schema')
                    LOOP
                        EXECUTE format('UPDATE %I.%I SET ""PublicId"" = LOWER(""PublicId"") WHERE ""PublicId"" IS NOT NULL AND ""PublicId"" <> LOWER(""PublicId"")', r.table_schema, r.table_name);
                    END LOOP;
                END $$;
            ");

            // Drop empty PascalCase schemas created by previous GenerateCreateScript
            var emptySchemas = new[] { "Ebook", "EOffice", "Evaluate", "PrintBook" };
            foreach (var schema in emptySchemas)
                db.Database.ExecuteSqlRaw($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");

            logger.LogInformation("PostgreSQL schema initialization complete");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database initialization failed");
        throw;
    }
}

app.Run();

