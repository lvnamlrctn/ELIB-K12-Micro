using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ELIBAPI.Infrastructure.Services;

public class Z3950SearchService(IServiceScopeFactory scopeFactory, IHttpClientFactory http, Z3950BerClient ber)
    : IZ3950SearchService
{
    /// <summary>
    /// Xác định chiến lược kết nối thực tế cho 1 Z3950Config.
    /// LƯU Ý QUAN TRỌNG: cột "Systax" trên form quản trị (nhãn "SYNTAX", mặc định "USMARC") là
    /// khổ mẫu MARC (USMARC/MARC21/UNIMARC/XML...) mà người quản trị nhập tự do — KHÔNG phải
    /// tên giao thức kết nối. Do đó KHÔNG được dùng giá trị Systax để chọn Internal/Z3950/Z3950SRU;
    /// phải suy ra từ hình dạng cấu hình thực tế: Internal = không có Host, SRU = có Url, còn lại = BER Z3950.
    /// </summary>
    private enum ConnStrategy { Internal, Sru, Ber }

    private static ConnStrategy ResolveStrategy(Z3950Config config)
    {
        if ((config.Systax ?? "").Equals("Internal", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(config.Host))
            return ConnStrategy.Internal;
        if (!string.IsNullOrWhiteSpace(config.Url))
            return ConnStrategy.Sru;
        return ConnStrategy.Ber;
    }

    /// <summary>Tìm Z3950 chỉ trả tối đa ngần này kết quả mỗi thư viện (đếm và phân trang đều cắt ở đây) — port ELIB-LRC 09-29.</summary>
    public const int MaxResults = 1000;

    public async Task<Z3950BriefResult> SearchBriefAsync(Z3950Config config, List<Z3950SearchField> fields, string op)
    {
        var result = await (ResolveStrategy(config) switch
        {
            ConnStrategy.Internal => InternalBriefAsync(config, fields, op),
            ConnStrategy.Sru      => SruBriefAsync(config, fields, op),
            _                     => BerBriefAsync(config, fields, op)
        });
        result.Truncated = result.Count > MaxResults;
        result.Count     = Math.Min(result.Count, MaxResults);
        return result;
    }

    public async Task<Z3950DetailResult> SearchDetailAsync(Z3950Config config, List<Z3950SearchField> fields, string op, int pageIndex, int pageSize)
    {
        var take = CappedPageSize(pageIndex, pageSize);
        if (take == 0)
        {
            // Trang nằm hẳn ngoài 1000 kết quả đầu: chỉ cần tổng số, không kéo bản ghi.
            var brief = await SearchBriefAsync(config, fields, op);
            return new Z3950DetailResult
            {
                LibraryId = brief.LibraryId, LibraryName = brief.LibraryName, Systax = brief.Systax,
                Connected = brief.Connected, Error = brief.Error, TotalCount = brief.Count, Truncated = brief.Truncated,
                PageIndex = pageIndex, PageSize = pageSize, Records = []
            };
        }
        var result = await (ResolveStrategy(config) switch
        {
            ConnStrategy.Internal => InternalDetailAsync(config, fields, op, pageIndex, pageSize),
            ConnStrategy.Sru      => SruDetailAsync(config, fields, op, pageIndex, pageSize),
            _                     => BerDetailAsync(config, fields, op, pageIndex, pageSize)
        });
        // Trang vắt qua mốc 1000: bỏ phần bản ghi vượt giới hạn.
        if (result.Records.Count > take) result.Records = result.Records.Take(take).ToList();
        result.Truncated  = result.TotalCount > MaxResults;
        result.TotalCount = Math.Min(result.TotalCount, MaxResults);
        result.PageSize   = pageSize;
        return result;
    }

    /// <summary>Số bản ghi được lấy ở trang (pageIndex, pageSize) khi chỉ cho xem MaxResults kết quả đầu:
    /// đủ pageSize nếu cả trang nằm trong giới hạn, phần còn lại nếu trang vắt qua mốc, 0 nếu vượt hẳn.</summary>
    public static int CappedPageSize(int pageIndex, int pageSize)
    {
        if (pageIndex < 1 || pageSize < 1) return 0;
        var skip = (long)(pageIndex - 1) * pageSize;
        return skip >= MaxResults ? 0 : (int)Math.Min(pageSize, MaxResults - skip);
    }

    // ── Internal strategy ─────────────────────────────────────────────────────

    private async Task<Z3950BriefResult> InternalBriefAsync(Z3950Config config, List<Z3950SearchField> fields, string op)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db    = scope.ServiceProvider.GetRequiredService<ELIBAPIDbContext>();

            var count = await BuildInternalQuery(db, config, fields, op).CountAsync();
            return new Z3950BriefResult
            {
                LibraryId   = config.PublicId,
                LibraryName = config.Name ?? "",
                Systax      = config.Systax ?? "",
                Connected   = true,
                Count       = count
            };
        }
        catch (Exception ex)
        {
            return new Z3950BriefResult { LibraryId = config.PublicId, LibraryName = config.Name ?? "", Systax = config.Systax ?? "", Connected = false, Error = ex.Message };
        }
    }

    private async Task<Z3950DetailResult> InternalDetailAsync(Z3950Config config, List<Z3950SearchField> fields, string op, int pageIndex, int pageSize)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db    = scope.ServiceProvider.GetRequiredService<ELIBAPIDbContext>();
            var q     = BuildInternalQuery(db, config, fields, op);
            var total = await q.CountAsync();
            var items = await q
                .Skip((Math.Max(pageIndex, 1) - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new Z3950DetailResult
            {
                LibraryId   = config.PublicId,
                LibraryName = config.Name ?? "",
                Systax      = config.Systax ?? "",
                Connected   = true,
                TotalCount  = total,
                PageIndex   = pageIndex,
                PageSize    = pageSize,
                Records     = items.Select(ToRecord).ToList()
            };
        }
        catch (Exception ex)
        {
            return new Z3950DetailResult { LibraryId = config.PublicId, LibraryName = config.Name ?? "", Systax = config.Systax ?? "", Error = ex.Message };
        }
    }

    private static IQueryable<EbookItem> BuildInternalQuery(ELIBAPIDbContext db, Z3950Config config, List<Z3950SearchField> fields, string op)
    {
        var q = db.EbookItems
            .Include(x => x.ItemXml)
            .Where(x => x.IsDelete != 2 && x.Status == 2 && x.TenantId == config.TenantId);

        if (fields.Count == 0) return q;

        if (op.Equals("OR", StringComparison.OrdinalIgnoreCase))
        {
            var predicate = BuildOrPredicate(fields);
            q = q.Where(predicate);
        }
        else
        {
            foreach (var f in fields)
                q = ApplyField(q, f);
        }
        return q;
    }

    private static IQueryable<EbookItem> ApplyField(IQueryable<EbookItem> q, Z3950SearchField f)
    {
        var v = f.Value.ToLower();
        return f.Field.ToUpperInvariant() switch
        {
            "TITLE"     => q.Where(x => x.ItemXml != null && x.ItemXml.Title     != null && x.ItemXml.Title.ToLower().Contains(v)),
            "AUTHOR"    => q.Where(x => x.ItemXml != null && x.ItemXml.Author    != null && x.ItemXml.Author.ToLower().Contains(v)),
            "PUBLISHER" => q.Where(x => x.ItemXml != null && x.ItemXml.Publisher != null && x.ItemXml.Publisher.ToLower().Contains(v)),
            "KEYWORD"   => q.Where(x => x.ItemXml != null && (
                               (x.ItemXml.Title      != null && x.ItemXml.Title.ToLower().Contains(v))      ||
                               (x.ItemXml.Author     != null && x.ItemXml.Author.ToLower().Contains(v))     ||
                               (x.ItemXml.Publisher   != null && x.ItemXml.Publisher.ToLower().Contains(v))  ||
                               (x.ItemXml.Keyword    != null && x.ItemXml.Keyword.ToLower().Contains(v))    ||
                               (x.ItemXml.OtherTitle != null && x.ItemXml.OtherTitle.ToLower().Contains(v))
                           )),
            "SUBJECT"   => q.Where(x => x.ItemXml != null && x.ItemXml.Keyword   != null && x.ItemXml.Keyword.ToLower().Contains(v)),
            "OTHERTITLE"=> q.Where(x => x.ItemXml != null && x.ItemXml.OtherTitle!= null && x.ItemXml.OtherTitle.ToLower().Contains(v)),
            _           => q.Where(x => x.ItemXml != null && (
                               (x.ItemXml.Title     != null && x.ItemXml.Title.ToLower().Contains(v))     ||
                               (x.ItemXml.Author    != null && x.ItemXml.Author.ToLower().Contains(v))    ||
                               (x.ItemXml.Publisher != null && x.ItemXml.Publisher.ToLower().Contains(v)) ||
                               (x.ItemXml.Keyword   != null && x.ItemXml.Keyword.ToLower().Contains(v))
                           ))
        };
    }

    private static Expression<Func<EbookItem, bool>> BuildOrPredicate(List<Z3950SearchField> fields)
    {
        var param = Expression.Parameter(typeof(EbookItem), "x");
        Expression? body = null;
        foreach (var f in fields)
        {
            var v = f.Value.ToLower();
            Expression<Func<EbookItem, bool>> cond = f.Field.ToUpperInvariant() switch
            {
                "TITLE"     => x => x.ItemXml != null && x.ItemXml.Title     != null && x.ItemXml.Title.ToLower().Contains(v),
                "AUTHOR"    => x => x.ItemXml != null && x.ItemXml.Author    != null && x.ItemXml.Author.ToLower().Contains(v),
                "PUBLISHER" => x => x.ItemXml != null && x.ItemXml.Publisher != null && x.ItemXml.Publisher.ToLower().Contains(v),
                "KEYWORD"   => x => x.ItemXml != null && (
                                   (x.ItemXml.Title      != null && x.ItemXml.Title.ToLower().Contains(v))      ||
                                   (x.ItemXml.Author     != null && x.ItemXml.Author.ToLower().Contains(v))     ||
                                   (x.ItemXml.Publisher   != null && x.ItemXml.Publisher.ToLower().Contains(v))  ||
                                   (x.ItemXml.Keyword    != null && x.ItemXml.Keyword.ToLower().Contains(v))    ||
                                   (x.ItemXml.OtherTitle != null && x.ItemXml.OtherTitle.ToLower().Contains(v))
                               ),
                "SUBJECT"   => x => x.ItemXml != null && x.ItemXml.Keyword   != null && x.ItemXml.Keyword.ToLower().Contains(v),
                _           => x => x.ItemXml != null && x.ItemXml.Title != null && x.ItemXml.Title.ToLower().Contains(v),
            };
            var rebodied = new ParameterReplacer(param).Visit(cond.Body);
            body = body == null ? rebodied : Expression.OrElse(body, rebodied);
        }
        body ??= Expression.Constant(false);
        return Expression.Lambda<Func<EbookItem, bool>>(body, param);
    }

    private static Z3950Record ToRecord(EbookItem x) => new()
    {
        PublicId    = x.PublicId,
        Title       = x.ItemXml?.Title,
        Author      = x.ItemXml?.Author,
        Publisher   = x.ItemXml?.Publisher,
        PublishDate = x.ItemXml?.PublishDate,
        Keyword     = x.ItemXml?.Keyword,
        OtherTitle  = x.ItemXml?.OtherTitle
    };

    // ── SRU strategy ─────────────────────────────────────────────────────────

    private async Task<Z3950BriefResult> SruBriefAsync(Z3950Config config, List<Z3950SearchField> fields, string op)
    {
        try
        {
            var (connected, count, error) = await SruSearchCountAsync(config, fields, op);
            return new Z3950BriefResult
            {
                LibraryId   = config.PublicId,
                LibraryName = config.Name ?? "",
                Systax      = config.Systax ?? "",
                Connected   = connected,
                Count       = count,
                Error       = error
            };
        }
        catch (Exception ex)
        {
            return new Z3950BriefResult { LibraryId = config.PublicId, LibraryName = config.Name ?? "", Systax = config.Systax ?? "", Error = ex.Message };
        }
    }

    private async Task<Z3950DetailResult> SruDetailAsync(Z3950Config config, List<Z3950SearchField> fields, string op, int pageIndex, int pageSize)
    {
        try
        {
            var cql       = BuildCql(fields, op);
            var startRec  = (pageIndex - 1) * pageSize + 1;
            var url       = $"{config.Url?.TrimEnd('/')}?operation=searchRetrieve&version=1.1&query={Uri.EscapeDataString(cql)}&startRecord={startRec}&maximumRecords={pageSize}";
            var client    = http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            var xml = await client.GetStringAsync(url);
            var (total, records) = ParseSruXmlDetail(xml);
            return new Z3950DetailResult
            {
                LibraryId   = config.PublicId,
                LibraryName = config.Name ?? "",
                Systax      = config.Systax ?? "",
                Connected   = true,
                TotalCount  = total,
                PageIndex   = pageIndex,
                PageSize    = pageSize,
                Records     = records
            };
        }
        catch (Exception ex)
        {
            return new Z3950DetailResult { LibraryId = config.PublicId, LibraryName = config.Name ?? "", Systax = config.Systax ?? "", Error = ex.Message };
        }
    }

    private async Task<(bool connected, int count, string? error)> SruSearchCountAsync(Z3950Config config, List<Z3950SearchField> fields, string op)
    {
        var cql    = BuildCql(fields, op);
        var url    = $"{config.Url?.TrimEnd('/')}?operation=searchRetrieve&version=1.1&query={Uri.EscapeDataString(cql)}&maximumRecords=1";
        var client = http.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);
        var xml    = await client.GetStringAsync(url);
        return (true, ParseSruCount(xml), null);
    }

    private static string BuildCql(List<Z3950SearchField> fields, string op)
    {
        var parts = fields.Select(f =>
        {
            var index = f.Field.ToUpperInvariant() switch
            {
                "TITLE"     => "dc.title",
                "AUTHOR"    => "dc.creator",
                "PUBLISHER" => "dc.publisher",
                "KEYWORD"   => "cql.anyIndexes",
                "SUBJECT"   => "dc.subject",
                "ISBN"      => "bath.isbn",
                _           => "cql.anyIndexes"
            };
            var escaped = f.Value.Replace("\"", "\\\"");
            return $"{index}=\"{escaped}\"";
        });
        var joiner = op.Equals("OR", StringComparison.OrdinalIgnoreCase) ? " OR " : " AND ";
        return string.Join(joiner, parts);
    }

    private static int ParseSruCount(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            XNamespace srw = "http://www.loc.gov/zing/srw/";
            var el = doc.Descendants(srw + "numberOfRecords").FirstOrDefault()
                  ?? doc.Descendants("numberOfRecords").FirstOrDefault();
            if (el != null && int.TryParse(el.Value.Trim(), out var n)) return n;
        }
        catch { }
        // Fallback: regex
        var m = Regex.Match(xml, @"<[^>]*numberOfRecords[^>]*>(\d+)<");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    private static (int total, List<Z3950Record> records) ParseSruXmlDetail(string xml)
    {
        var total = ParseSruCount(xml);
        var records = new List<Z3950Record>();
        try
        {
            var doc = XDocument.Parse(xml);
            XNamespace srw = "http://www.loc.gov/zing/srw/";
            XNamespace dc  = "http://purl.org/dc/elements/1.1/";
            foreach (var recEl in doc.Descendants(srw + "record"))
            {
                var data = recEl.Descendants(srw + "recordData").FirstOrDefault();
                if (data == null) continue;
                records.Add(new Z3950Record
                {
                    Title       = data.Descendants(dc + "title").FirstOrDefault()?.Value.Trim()
                                  ?? data.Descendants("title").FirstOrDefault()?.Value.Trim(),
                    Author      = data.Descendants(dc + "creator").FirstOrDefault()?.Value.Trim()
                                  ?? data.Descendants("creator").FirstOrDefault()?.Value.Trim(),
                    Publisher   = data.Descendants(dc + "publisher").FirstOrDefault()?.Value.Trim()
                                  ?? data.Descendants("publisher").FirstOrDefault()?.Value.Trim(),
                    PublishDate = data.Descendants(dc + "date").FirstOrDefault()?.Value.Trim()
                                  ?? data.Descendants("date").FirstOrDefault()?.Value.Trim(),
                    Keyword     = data.Descendants(dc + "subject").FirstOrDefault()?.Value.Trim()
                                  ?? data.Descendants("subject").FirstOrDefault()?.Value.Trim(),
                });
            }
        }
        catch { }
        return (total, records);
    }

    // ── Z3950 BER strategy ────────────────────────────────────────────────────

    private async Task<Z3950BriefResult> BerBriefAsync(Z3950Config config, List<Z3950SearchField> fields, string op)
    {
        if (!int.TryParse(config.Port, out var port))
            return new Z3950BriefResult { LibraryId = config.PublicId, LibraryName = config.Name ?? "", Systax = config.Systax ?? "", Error = "Invalid port" };

        var terms = fields.Select(f => (f.Field, f.Value)).ToList();
        var (connected, count, error) = await ber.SearchCountAsync(
            config.Host ?? "", port, config.DatabaseName ?? "Default",
            config.UserName, config.Password, terms, op);

        return new Z3950BriefResult
        {
            LibraryId   = config.PublicId,
            LibraryName = config.Name ?? "",
            Systax      = config.Systax ?? "",
            Connected   = connected,
            Count       = count,
            Error       = error
        };
    }

    private async Task<Z3950DetailResult> BerDetailAsync(Z3950Config config, List<Z3950SearchField> fields, string op, int pageIndex, int pageSize)
    {
        if (!int.TryParse(config.Port, out var port))
            return new Z3950DetailResult { LibraryId = config.PublicId, LibraryName = config.Name ?? "", Systax = config.Systax ?? "", Error = "Invalid port" };

        var terms      = fields.Select(f => (f.Field, f.Value)).ToList();
        var startRec   = (pageIndex - 1) * pageSize + 1;
        var (connected, total, rawRecords, error) = await ber.SearchFetchAsync(
            config.Host ?? "", port, config.DatabaseName ?? "Default",
            config.UserName, config.Password, terms, op, startRec, pageSize);
        // Máy chủ (vd Library of Congress) tạm từ chối Present khi bị gọi dồn dập — mỗi lần chuyển trang là
        // 1 phiên mới — và trả chẩn đoán Bib-1 thay vì bản ghi; gọi lại sau vài giây thì được. Thử lại tối đa
        // 2 lần trước khi báo lỗi (port ELIB-LRC 09-29).
        for (var retry = 1; retry <= 2 && connected && total >= startRec && rawRecords.Count == 0 && error != null; retry++)
        {
            await Task.Delay(TimeSpan.FromSeconds(1.5 * retry));
            (connected, total, rawRecords, error) = await ber.SearchFetchAsync(
                config.Host ?? "", port, config.DatabaseName ?? "Default",
                config.UserName, config.Password, terms, op, startRec, pageSize);
        }

        return new Z3950DetailResult
        {
            LibraryId   = config.PublicId,
            LibraryName = config.Name ?? "",
            Systax      = config.Systax ?? "",
            Connected   = connected,
            TotalCount  = total,
            PageIndex   = pageIndex,
            PageSize    = pageSize,
            Records     = rawRecords.Select(d => new Z3950Record
            {
                Title       = d.GetValueOrDefault("Title"),
                Author      = d.GetValueOrDefault("Author"),
                Publisher   = d.GetValueOrDefault("Publisher"),
                PublishDate = d.GetValueOrDefault("PublishDate"),
                Keyword     = d.GetValueOrDefault("Keyword"),
            }).ToList(),
            Error = error
        };
    }

    // ── Lấy trọn bản ghi MARC (dùng cho "Thêm sách từ Z3950") ─────────────────

    public Task<(bool connected, List<Iso2709Reader.Field> fields, string? error)> FetchFullRecordAsync(
        Z3950Config config, List<Z3950SearchField> fields, string op, int position) =>
        ResolveStrategy(config) switch
        {
            ConnStrategy.Internal => InternalFetchFullAsync(config, fields, op, position),
            ConnStrategy.Sru      => SruFetchFullAsync(config, fields, op, position),
            _                     => BerFetchFullAsync(config, fields, op, position)
        };

    private async Task<(bool, List<Iso2709Reader.Field>, string?)> InternalFetchFullAsync(
        Z3950Config config, List<Z3950SearchField> fields, string op, int position)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db   = scope.ServiceProvider.GetRequiredService<ELIBAPIDbContext>();
            var q    = BuildInternalQuery(db, config, fields, op);
            var item = await q.Skip(position - 1).FirstOrDefaultAsync();
            if (item == null) return (false, [], "Không tìm thấy bản ghi");
            var rec = ToRecord(item);
            return (true, BuildSkeletonFields(rec.Title, rec.Author, rec.Publisher, rec.PublishDate, rec.Keyword), null);
        }
        catch (Exception ex) { return (false, [], ex.Message); }
    }

    private async Task<(bool, List<Iso2709Reader.Field>, string?)> SruFetchFullAsync(
        Z3950Config config, List<Z3950SearchField> fields, string op, int position)
    {
        try
        {
            var cql    = BuildCql(fields, op);
            var url    = $"{config.Url?.TrimEnd('/')}?operation=searchRetrieve&version=1.1&query={Uri.EscapeDataString(cql)}&startRecord={position}&maximumRecords=1&recordSchema=marcxml";
            var client = http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            var xml = await client.GetStringAsync(url);
            var doc = XDocument.Parse(xml);
            XNamespace srw = "http://www.loc.gov/zing/srw/";
            var recordEl = doc.Descendants(srw + "recordData").Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "record");

            if (recordEl != null) return (true, Iso2709Reader.ParseMarcXmlRecord(recordEl), null);

            // Server không hỗ trợ recordSchema=marcxml — dùng lại tóm tắt Dublin Core làm khung MARC tối thiểu
            var (_, records) = ParseSruXmlDetail(xml);
            var rec = records.FirstOrDefault();
            if (rec == null) return (false, [], "Không có dữ liệu bản ghi");
            return (true, BuildSkeletonFields(rec.Title, rec.Author, rec.Publisher, rec.PublishDate, rec.Keyword), null);
        }
        catch (Exception ex) { return (false, [], ex.Message); }
    }

    private async Task<(bool, List<Iso2709Reader.Field>, string?)> BerFetchFullAsync(
        Z3950Config config, List<Z3950SearchField> fields, string op, int position)
    {
        if (!int.TryParse(config.Port, out var port))
            return (false, [], "Invalid port");

        var terms = fields.Select(f => (f.Field, f.Value)).ToList();
        var (connected, _, rawRecords, error) = await ber.SearchFetchRawAsync(
            config.Host ?? "", port, config.DatabaseName ?? "Default",
            config.UserName, config.Password, terms, op, position, 1);
        if (!connected) return (false, [], error);

        if (rawRecords.Count > 0)
        {
            var parsed = Iso2709Reader.Parse(rawRecords[0]);
            var record = parsed.FirstOrDefault();
            if (record != null) return (true, record.Fields, null);
        }

        // Không trích được bytes ISO2709 thô (xem ghi chú trong Z3950BerClient) — dùng lại đường tóm tắt cũ
        var (ok2, _, summary, err2) = await ber.SearchFetchAsync(
            config.Host ?? "", port, config.DatabaseName ?? "Default",
            config.UserName, config.Password, terms, op, position, 1);
        if (!ok2 || summary.Count == 0) return (false, [], err2 ?? "Không có dữ liệu bản ghi");
        var d = summary[0];
        return (true, BuildSkeletonFields(d.GetValueOrDefault("Title"), d.GetValueOrDefault("Author"), d.GetValueOrDefault("Publisher"), d.GetValueOrDefault("PublishDate"), d.GetValueOrDefault("Keyword")), null);
    }

    /// <summary>Dựng khung MARC tối thiểu từ vài trường tóm tắt — dùng khi không lấy được bản ghi MARC gốc.</summary>
    private static List<Iso2709Reader.Field> BuildSkeletonFields(string? title, string? author, string? publisher, string? publishDate, string? keyword)
    {
        var fields = new List<Iso2709Reader.Field>();
        if (!string.IsNullOrWhiteSpace(title))
            fields.Add(new Iso2709Reader.Field { Tag = "245", Ind1 = '1', Ind2 = '0', Subfields = [('a', title)] });
        if (!string.IsNullOrWhiteSpace(author))
            fields.Add(new Iso2709Reader.Field { Tag = "100", Ind1 = '1', Ind2 = ' ', Subfields = [('a', author)] });
        if (!string.IsNullOrWhiteSpace(publisher) || !string.IsNullOrWhiteSpace(publishDate))
        {
            var f264 = new Iso2709Reader.Field { Tag = "264", Ind1 = ' ', Ind2 = '1', Subfields = [] };
            if (!string.IsNullOrWhiteSpace(publisher))   f264.Subfields.Add(('b', publisher));
            if (!string.IsNullOrWhiteSpace(publishDate)) f264.Subfields.Add(('c', publishDate));
            fields.Add(f264);
        }
        if (!string.IsNullOrWhiteSpace(keyword))
            fields.Add(new Iso2709Reader.Field { Tag = "650", Ind1 = ' ', Ind2 = '0', Subfields = [('a', keyword)] });
        return fields;
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static object Fail<T>(Z3950Config config, string error) => new Z3950BriefResult
    {
        LibraryId   = config.PublicId,
        LibraryName = config.Name ?? "",
        Systax      = config.Systax ?? "",
        Connected   = false,
        Error       = error
    };
}

/// <summary>Replaces one ParameterExpression with another in an expression tree.</summary>
internal sealed class ParameterReplacer(ParameterExpression target) : ExpressionVisitor
{
    protected override Expression VisitParameter(ParameterExpression node) =>
        node.Type == target.Type ? target : base.VisitParameter(node);
}
