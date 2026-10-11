using Elib.Catalog.Domain;

namespace Elib.Catalog.Tests.Domain;

public sealed class IsbdTests
{
    private static MarcField Data(string tag, string ind2, params (string Code, string Value)[] subfields) =>
        new(tag, " ", ind2, Subfields: [.. subfields.Select(s => new MarcSubfield(s.Code, s.Value))]);

    [Fact]
    public void Uses_264_publication_keeps_abbreviation_dots_and_numbered_parts()
    {
        var isbd = Isbd.Build(
        [
            Data("245", "0", ("a", "Toán 6."), ("n", "Tập 1"), ("p", "Số học :"), ("b", "sách giáo khoa")),
            Data("264", "0", ("a", "Nơi sản xuất bỏ qua")),
            Data("264", "1", ("a", "H."), ("a", "Tp. Hồ Chí Minh :"), ("b", "Giáo dục,"), ("c", "2021.")),
        ]);
        Assert.Equal(["Toán 6. Tập 1. Số học : sách giáo khoa. — H. ; Tp. Hồ Chí Minh : Giáo dục, 2021."], isbd);
    }

    [Fact]
    public void Without_title_there_is_no_isbd() => Assert.Empty(Isbd.Build([Data("100", " ", ("a", "Nguyễn Du"))]));
}
