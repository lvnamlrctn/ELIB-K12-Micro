using System.Globalization;
using System.Text;
using Elib.BuildingBlocks.Domain;
using Elib.Catalog.Domain;

namespace Elib.Catalog.Tests.Domain;

public sealed class MarcFormatsTests
{
    private static MarcField Data(string tag, string ind1, string ind2, params (string Code, string Value)[] subfields) =>
        new(tag, ind1, ind2, Subfields: [.. subfields.Select(s => new MarcSubfield(s.Code, s.Value))]);

    private static readonly MarcFileRecord Sample = new("00000nam a2200000 a 4500",
    [
        new MarcField("001", Value: "15"),
        new MarcField("008", Value: "240101s2019    vm            000 0 vie d"),
        Data("020", " ", " ", ("a", "978-604-2-12345-6")),
        Data("245", "1", "0", ("a", "Truyện Kiều /"), ("c", "Nguyễn Du")),
        Data("650", " ", "7", ("a", "Văn học Việt Nam")),
        Data("650", " ", "7", ("a", "Thơ lục bát")),
    ]);

    private static void AssertSameFields(IReadOnlyList<MarcField> expected, IReadOnlyList<MarcField> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Tag, actual[i].Tag);
            Assert.Equal(expected[i].Value, actual[i].Value);
            if (expected[i].IsControl) continue;
            Assert.Equal((expected[i].Ind1, expected[i].Ind2), (actual[i].Ind1, actual[i].Ind2));
            Assert.Equal(expected[i].Subfields!, actual[i].Subfields!);
        }
    }

    [Fact]
    public void Iso2709_round_trip_keeps_vietnamese_repeated_fields_and_patches_leader()
    {
        var bytes = MarcFormats.WriteIso2709([Sample, Sample with { Fields = [Data("245", "0", "0", ("a", "Số đỏ"))] }]);

        Assert.Equal(MarcFileFormat.Iso2709, MarcFormats.Detect(bytes, "x.dat"));
        var records = MarcFormats.ReadIso2709(bytes);
        Assert.Equal(2, records.Count);
        AssertSameFields(Sample.Fields, records[0].Fields);
        Assert.Equal("Số đỏ", records[1].Fields[0].Subfields![0].Value);

        var leader = records[0].Leader;
        Assert.Equal('a', leader[9]); // UTF-8
        var length = int.Parse(leader[..5], System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(0x1D, bytes[length - 1]); // độ dài bản ghi tính theo byte
        Assert.Equal("4500", leader[20..]);
    }

    [Fact]
    public void Iso2709_without_unicode_flag_is_read_as_utf8_when_valid_and_latin1_otherwise()
    {
        var utf8 = MarcFormats.WriteIso2709([Sample]);
        utf8[9] = (byte)' '; // phần mềm trong nước hay để trống Leader/09 dù ghi UTF-8
        Assert.Equal("Truyện Kiều /", MarcFormats.ReadIso2709(utf8)[0].Fields[3].Subfields![0].Value);

        var latin = Iso("00000nam  2200000   4500", [("245", "10\u001Fa"u8.ToArray().Concat(new byte[] { 0x43, 0x61, 0x66, 0xE9 }).ToArray())]);
        Assert.Equal("Café", MarcFormats.ReadIso2709(latin)[0].Fields[0].Subfields![0].Value);
    }

    [Fact]
    public void Iso2709_with_directory_counted_in_characters_falls_back_to_field_terminators()
    {
        // Phần mềm cũ tính độ dài trường theo số ký tự thay vì số byte → offset lệch khi có dấu tiếng Việt.
        var fields = new[] { ("245", "10\u001FaTruyện Kiều"), ("650", " 7\u001FaVăn học") };
        var directory = new StringBuilder();
        var body = new StringBuilder();
        foreach (var (tag, value) in fields)
        {
            directory.Append(CultureInfo.InvariantCulture, $"{tag}{value.Length + 1:D4}{body.Length:D5}");
            body.Append(value).Append('\u001E');
        }
        var baseAddress = 24 + directory.Length + 1;
        var text = string.Create(CultureInfo.InvariantCulture, $"00000nam a22{baseAddress:D5}   4500{directory}\u001E{body}\u001D");
        var record = Assert.Single(MarcFormats.ReadIso2709(Encoding.UTF8.GetBytes(text)));

        Assert.Null(record.Error);
        Assert.Equal(["Truyện Kiều", "Văn học"], record.Fields.Select(f => f.Subfields![0].Value));
        Assert.Equal(" ", record.Fields[1].Ind1);
    }

    [Fact]
    public void Iso2709_skips_bom_and_line_breaks_between_records_and_reports_broken_leader()
    {
        var one = MarcFormats.WriteIso2709([Sample]);
        var data = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(one).Concat("\r\n"u8.ToArray()).Concat(one)
            .Concat("00000nam a2299999   4500\u001D"u8.ToArray()).ToArray();

        var records = MarcFormats.Read(data, "Marc_20240101.txt");
        Assert.Equal(3, records.Count);
        Assert.Null(records[1].Error);
        Assert.NotNull(records[2].Error);
    }

    [Fact]
    public void Marcxml_round_trip_and_records_without_namespace()
    {
        using var output = new MemoryStream();
        MarcFormats.WriteMarcXml(output, [Sample with { Fields = [.. Sample.Fields, Data("520", " ", " ", ("a", "Có ký tự lạ \u0001 ở đây"))] }]);
        var xml = output.ToArray();

        Assert.Equal(MarcFileFormat.MarcXml, MarcFormats.Detect(xml, null));
        var record = Assert.Single(MarcFormats.ReadMarcXml(xml));
        Assert.Equal(Sample.Leader, record.Leader);
        AssertSameFields(Sample.Fields, record.Fields.Take(Sample.Fields.Count).ToList());
        Assert.Equal("Có ký tự lạ  ở đây", record.Fields[^1].Subfields![0].Value);

        var plain = """
            <?xml version="1.0"?>
            <record><leader>00000cam a2200000 i 4500</leader>
              <datafield tag="245" ind1="1" ind2="#"><subfield code="a">Dế mèn phiêu lưu ký</subfield></datafield>
            </record>
            """;
        var single = Assert.Single(MarcFormats.Read(Encoding.UTF8.GetBytes(plain), "a.xml"));
        Assert.Equal("00000cam a2200000 i 4500", single.Leader);
        Assert.Equal(("1", " ", "Dế mèn phiêu lưu ký"), (single.Fields[0].Ind1, single.Fields[0].Ind2, single.Fields[0].Subfields![0].Value));
    }

    [Fact]
    public void Marcxml_with_dtd_is_rejected()
    {
        var xml = """<?xml version="1.0"?><!DOCTYPE r [<!ENTITY x "y">]><record><leader>&x;</leader></record>""";
        var ex = Assert.Throws<BusinessRuleException>(() => MarcFormats.Read(Encoding.UTF8.GetBytes(xml), "a.xml"));
        Assert.Equal("MARC_FILE_INVALID", ex.Code);
    }

    [Fact]
    public void Legacy_three_line_text_is_read_and_local_fields_without_subfields_are_dropped()
    {
        var text = "﻿Ldr\r\n\r\n00000nam a2200000 a 4500\r\n001\r\n\r\n123\r\n245\r\n10\r\n$aTắt đèn /$cNgô Tất Tố\r\n900\r\n\r\nTHUVIEN1\r\n650\r\n 7\r\n$aVăn học\r\n";
        var data = Encoding.UTF8.GetBytes(text);

        Assert.Equal(MarcFileFormat.Text, MarcFormats.Detect(data, "rc_20260923151948.txt"));
        var record = Assert.Single(MarcFormats.Read(data, "rc.txt"));
        Assert.Equal(["001", "245", "650"], record.Fields.Select(f => f.Tag));
        Assert.Equal([new MarcSubfield("a", "Tắt đèn /"), new MarcSubfield("c", "Ngô Tất Tố")], record.Fields[1].Subfields!);
        Assert.Equal((" ", "7"), (record.Fields[2].Ind1, record.Fields[2].Ind2));
    }

    private static byte[] Iso(string leader, (string Tag, byte[] Value)[] fields)
    {
        var directory = new StringBuilder();
        using var body = new MemoryStream();
        foreach (var (tag, value) in fields)
        {
            directory.Append(CultureInfo.InvariantCulture, $"{tag}{value.Length + 1:D4}{body.Length:D5}");
            body.Write(value);
            body.WriteByte(0x1E);
        }
        var baseAddress = 24 + directory.Length + 1;
        var head = leader[..12] + baseAddress.ToString("D5", System.Globalization.CultureInfo.InvariantCulture) + leader[17..] + directory + "\u001E";
        return [.. Encoding.ASCII.GetBytes(head), .. body.ToArray(), 0x1D];
    }
}
