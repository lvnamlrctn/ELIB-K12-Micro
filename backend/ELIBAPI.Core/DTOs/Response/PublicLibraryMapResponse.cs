namespace ELIBAPI.Core.DTOs.Response;

public class PublicMapBuildingResponse
{
    public long    Id   { get; set; }
    public string? Name { get; set; }
    public string? Code { get; set; }
}

public class PublicMapFloorSummaryResponse
{
    public long    Id           { get; set; }
    public long    BuildingId   { get; set; }
    public string? BuildingName { get; set; }
    public int?    FloorNumber  { get; set; }
    public string? Name         { get; set; }
}

public class PublicMapShelfRowInfo
{
    public long    Id          { get; set; }
    public int?    RowIndex    { get; set; }
    public string? DdcStart    { get; set; }
    public string? DdcEnd      { get; set; }
    public string? Description { get; set; }
}

public class PublicMapShelfDetailResponse
{
    public string? CategoryRange { get; set; }
    public string? SubjectName   { get; set; }
    public int?    Capacity      { get; set; }
    /// <summary>Số bản sách thật đang gán vào giá này (đếm từ Barcode.MapObjectId) — không suy diễn.</summary>
    public int     OccupiedCount { get; set; }
    public List<PublicMapShelfRowInfo> Rows { get; set; } = [];
}

public class PublicMapObjectResponse
{
    public long    Id         { get; set; }
    public string? Name       { get; set; }
    public string? Code       { get; set; }
    public string? ObjectType { get; set; }
    public double? PositionX  { get; set; }
    public double? PositionY  { get; set; }
    public double? Width      { get; set; }
    public double? Height     { get; set; }
    public string? ColorHex   { get; set; }
    public string? IconName   { get; set; }
    public PublicMapShelfDetailResponse? ShelfDetail { get; set; }
}

public class PublicMapFloorResponse
{
    public long    Id           { get; set; }
    public string? Name         { get; set; }
    public int?    FloorNumber  { get; set; }
    public double? Width        { get; set; }
    public double? Height       { get; set; }
    public long    BuildingId   { get; set; }
    public string? BuildingName { get; set; }
    public List<PublicMapObjectResponse> Objects { get; set; } = [];
}

/// <summary>Kết quả tra cứu vị trí thật của 1 bản sách (barcode) cụ thể.</summary>
public class PublicBookLocationResponse
{
    public bool    Found  { get; set; }
    /// <summary>"assigned" = đã xếp giá thật cho đúng bản sách này; "ddc-range" = ước lượng theo khoảng DDC; "none" = không xác định được.</summary>
    public string  Source { get; set; } = "none";

    public long?   BuildingId   { get; set; }
    public string? BuildingName { get; set; }
    public long?   FloorId      { get; set; }
    public int?    FloorNumber  { get; set; }
    public string? FloorName    { get; set; }
    public double? FloorWidth   { get; set; }
    public double? FloorHeight  { get; set; }

    public long?   ShelfObjectId { get; set; }
    public string? ShelfCode     { get; set; }
    public string? ShelfName     { get; set; }
    public double? PositionX     { get; set; }
    public double? PositionY     { get; set; }

    public long?   ShelfRowId    { get; set; }
    public int?    RowIndex      { get; set; }
    public string? DdcStart      { get; set; }
    public string? DdcEnd        { get; set; }
    public string? CategoryRange { get; set; }
    public string? SubjectName   { get; set; }

    /// <summary>Điểm xuất phát thật (MapObject loại DOOR/ELEVATOR gần nhất cùng tầng) để vẽ đường dẫn — null nếu tầng chưa có.</summary>
    public long?   EntranceObjectId  { get; set; }
    public double? EntrancePositionX { get; set; }
    public double? EntrancePositionY { get; set; }
}
