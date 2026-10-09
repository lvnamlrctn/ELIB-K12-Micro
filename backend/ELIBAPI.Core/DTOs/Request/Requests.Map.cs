namespace ELIBAPI.Core.DTOs.Request;

// ==================== MAP_BUILDING ====================
public class MapBuildingRequest
{
    public string? Name        { get; set; }
    public string? Code        { get; set; }
    public string? Description { get; set; }
}
public class MapBuildingSearchRequest : SearchRequest { }

// ==================== MAP_FLOOR ====================
public class MapFloorRequest
{
    public long    BuildingId     { get; set; }
    public int?    FloorNumber    { get; set; }
    public string? Name           { get; set; }
    public string? LayoutImageUrl { get; set; }
    public double? Width          { get; set; }
    public double? Height         { get; set; }
}
public class MapFloorSearchRequest : SearchRequest
{
    public long? BuildingId { get; set; }
}

// ==================== MAP_FLOOR_UTILITY ====================
public class MapFloorUtilityRequest
{
    public long    FloorId         { get; set; }
    public string? Name            { get; set; }
    public string? Description     { get; set; }
    public int?    Quantity        { get; set; }
    public string? ConditionStatus { get; set; }
    public string? IconName        { get; set; }
}
public class MapFloorUtilitySearchRequest : SearchRequest
{
    public long? FloorId { get; set; }
}

// ==================== MAP_OBJECT ====================
public class MapObjectRequest
{
    public long    FloorId    { get; set; }
    public string? Name       { get; set; }
    public string? Code       { get; set; }
    public string? ObjectType { get; set; }
    public int?    Category   { get; set; }
    public double? PositionX  { get; set; }
    public double? PositionY  { get; set; }
    public double? Width      { get; set; }
    public double? Height     { get; set; }
    public string? ColorHex   { get; set; }
    public string? IconName   { get; set; }
    public long?   StoreId    { get; set; }
}
public class MapObjectSearchRequest : SearchRequest
{
    public long?   FloorId    { get; set; }
    public string? ObjectType { get; set; }
    public int?    Category   { get; set; }
    public long?   StoreId    { get; set; }
}

// ==================== MAP_SHELF_DETAIL ====================
public class MapShelfDetailRequest
{
    public long    ObjectId      { get; set; }
    public string? CategoryRange { get; set; }
    public string? SubjectName   { get; set; }
    public int?    Capacity      { get; set; }
    public string? Description   { get; set; }
}
public class MapShelfDetailSearchRequest : SearchRequest
{
    public long? ObjectId { get; set; }
}

// ==================== MAP_SHELF_ROW ====================
public class MapShelfRowRequest
{
    public long    ShelfDetailId { get; set; }
    public int?    RowIndex      { get; set; }
    public string? DdcStart      { get; set; }
    public string? DdcEnd        { get; set; }
    public string? Description   { get; set; }
}
public class MapShelfRowSearchRequest : SearchRequest
{
    public long? ShelfDetailId { get; set; }
}

// ==================== MAP_EQUIPMENT ====================
public class MapEquipmentRequest
{
    public long    ObjectId        { get; set; }
    public string? Name            { get; set; }
    public string? Description     { get; set; }
    public int?    Quantity        { get; set; }
    public string? ConditionStatus { get; set; }
}
public class MapEquipmentSearchRequest : SearchRequest
{
    public long? ObjectId { get; set; }
}

// ==================== ROOM_BOOKING_CONFIG ====================
public class RoomBookingConfigRequest
{
    public long MapObjectId                { get; set; }
    public int  Capacity                   { get; set; }
    public int  SlotMinutes                { get; set; } = 60;
    public int  MinAdvanceMinutes           { get; set; }
    public int  MaxAdvanceDays             { get; set; } = 7;
    public int  MaxBookingMinutesPerReader { get; set; }
    public int  CheckInGraceMinutes        { get; set; } = 15;
    public bool AutoApprove                { get; set; }
    public int? MaxAdvanceHours            { get; set; }
    public string? Rules                   { get; set; }
    public string? AllowedReaderTypeIds    { get; set; }
    public int? MinGroupSize               { get; set; }
}
public class RoomBookingConfigSearchRequest : SearchRequest
{
    public long? MapObjectId { get; set; }
}

// ==================== ROOM_BOOKING (đặt phòng học nhóm) ====================
// RoomBookingRequest chỉ tồn tại để thoả mãn tham số kiểu chung IGenericRepository/GenericController
// (RoomBookingAdminController chỉ dùng các thao tác GetById/GetByPublicId/Search/SearchAll/Approve/Reject
// — không có Add/Update/Delete qua đường generic, luồng tạo booking đi qua CreateRoomBookingRequest ở
// api/public/RoomBooking/Book).
public class RoomBookingRequest { }

public class RoomBookingSearchRequest : SearchRequest
{
    public long? MapObjectId { get; set; }
    public long? ReaderId    { get; set; }
    public int?  Status      { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo   { get; set; }
    /// Loại cơ sở của phòng (MapObject.Category).
    public int?      Category { get; set; }
}

/// Bạn đọc gửi yêu cầu đặt phòng qua OPAC (api/public/RoomBooking/Book).
public class CreateRoomBookingRequest
{
    public long     MapObjectId { get; set; }
    public DateTime StartAt     { get; set; }
    public DateTime EndAt       { get; set; }
    public int      PartySize   { get; set; }
    public string?  Note        { get; set; }
    /// Số thẻ hoặc UID thẻ chip của các thành viên (không gồm người đặt). Có thành viên thì PartySize = số thành viên + 1.
    public List<string>? MemberCards { get; set; }
}

/// Nhân viên từ chối yêu cầu đặt phòng (api/Map/RoomBookingAdmin/Reject/{publicId}).
public class RejectRoomBookingRequest
{
    public string? Reason { get; set; }
}
