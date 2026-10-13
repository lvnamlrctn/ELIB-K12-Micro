using System.Globalization;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Circulation.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Circulation.Application.Sip2;

/// <summary>Mật khẩu OPAC bạn đọc nhập ở trạm (trường AD) — identity kiểm tra (cùng luật khoá tạm như đăng nhập OPAC).</summary>
public interface IReaderPasswordChecker
{
    /// <summary>valid | invalid | no-patron | locked-out. Identity không trả lời được → HttpRequestException.</summary>
    Task<string> CheckAsync(long tenantId, string cardNo, string? password, CancellationToken ct);
}

/// <summary>Trạm đã đăng nhập — đơn vị, điểm lưu thông và tuỳ chọn áp cho mọi giao dịch của kết nối.</summary>
public sealed record Sip2StationInfo(long Id, long TenantId, string Code, string Name, long CircPlaceId, bool RequirePatronPassword, bool AsciiOnly);

/// <summary>Trạng thái một kết nối TCP: trạm đã đăng nhập, số lần đăng nhập sai, thông điệp trả lời gần nhất (lệnh 97 gửi lại).</summary>
public sealed class Sip2Session(string? remoteIp)
{
    public const int MaxFailedLogins = 5;

    public string? RemoteIp { get; } = remoteIp;
    public Sip2StationInfo? Station { get; set; }
    public int FailedLogins { get; set; }
    public byte[]? LastResponse { get; set; }
}

/// <summary>
/// Đăng nhập trạm (lệnh 93): tìm tài khoản trên mọi đơn vị (ngữ cảnh hệ thống), so mật khẩu băm, trạm đang bật và đơn vị còn license
/// lưu thông. Sai thì vẫn băm thử để thời gian trả lời như nhau.
/// </summary>
public sealed class Sip2Authenticator(ICrudDbContext db, ITenantContext tenant, IModuleLicenseSource licenses, TimeProvider clock)
{
    private static readonly string DummyHash = Sip2Passwords.Hash(Guid.NewGuid().ToString("N"));

    public async Task<Sip2StationInfo?> LoginAsync(string? loginId, string? password, CancellationToken ct)
    {
        var key = Sip2Station.Key(loginId);
        Sip2Station? station = null;
        if (key.Length > 0)
        {
            using (tenant.UseSystem())
                station = await db.Set<Sip2Station>().IgnoreQueryFilters(["Tenant"]).AsNoTracking().FirstOrDefaultAsync(x => x.LoginKey == key, ct);
        }
        if (!Sip2Passwords.Verify(password, station?.PasswordHash ?? DummyHash) || station is not { Enabled: true }) return null;
        if (!await licenses.IsLicensedAsync(station.TenantId, "CIRCULATION", ct)) return null;

        using (tenant.Use(station.TenantId))
        {
            var now = clock.GetUtcNow();
            if (station.LastSeenAt is not { } seen || now - seen > TimeSpan.FromMinutes(1))
                await db.Set<Sip2Station>().Where(x => x.Id == station.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.LastSeenAt, now), ct);
        }
        return new Sip2StationInfo(station.Id, station.TenantId, station.Code, station.Name, station.CircPlaceId, station.RequirePatronPassword, station.AsciiOnly);
    }
}

/// <summary>
/// Xử lý lệnh SIP2 của trạm đã đăng nhập — chạy trong ngữ cảnh đơn vị của trạm, dùng đúng nghiệp vụ của quầy (chính sách, luật nhóm tài
/// liệu, đặt mượn đang giữ, thẻ khoá/hết hạn/quá hạn). Hỗ trợ: 99, 63, 23, 11, 09, 17, 29, 65, 35, 37 (trả "không nhận tiền").
/// </summary>
public sealed class Sip2Service(LoanResource loans, Replicas replicas, ICrudDbContext db, IReaderPasswordChecker passwords, TimeProvider clock)
{
    private const int MaxListItems = 50;
    private const string WrongPassword = "Mật khẩu không đúng";

