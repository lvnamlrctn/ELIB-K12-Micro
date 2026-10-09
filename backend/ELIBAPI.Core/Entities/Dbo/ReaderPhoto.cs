using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Dbo;

/// <summary>
/// Ảnh khuôn mặt bổ sung của 1 bạn đọc — dùng cùng với <see cref="Reader.Photo"/> (ảnh chính) khi
/// nhận diện khuôn mặt (<see cref="Infrastructure.Services.FaceRecognitionService"/>) để tăng độ ổn
/// định khi bạn đọc đổi kiểu tóc/đeo kính/góc chụp khác nhau.
/// </summary>
[Table("ReaderPhoto", Schema = "dbo")]
public class ReaderPhoto
{
    [Key] public long Id { get; set; }
    public Guid PublicId { get; set; }
    public long ReaderId { get; set; }
    public long? TenantId { get; set; }
    public string? PhotoUrl { get; set; }
    public int? IsDelete { get; set; }
    public DateTime? CreatedRowDate { get; set; }
}
