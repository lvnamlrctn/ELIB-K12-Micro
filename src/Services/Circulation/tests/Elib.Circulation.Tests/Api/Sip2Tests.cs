using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using Elib.BuildingBlocks.Crud;
using Elib.Circulation.Application;
using Elib.Circulation.Application.Sip2;
using Elib.Circulation.Infrastructure;
using Elib.Contracts.Events.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Circulation.Tests.Api;

/// <summary>Bản tin SIP2: checksum trên byte, đọc phần cố định/trường, mã hoá UTF-8 hoặc bỏ dấu, che mật khẩu khi ghi nhật ký.</summary>
public sealed class Sip2ProtocolTests
{
    [Fact]
    public void Checksum_is_the_twos_complement_of_the_byte_sum()
    {
        // Tính độc lập bằng Python: (-sum(b'9300CNkiosk|CO123456|CPgate1|AY1AZ')) & 0xFFFF = 0xF57C.
        Assert.Equal("F57C", Sip2Codec.Checksum("9300CNkiosk|CO123456|CPgate1|AY1AZ"u8));
        Assert.True(Sip2Codec.ChecksumValid("9300CNkiosk|CO123456|CPgate1|AY1AZF57C\r"u8));
        Assert.False(Sip2Codec.ChecksumValid("9300CNkiosk|CO123457|CPgate1|AY1AZF57C"u8));
        Assert.True(Sip2Codec.ChecksumValid("9300CNkiosk|CO123456|"u8)); // không có AZ: không kiểm tra
    }

    [Fact]
    public void Parse_splits_fixed_part_fields_and_sequence()
    {
        var m = Sip2Codec.Parse("11YN20261013    09000020261013    090000AOK|AAHS-01|ABTV0001|ACx|ADmk|AY3AZFFFF\r");
        Assert.Equal(("11", 'Y', '3'), (m.Command, m.FixedChar(0), m.Sequence));
        Assert.Equal(38, m.Fixed.Length);
        Assert.Equal(("HS-01", "TV0001", "mk"), (m.Field(Sip2Tags.PatronId), m.Field(Sip2Tags.ItemId), m.Field(Sip2Tags.PatronPassword)));
        Assert.Equal("11YN20261013    09000020261013    090000AOK|AAHS-01|ABTV0001|ACx|AD***|", Sip2Codec.Mask(m));
    }

    [Fact]
    public void Encode_appends_sequence_and_checksum_and_can_strip_vietnamese_marks()
    {
        var message = new Sip2Message("12", "1YUY20261013    090000") { Sequence = '4' }.Add(Sip2Tags.ScreenMessage, "Mượn thành công | Đã ghi");
        var utf8 = Sip2Codec.Encode(message, asciiOnly: false);
        Assert.True(Sip2Codec.ChecksumValid(utf8));
        Assert.Contains("AFMượn thành công / Đã ghi|AY4AZ", Encoding.UTF8.GetString(utf8), StringComparison.Ordinal);
        var ascii = Sip2Codec.Encode(message, asciiOnly: true);
        Assert.True(Sip2Codec.ChecksumValid(ascii));
        Assert.Contains("AFMuon thanh cong / Da ghi|", Encoding.ASCII.GetString(ascii), StringComparison.Ordinal);
        Assert.Equal((byte)'\r', ascii[^1]);
    }
}

/// <summary>Trạm tự phục vụ qua cổng TCP thật: đăng nhập trạm, thông tin bạn đọc/tài liệu, mượn, gia hạn, trả, mật khẩu bạn đọc, nhật ký.</summary>
public sealed class Sip2Tests(CirculationApiFactory factory) : CirculationTestBase(factory)
{
    private sealed class Kiosk : IDisposable
    {
        private readonly TcpClient _client = new();
        private NetworkStream _stream = null!;
        private char _seq = '0';

        public static async Task<Kiosk> ConnectAsync(int port)
        {
            var kiosk = new Kiosk();
            await kiosk._client.ConnectAsync(IPAddress.Loopback, port);
            kiosk._stream = kiosk._client.GetStream();
            return kiosk;
        }

        /// <summary>Gửi một thông điệp (thêm AY + AZ), nhận một dòng trả lời, kiểm tra checksum của trả lời.</summary>
        public async Task<Sip2Message> SendAsync(string body)
        {
            _seq = _seq == '9' ? '0' : (char)(_seq + 1);
            var text = body + "AY" + _seq + "AZ";
            var bytes = Encoding.UTF8.GetBytes(text);
            byte[] frame = [.. bytes, .. Encoding.ASCII.GetBytes(Sip2Codec.Checksum(bytes) + "\r")];
            await _stream.WriteAsync(frame);
            var reply = new List<byte>();
            var one = new byte[1];
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (await _stream.ReadAsync(one, timeout.Token) == 1 && one[0] != (byte)'\r') reply.Add(one[0]);
            Assert.True(Sip2Codec.ChecksumValid([.. reply]), Encoding.UTF8.GetString([.. reply]));
            var message = Sip2Codec.Parse(Encoding.UTF8.GetString([.. reply]));
            Assert.Equal(_seq, message.Sequence);
            return message;
        }

