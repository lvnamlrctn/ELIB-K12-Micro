using Elib.BuildingBlocks.Domain;

namespace Elib.Circulation.Domain;

/// <summary>
/// Lượt vào/ra thư viện theo thẻ bạn đọc (monolith: CheckIn + CheckOut gộp một, như <see cref="Loan"/> gộp BookOut/BookIn — ra là
/// điền <see cref="CheckedOutAt"/>). Không phát event — chỉ dùng tại quầy tiếp nhận, không service khác cần biết.
/// </summary>
public sealed class LibraryVisit : TenantEntity
{
    private LibraryVisit() { }

    public Guid ReaderPublicId { get; private set; }
    public string CardNo { get; private set; } = "";
    public long CircPlaceId { get; private set; }
    public DateTimeOffset CheckedInAt { get; private set; }
    public long? CheckedInBy { get; private set; }
    public DateTimeOffset? CheckedOutAt { get; private set; }
    public long? CircPlaceOutId { get; private set; }
    public long? CheckedOutBy { get; private set; }

    public bool IsOpen => CheckedOutAt is null;

    public static LibraryVisit Open(PatronReplica reader, long circPlaceId, DateTimeOffset now, long? staffId)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return new LibraryVisit
        {
            ReaderPublicId = reader.ReaderPublicId, CardNo = reader.CardNo, CircPlaceId = circPlaceId,
            CheckedInAt = now, CheckedInBy = staffId,
        };
    }

    public void Close(DateTimeOffset now, long? circPlaceId, long? staffId)
    {
        if (!IsOpen) throw new BusinessRuleException("VISIT_ALREADY_CHECKED_OUT", $"Thẻ {CardNo} đã quét ra lúc {CheckedOutAt:dd/MM/yyyy HH:mm}.");
        CheckedOutAt = now;
        CircPlaceOutId = circPlaceId;
        CheckedOutBy = staffId;
    }
}
