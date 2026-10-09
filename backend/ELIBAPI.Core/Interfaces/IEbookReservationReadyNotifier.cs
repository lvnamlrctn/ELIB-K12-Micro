using ELIBAPI.Core.Entities.Ebook;

namespace ELIBAPI.Core.Interfaces;

/// Gửi email "đến lượt mượn" cho các đặt trước vừa được thăng hạng (Status → 2, Ready). Dùng chung ở
/// mọi nơi có thể kích hoạt thăng hạng để đảm bảo bạn đọc luôn được báo, bất kể nguyên nhân trả slot
/// (huỷ đặt trước, trả sớm, job tự động hết hạn, hay lazy-check khi tải danh sách đặt trước).
public interface IEbookReservationReadyNotifier
{
    Task NotifyAsync(IEnumerable<EbookItemReservation> promoted);
}