    public static Sip2Message Status(Sip2StationInfo? station, Sip2Message request, DateTimeOffset now)
    {
        var online = station is null ? 'N' : 'Y';
        var response = new Sip2Message("98", $"{online}{online}{online}{online}NN030003{Sip2Clock.Format(now)}2.00") { Sequence = request.Sequence };
        response.Add(Sip2Tags.InstitutionId, request.Field(Sip2Tags.InstitutionId) ?? station?.Code ?? "");
        if (station is null) return response.Add(Sip2Tags.ScreenMessage, "Trạm chưa đăng nhập (lệnh 93).");
        return response.Add(Sip2Tags.LibraryName, station.Name).Add(Sip2Tags.SupportedMessages, Sip2Commands.Supported);
    }

    public async Task<Sip2Message> HandleAsync(Sip2StationInfo station, Sip2Message request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(station);
        ArgumentNullException.ThrowIfNull(request);
        var now = clock.GetUtcNow();
        var response = request.Command switch
        {
            Sip2Commands.ScStatus => Status(station, request, now),
            Sip2Commands.PatronInformation => await PatronAsync(station, request, "64", now, ct),
            Sip2Commands.PatronStatus => await PatronAsync(station, request, "24", now, ct),
            Sip2Commands.Checkout => await CheckoutAsync(station, request, now, ct),
            Sip2Commands.Checkin => await CheckinAsync(station, request, now, ct),
            Sip2Commands.ItemInformation => await ItemAsync(station, request, now, ct),
            Sip2Commands.Renew => await RenewAsync(station, request, now, ct),
            Sip2Commands.RenewAll => await RenewAllAsync(station, request, now, ct),
            Sip2Commands.EndPatronSession => Patron(new Sip2Message("36", "Y" + Sip2Clock.Format(now)), station, request)
                .Add(Sip2Tags.ScreenMessage, "Cảm ơn bạn đã sử dụng trạm tự phục vụ."),
            Sip2Commands.FeePaid => Patron(new Sip2Message("38", "N" + Sip2Clock.Format(now)), station, request)
                .Add(Sip2Tags.ScreenMessage, "Trạm không thu tiền — vui lòng nộp tại quầy thủ thư."),
            _ => Status(station, request, now).Add(Sip2Tags.ScreenMessage, $"Lệnh {request.Command} không được hỗ trợ."),
        };
        return response.Sequence == request.Sequence ? response : Copy(response, request.Sequence);
    }

    // ── 63 / 23: thông tin / trạng thái bạn đọc ──