        public void Dispose() => _client.Dispose();
    }

    private static string Now => "20261013    090000";

    [Fact]
    public async Task Kiosk_logs_in_and_borrows_renews_and_returns_with_the_desk_rules()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var place = await AddPlace(staff, "TU");
        await Read<LoanPolicyDto>(await staff.PostAsJsonAsync(U("/api/loan-policies/Add"), new LoanPolicyRequest(null, null, 10, MaxRenewals: 1, RenewDays: 5), Json));
        var mfn = NextMfn();
        await Publish(Reader(tenantId, "KS-01"), Reader(tenantId, "KS-02", status: 1), Bib(tenantId, mfn, "Đất rừng phương Nam"),
            Item(tenantId, "KS001", mfn), Item(tenantId, "KS002", mfn));

        var login = $"kiosk-{tenantId}";
        var station = await Read<Sip2StationDto>(await staff.PostAsJsonAsync(U("/api/sip2-stations/Add"),
            new Sip2StationRequest("T1", "Trạm tầng 1", login, "Kiosk#2026", place.Id), Json));
        Assert.True(station.HasPassword);
        var otherStaff = Staff(NewTenant());
        var otherPlace = await AddPlace(otherStaff, "TU");
        var other = await otherStaff.PostAsJsonAsync(U("/api/sip2-stations/Add"), new Sip2StationRequest("T1", "Trạm", login.ToUpperInvariant(), "Kiosk#2026", otherPlace.Id), Json);
        Assert.Equal((409, "SIP2_LOGIN_EXISTS"), ((await Error(other)).Status, (await Error(other)).Code)); // tài khoản trạm duy nhất toàn nền tảng
        Assert.DoesNotContain("pbkdf2", await (await staff.GetAsync(U($"/api/sip2-stations/GetById/{station.PublicId}"))).Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var kiosk = await Kiosk.ConnectAsync(await Factory.Services.GetRequiredService<Sip2Listener>().BoundPort);
        Assert.Equal("NNNN", (await kiosk.SendAsync("9900802.00")).Fixed[..4]); // chưa đăng nhập
        Assert.Equal("0", (await kiosk.SendAsync($"9300CN{login}|COsai-mat-khau|")).Fixed);
        Assert.Equal("1", (await kiosk.SendAsync($"9300CN{login}|COKiosk#2026|CPtang1|")).Fixed);
        var status = await kiosk.SendAsync("9900802.00");
        Assert.Equal(("YYYY", "Trạm tầng 1", Sip2Commands.Supported), (status.Fixed[..4], status.Field(Sip2Tags.LibraryName), status.Field(Sip2Tags.SupportedMessages)));

        var patron = await kiosk.SendAsync($"63000{Now}          AOTEST|AAks-01|");
        Assert.Equal(("Bạn đọc KS-01", "Y", ' '), (patron.Field(Sip2Tags.PersonalName), patron.Field(Sip2Tags.ValidPatron), patron.FixedChar(0)));
        Assert.Equal("N", (await kiosk.SendAsync($"63000{Now}          AOTEST|AAKHONG-CO|")).Field(Sip2Tags.ValidPatron));

        var item = await kiosk.SendAsync($"17{Now}AOTEST|ABks001|");
        Assert.Equal(("03", "Đất rừng phương Nam"), (item.Fixed[..2], item.Field(Sip2Tags.TitleId)));

        var checkout = await kiosk.SendAsync($"11NN{Now}{Now}AOTEST|AAKS-01|ABKS001|");
        Assert.Equal('1', checkout.FixedChar(0));
        Assert.Equal(Sip2Clock.Format(Factory.Clock.GetUtcNow().AddDays(10)), checkout.Field(Sip2Tags.DueDate));
        Assert.Equal("04", (await kiosk.SendAsync($"17{Now}AOTEST|ABKS001|")).Fixed[..2]);
        Assert.Equal("KS001", (await kiosk.SendAsync($"63000{Now}          AOTEST|AAKS-01|")).Field(Sip2Tags.ChargedItems));

        var locked = await kiosk.SendAsync($"11NN{Now}{Now}AOTEST|AAKS-02|ABKS002|");
        Assert.Equal('0', locked.FixedChar(0));
        Assert.Contains("khoá", locked.Field(Sip2Tags.ScreenMessage), StringComparison.Ordinal);

        Assert.Equal('1', (await kiosk.SendAsync($"29NN{Now}{Now}AOTEST|AAKS-01|ABKS001|")).FixedChar(0));
        var again = await kiosk.SendAsync($"29NN{Now}{Now}AOTEST|AAKS-01|ABKS001|");
        Assert.Equal('0', again.FixedChar(0)); // chính sách: gia hạn tối đa 1 lần

        var checkin = await kiosk.SendAsync($"09N{Now}{Now}APtang1|AOTEST|ABKS001|");
        Assert.Equal(("1", "KS-01"), (checkin.Fixed[..1], checkin.Field(Sip2Tags.PatronId)));
        Assert.Equal('0', (await kiosk.SendAsync($"09N{Now}{Now}APtang1|AOTEST|ABKS001|")).FixedChar(0)); // không còn lượt mở
        Assert.Equal("N", (await kiosk.SendAsync($"37{Now}0100VNDBV10000|AOTEST|AAKS-01|")).Fixed[..1]);

        // Nhật ký: lượt mượn ghi "Trạm SIP2 …" làm người thao tác; nhật ký SIP2 che mật khẩu trạm.
        Assert.Contains(Factory.PublishedOf<AuditRecorded>(), a => a.TenantId == tenantId && a.ActorName == "Trạm SIP2 T1 — Trạm tầng 1"
            && a.Summary!.Contains("KS001", StringComparison.Ordinal));
        var logs = await Read<CrudPage<Sip2LogDto>>(await staff.PostAsJsonAsync(U("/api/sip2-stations/Logs"), new Sip2LogSearch { PageSize = 100 }, Json));
        Assert.Contains(logs.Items, l => l.Command == "93" && l.Request.Contains("CO***", StringComparison.Ordinal) && !l.Request.Contains("Kiosk#2026", StringComparison.Ordinal));
        Assert.Contains(logs.Items, l => l.Command == "11" && !l.Ok && l.StationCode == "T1");
        Assert.Empty((await Read<CrudPage<Sip2LogDto>>(await Staff(NewTenant()).PostAsJsonAsync(U("/api/sip2-stations/Logs"), new Sip2LogSearch(), Json))).Items);
    }

