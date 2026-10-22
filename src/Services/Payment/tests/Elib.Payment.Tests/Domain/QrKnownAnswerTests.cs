using Elib.Payment.Application;

namespace Elib.Payment.Tests.Domain;

/// <summary>
/// Đáp án đã biết: các giá trị dưới đây tính độc lập bằng Python (hmac/hashlib + CRC16 viết lại từ chuẩn) theo đúng bố cục monolith đã chạy thật với app ngân hàng /
/// cổng VNPAY — để test không chỉ chứng minh code tự khớp với chính nó.
/// </summary>
public sealed class QrKnownAnswerTests
{
    [Fact]
    public void Crc16_ccitt_false_matches_the_standard_check_value()
    {
        Assert.Equal((ushort)0x29B1, VietQrPayload.Crc16("123456789"));
    }

    [Fact]
    public void A_vietqr_payload_is_byte_for_byte_the_layout_the_monolith_produced()
    {
        var payload = VietQrPayload.Build("970422", "0123456789", 50_000, "PT261022101500ABCDEF", "Thu vien Truong A");
        Assert.Equal(
            "00020101021238540010A00000072701240006970422011001234567890208QRIBFTTA53037045405500005802VN5917Thu vien Truong A6004ELIB62240820PT261022101500ABCDEF6304D955",
            payload);
    }

    [Fact]
    public void Vietnamese_account_names_are_folded_to_ascii_and_cut_at_25_characters()
    {
        var payload = VietQrPayload.Build("970422", "0123456789", 1_000, "PT1", "Thư viện Trường Đại học Thái Nguyên chi nhánh");
        Assert.Contains("5925Thu vien Truong Dai hoc T", payload, StringComparison.Ordinal);
    }

    [Fact]
    public void Vnpay_order_info_keeps_only_letters_digits_and_spaces()
    {
        Assert.Equal("Sao chup GL001 tr 1 10 2 ban", VnPayUrl.PlainOrderInfo("Sao chụp GL001 (tr. 1–10 × 2 bản)"));
        Assert.Equal("Phieu phat PT001 HS01", VnPayUrl.PlainOrderInfo("Phiếu phạt PT001  HS01"));
    }

    [Fact]
    public void A_vnpay_query_is_signed_exactly_like_the_vnpay_reference_library()
    {
        var parameters = new Dictionary<string, string>
        {
            ["vnp_Amount"] = "1000000", ["vnp_Command"] = "pay", ["vnp_CreateDate"] = "20210801153333", ["vnp_CurrCode"] = "VND", ["vnp_IpAddr"] = "127.0.0.1",
            ["vnp_Locale"] = "vn", ["vnp_OrderInfo"] = "Thanh toan don hang:5", ["vnp_OrderType"] = "other", ["vnp_ReturnUrl"] = "https://domainmerchant.vn/ReturnUrl",
            ["vnp_TmnCode"] = "DEMOV210", ["vnp_TxnRef"] = "5", ["vnp_Version"] = "2.1.0",
        };
        var query = VnPayUrl.Query(parameters);
        Assert.Equal(
            "vnp_Amount=1000000&vnp_Command=pay&vnp_CreateDate=20210801153333&vnp_CurrCode=VND&vnp_IpAddr=127.0.0.1&vnp_Locale=vn&vnp_OrderInfo=Thanh+toan+don+hang%3A5&vnp_OrderType=other&vnp_ReturnUrl=https%3A%2F%2Fdomainmerchant.vn%2FReturnUrl&vnp_TmnCode=DEMOV210&vnp_TxnRef=5&vnp_Version=2.1.0",
            query);
        Assert.Equal(
            "1ee9038d8d9b4801b67ef5faf55ebefa8e642f0f2dbd382109004bbb4a24e7c2f930ff282ba1b77c2029a0a4dedba5a3b274339962179454835b5ee39774b4f6",
            VnPayUrl.Sign(query, "RAOEXHYVSDDIIENYWSLDIIZTANXUXZFJ"));
    }
}