    private async Task<Sip2Message> PatronAsync(Sip2StationInfo station, Sip2Message request, string command, DateTimeOffset now, CancellationToken ct)
    {
        var card = (request.Field(Sip2Tags.PatronId) ?? "").Trim();
        var fixedTail = "000" + Sip2Clock.Format(now) + (command == "64" ? new string('0', 24) : "");
        var password = await PasswordAsync(station, card, request.Field(Sip2Tags.PatronPassword), ct);
        if (password.Denied is { } denied)
            return Patron(new Sip2Message(command, "YY Y          " + fixedTail), station, request)
                .Add(Sip2Tags.PersonalName, "").Add(Sip2Tags.ValidPatron, "Y").Add(Sip2Tags.ValidPatronPassword, "N").Add(Sip2Tags.ScreenMessage, denied);

        ReaderPanel panel;
        try
        {
            panel = await loans.ReaderPanelAsync(new ReaderPanelRequest(card, station.CircPlaceId), ct);
        }
        catch (BusinessRuleException ex)
        {
            return Patron(new Sip2Message(command, "YYYY          " + fixedTail), station, request)
                .Add(Sip2Tags.PersonalName, "").Add(Sip2Tags.ValidPatron, "N").Add(Sip2Tags.ScreenMessage, ex.Message);
        }

        var overdue = panel.CurrentLoans.Where(l => l.DueAt < now).ToList();
        var holds = panel.Holds ?? [];
        var ready = holds.Where(h => h.Status == HoldStatus.Ready).ToList();
        var flags = new string(' ', 14).ToCharArray();
        if (!panel.CanBorrow) flags[0] = 'Y';
        if (panel.IsLocked || panel.IsExpired) flags[1] = flags[3] = 'Y';
        if (panel.MaxLoans is { } max && panel.CurrentLoans.Count >= max) flags[5] = 'Y';
        if (overdue.Count > 0) flags[6] = 'Y';
        var fixedPart = new string(flags) + "000" + Sip2Clock.Format(now);
        if (command == "64")
            fixedPart += string.Concat(new[] { ready.Count, overdue.Count, panel.CurrentLoans.Count, panel.UnpaidFines > 0 ? 1 : 0, 0, holds.Count - ready.Count }
                .Select(n => Math.Min(n, 9999).ToString("D4", CultureInfo.InvariantCulture)));

        var response = Patron(new Sip2Message(command, fixedPart), station, request)
            .Add(Sip2Tags.PersonalName, panel.FullName).Add(Sip2Tags.ValidPatron, "Y");
        if (password.Checked) response.Add(Sip2Tags.ValidPatronPassword, "Y");
        if (panel.UnpaidFines > 0)
            response.Add(Sip2Tags.CurrencyType, "VND").Add(Sip2Tags.FeeAmount, panel.UnpaidFines.ToString("F0", CultureInfo.InvariantCulture));
        if (command == "64")
        {
            foreach (var h in ready.Take(MaxListItems)) response.Add(Sip2Tags.HoldItems, h.Barcode ?? h.Title ?? h.Mfn.ToString(CultureInfo.InvariantCulture));
            foreach (var l in overdue.Take(MaxListItems)) response.Add(Sip2Tags.OverdueItems, l.Barcode);
            foreach (var l in panel.CurrentLoans.Take(MaxListItems)) response.Add(Sip2Tags.ChargedItems, l.Barcode);
            if (panel.UnpaidFines > 0) response.Add(Sip2Tags.FineItems, $"Còn nợ phạt: {Money(panel.UnpaidFines)}");
        }
        if (panel.BlockReason is { } block) response.Add(Sip2Tags.ScreenMessage, block);
        else if (ready.Count > 0) response.Add(Sip2Tags.ScreenMessage, $"Có {ready.Count} tài liệu đặt mượn đang chờ nhận.");
        return response;
    }

    // ── 11: mượn ──

    private async Task<Sip2Message> CheckoutAsync(Sip2StationInfo station, Sip2Message request, DateTimeOffset now, CancellationToken ct)
    {
        var card = (request.Field(Sip2Tags.PatronId) ?? "").Trim();
        var barcode = (request.Field(Sip2Tags.ItemId) ?? "").Trim();
        Sip2Message Result(bool ok, string message, LoanDto? loan = null)
        {
            var r = Patron(new Sip2Message("12", $"{(ok ? '1' : '0')}{(ok ? 'Y' : 'N')}U{(ok ? 'Y' : 'N')}{Sip2Clock.Format(now)}"), station, request)
                .Add(Sip2Tags.ItemId, barcode).Add(Sip2Tags.TitleId, loan?.Title ?? "");
            if (loan is not null) r.Add(Sip2Tags.DueDate, Sip2Clock.Format(loan.DueAt));
            return r.Add(Sip2Tags.ScreenMessage, message);
        }

        if ((await PasswordAsync(station, card, request.Field(Sip2Tags.PatronPassword), ct)).Denied is { } denied) return Result(false, denied);
        if (barcode.Length == 0) return Result(false, "Chưa quét mã tài liệu.");

        // Bạn đọc đang mượn chính bản này và trạm cho phép (SC renewal policy = Y) → gia hạn.
        if (request.FixedChar(0) == 'Y' && await OpenLoanAsync(barcode, ct) is { } current && PatronReplica.Key(current.CardNo) == PatronReplica.Key(card))
        {
            var (renewed, error) = await TryRenewAsync(station, current, ct);
            return renewed is not null ? Result(true, $"Gia hạn thành công, hạn trả {Date(renewed.DueAt)}.", renewed) : Result(false, error!);
        }

        try
        {
            var line = (await loans.CheckoutAsync(new CheckoutRequest(card, [barcode], station.CircPlaceId), ct)).Lines[0];
            return Result(line.Success, line.Message, line.Loan);
        }
        catch (BusinessRuleException ex)
        {
            return Result(false, ex.Message);
        }
    }