    [Fact]
    public async Task Station_can_require_the_reader_password_and_wrong_station_logins_close_the_connection()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var place = await AddPlace(staff, "MK");
        var mfn = NextMfn();
        await Publish(Reader(tenantId, "MK-01"), Bib(tenantId, mfn, "Tắt đèn"), Item(tenantId, "MK001", mfn));
        Factory.ReaderPasswords.Passwords[(tenantId, "MK-01")] = "1234abcd";
        var login = $"mk-{tenantId}";
        await Read<Sip2StationDto>(await staff.PostAsJsonAsync(U("/api/sip2-stations/Add"),
            new Sip2StationRequest("MK", "Trạm mật khẩu", login, "Kiosk#2026", place.Id, RequirePatronPassword: true, AsciiOnly: true), Json));

        using var kiosk = await Kiosk.ConnectAsync(await Factory.Services.GetRequiredService<Sip2Listener>().BoundPort);
        Assert.Equal("1", (await kiosk.SendAsync($"9300CN{login}|COKiosk#2026|")).Fixed);
        var noPassword = await kiosk.SendAsync($"11NN{Now}{Now}AOMK|AAMK-01|ABMK001|");
        Assert.Equal(('0', "Nhap mat khau ban doc."), (noPassword.FixedChar(0), noPassword.Field(Sip2Tags.ScreenMessage))); // trạm chỉ hiện ASCII
        var wrong = await kiosk.SendAsync($"63000{Now}          AOMK|AAMK-01|ADsai|");
        Assert.Equal(("N", 'Y'), (wrong.Field(Sip2Tags.ValidPatronPassword), wrong.FixedChar(0)));
        Assert.Null(wrong.Field(Sip2Tags.ChargedItems));
        Assert.Equal('1', (await kiosk.SendAsync($"11NN{Now}{Now}AOMK|AAMK-01|ABMK001|AD1234abcd|")).FixedChar(0));

        using var guesser = await Kiosk.ConnectAsync(await Factory.Services.GetRequiredService<Sip2Listener>().BoundPort);
        for (var i = 0; i < Sip2Session.MaxFailedLogins; i++) Assert.Equal("0", (await guesser.SendAsync($"9300CN{login}|COdoan{i}|")).Fixed);
        await Assert.ThrowsAnyAsync<Exception>(() => guesser.SendAsync($"9300CN{login}|COKiosk#2026|")); // đã đóng kết nối
    }
}
