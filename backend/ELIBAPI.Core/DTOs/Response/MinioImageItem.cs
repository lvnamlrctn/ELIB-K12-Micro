namespace ELIBAPI.Core.DTOs.Response;

public class MinioImageItem
{
    public string   Path         { get; set; } = "";
    public string   FileName     { get; set; } = "";
    public long     Size         { get; set; }
    public DateTime UploadedDate { get; set; }
}