    // ── 09: trả ──

    private async Task<Sip2Message> CheckinAsync(Sip2StationInfo station, Sip2Message request, DateTimeOffset now, CancellationToken ct)
    {
        var barcode = (request.Field(Sip2Tags.ItemId) ?? "").Trim();
        Sip2Message Result(bool ok, bool alert, string message, LoanDto? loan = null, string? location = null)
        {
            var r = new Sip2Message("10", $"{(ok ? '1' : '0')}YU{(alert ? 'Y' : 'N')}{Sip2Clock.Format(now)}");
            r.Add(Sip2Tags.InstitutionId, request.Field(Sip2Tags.InstitutionId) ?? station.Code).Add(Sip2Tags.ItemId, barcode)
                .Add(Sip2Tags.PermanentLocation, location ?? "");
            if (loan is not null) r.Add(Sip2Tags.TitleId, loan.Title ?? "").Add(Sip2Tags.PatronId, loan.CardNo);
            return r.Add(Sip2Tags.ScreenMessage, message);
        }

        if (barcode.Length == 0) return Result(false, true, "Chưa quét mã tài liệu.");
        try
        {
            var result = await loans.ReturnAsync(new ReturnRequest(barcode, null, station.CircPlaceId), ct);
            var message = "Trả tài liệu thành công." + (result.OverdueDays > 0 ? $" Quá hạn {result.OverdueDays} ngày." : "")
                + (result.HoldFor is { } hold ? $" Giữ cho bạn đọc đặt mượn {hold.CardNo} — không xếp lên giá." : "");
            var response = Result(true, result.HoldFor is not null, message, result.Loan, result.Loan.StoreName);
            if (result.HoldFor is not null) response.Add("CV", "02"); // cảnh báo: giữ cho đặt mượn
            return response;
        }
        catch (BusinessRuleException ex)
        {
            return Result(false, true, ex.Message);
        }
    }

    // ── 17: thông tin tài liệu ──

