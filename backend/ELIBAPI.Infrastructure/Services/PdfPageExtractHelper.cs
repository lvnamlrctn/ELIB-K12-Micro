using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SkiaSharp;

namespace ELIBAPI.Infrastructure.Services;

public static class PdfPageExtractHelper
{
    /// <summary>Trích 1 trang PDF thành file PDF mới. Trả về null nếu pageNumber ngoài phạm vi.</summary>
    public static byte[]? ExtractPage(Stream pdfStream, int pageNumber)
    {
        var inputDocument = PdfReader.Open(pdfStream, PdfDocumentOpenMode.Import);
        if (pageNumber < 1 || pageNumber > inputDocument.PageCount)
            return null;

        var outputDocument = new PdfDocument();
        outputDocument.AddPage(inputDocument.Pages[pageNumber - 1]);

        using var ms = new MemoryStream();
        outputDocument.Save(ms);
        return ms.ToArray();
    }

    /// <summary>Render 1 trang PDF thành ảnh PNG. Trả về null nếu pageNumber ngoài phạm vi.</summary>
    public static byte[]? RenderPageAsPng(Stream pdfStream, int pageNumber, int dpi = 150)
    {
        using var ms = new MemoryStream();
        pdfStream.CopyTo(ms);
        var pdfBytes = ms.ToArray();

        var pageCount = PDFtoImage.Conversion.GetPageCount(pdfBytes);
        if (pageNumber < 1 || pageNumber > pageCount)
            return null;

        using var bitmap = PDFtoImage.Conversion.ToImage(pdfBytes, page: pageNumber - 1,
            options: new PDFtoImage.RenderOptions(Dpi: dpi));
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }
}