    private async Task<Sip2Message> ItemAsync(Sip2StationInfo station, Sip2Message request, DateTimeOffset now, CancellationToken ct)
    {
        var barcode = (request.Field(Sip2Tags.ItemId) ?? "").Trim();
        var item = barcode.Length == 0 ? null : await replicas.ItemAsync(barcode, ct);
        if (item is null)
            return new Sip2Message("18", "010001" + Sip2Clock.Format(now)).Add(Sip2Tags.ItemId, barcode).Add(Sip2Tags.TitleId, "")
                .Add(Sip2Tags.ScreenMessage, "Không tìm thấy mã tài liệu.");
        var loan = await db.Set<Loan>().AsNoTracking().FirstOrDefaultAsync(l => l.ItemPublicId == item.ItemPublicId && l.ReturnedAt == null, ct);
        var held = await db.Set<Hold>().AsNoTracking().AnyAsync(h => h.ItemPublicId == item.ItemPublicId && h.Status == HoldStatus.Ready, ct);
        var waiting = await db.Set<Hold>().AsNoTracking().CountAsync(h => h.Mfn == item.Mfn && h.Status == HoldStatus.Waiting, ct);
        var status = loan is not null ? "04" : held ? "08" : item.Status switch
        {
            ItemReplica.Available => "03",
            "I" => "06",
            Loan.LostStatus => "12",
            _ => "01",
        };
        try
        {
            await replicas.EnsureBibsAsync([item.Mfn], ct);
        }
        catch (HttpRequestException)
        {
            // catalog đang down — trả không có nhan đề
        }
        var title = await db.Set<BibSnapshot>().AsNoTracking().Where(b => b.Mfn == item.Mfn).Select(b => b.Title).FirstOrDefaultAsync(ct);
        var response = new Sip2Message("18", status + "0001" + Sip2Clock.Format(now))
            .Add(Sip2Tags.InstitutionId, request.Field(Sip2Tags.InstitutionId) ?? station.Code)
            .Add(Sip2Tags.HoldQueueLength, waiting.ToString(CultureInfo.InvariantCulture))
            .Add(Sip2Tags.ItemId, item.Barcode).Add(Sip2Tags.TitleId, title ?? "").Add(Sip2Tags.PermanentLocation, item.StoreName ?? "");
        if (loan is not null) response.Add(Sip2Tags.DueDate, Sip2Clock.Format(loan.DueAt));
        return response;
    }

    // ── 29 / 65: gia hạn ──

    private async Task<Sip2Message> RenewAsync(Sip2StationInfo station, Sip2Message request, DateTimeOffset now, CancellationToken ct)
    {
        var card = (request.Field(Sip2Tags.PatronId) ?? "").Trim();
        var barcode = (request.Field(Sip2Tags.ItemId) ?? "").Trim();
        Sip2Message Result(bool ok, string message, LoanDto? loan = null)
        {
            var r = Patron(new Sip2Message("30", $"{(ok ? '1' : '0')}{(ok ? 'Y' : 'N')}UN{Sip2Clock.Format(now)}"), station, request)
                .Add(Sip2Tags.ItemId, barcode).Add(Sip2Tags.TitleId, loan?.Title ?? "");
            if (loan is not null) r.Add(Sip2Tags.DueDate, Sip2Clock.Format(loan.DueAt));
            return r.Add(Sip2Tags.ScreenMessage, message);
        }

        if ((await PasswordAsync(station, card, request.Field(Sip2Tags.PatronPassword), ct)).Denied is { } denied) return Result(false, denied);
        var loan = barcode.Length == 0 ? null : await OpenLoanAsync(barcode, ct);
        if (loan is null || PatronReplica.Key(loan.CardNo) != PatronReplica.Key(card))
            return Result(false, "Bạn đọc không mượn tài liệu này.");
        var (renewed, error) = await TryRenewAsync(station, loan, ct);
        return renewed is not null ? Result(true, $"Gia hạn thành công, hạn trả {Date(renewed.DueAt)}.", renewed) : Result(false, error!);
    }

    private async Task<Sip2Message> RenewAllAsync(Sip2StationInfo station, Sip2Message request, DateTimeOffset now, CancellationToken ct)
    {
        var card = (request.Field(Sip2Tags.PatronId) ?? "").Trim();
        Sip2Message Result(bool ok, IReadOnlyList<string> done, IReadOnlyList<string> failed, string message)
        {
            var r = Patron(new Sip2Message("66", $"{(ok ? '1' : '0')}{Math.Min(done.Count, 9999):D4}{Math.Min(failed.Count, 9999):D4}{Sip2Clock.Format(now)}"), station, request);
            foreach (var b in done.Take(MaxListItems)) r.Add(Sip2Tags.RenewedItems, b);
            foreach (var b in failed.Take(MaxListItems)) r.Add(Sip2Tags.UnrenewedItems, b);
            return r.Add(Sip2Tags.ScreenMessage, message);
        }

        if ((await PasswordAsync(station, card, request.Field(Sip2Tags.PatronPassword), ct)).Denied is { } denied) return Result(false, [], [], denied);
        var reader = await replicas.ReaderAsync(card, ct);
        if (reader is null) return Result(false, [], [], "Không tìm thấy bạn đọc.");
        var open = await db.Set<Loan>().AsNoTracking().Where(l => l.ReaderPublicId == reader.ReaderPublicId && l.ReturnedAt == null).OrderBy(l => l.DueAt).ToListAsync(ct);
        List<string> renewed = [], failed = [];
        foreach (var loan in open)
            ((await TryRenewAsync(station, loan, ct)).Loan is not null ? renewed : failed).Add(loan.Barcode);
        return Result(true, renewed, failed, $"Gia hạn được {renewed.Count}/{open.Count} tài liệu.");
    }

    private async Task<(LoanDto? Loan, string? Error)> TryRenewAsync(Sip2StationInfo station, Loan loan, CancellationToken ct)
    {
        try
        {
            return (await loans.RenewAsync(new RenewRequest(loan.PublicId, $"Gia hạn tại trạm tự phục vụ {station.Code}"), ct), null);
        }
        catch (BusinessRuleException ex)
        {
            db.Set<Loan>().Local.Where(l => l.PublicId == loan.PublicId).ToList().ForEach(l => db.Set<Loan>().Entry(l).State = EntityState.Detached);
            return (null, ex.Message);
        }
    }

    private async Task<Loan?> OpenLoanAsync(string barcode, CancellationToken ct)
    {
        var key = ItemReplica.Key(barcode);
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToUpper() sang upper() của SQL
        return await db.Set<Loan>().AsNoTracking().FirstOrDefaultAsync(l => l.ReturnedAt == null && l.Barcode.ToUpper() == key, ct);
#pragma warning restore CA1862, CA1304, CA1311
    }

    /// <summary>
    /// Mật khẩu bạn đọc (AD). Trạm bắt buộc mật khẩu: sai / thiếu / tài khoản bị khoá tạm → từ chối. Không bắt buộc → không
    /// kiểm tra (không gửi BM). Bạn đọc chưa có tài khoản OPAC cũng bị từ chối khi trạm bắt buộc mật khẩu.
    /// </summary>
    private async Task<(string? Denied, bool Checked)> PasswordAsync(Sip2StationInfo station, string card, string? password, CancellationToken ct)
    {
        if (!station.RequirePatronPassword) return (null, false);
        if (string.IsNullOrEmpty(password)) return ("Nhập mật khẩu bạn đọc.", false);
        string result;
        try
        {
            result = await passwords.CheckAsync(station.TenantId, card, password, ct);
        }
        catch (HttpRequestException)
        {
            return ("Chưa kiểm tra được mật khẩu — thử lại sau ít phút hoặc đến quầy thủ thư.", false);
        }
        return result switch
        {
            "valid" => (null, true),
            "no-patron" => ("Không tìm thấy bạn đọc hoặc bạn đọc chưa có mật khẩu OPAC.", false),
            "locked-out" => ("Nhập sai mật khẩu nhiều lần — tài khoản tạm khoá, thử lại sau.", false),
            _ => (WrongPassword, false),
        };
    }

    private static Sip2Message Patron(Sip2Message response, Sip2StationInfo station, Sip2Message request) =>
        response.Add(Sip2Tags.InstitutionId, request.Field(Sip2Tags.InstitutionId) ?? station.Code).Add(Sip2Tags.PatronId, request.Field(Sip2Tags.PatronId) ?? "");

    private static Sip2Message Copy(Sip2Message response, char? sequence)
    {
        var copy = new Sip2Message(response.Command, response.Fixed) { Sequence = sequence };
        copy.Fields.AddRange(response.Fields);
        return copy;
    }

    private static string Date(DateTimeOffset at) => Loan.LocalDate(at).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Money(decimal amount) => amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + "đ";
}
