using Elib.BuildingBlocks.Domain;

namespace Elib.Holdings.Domain;

/// <summary>Loại kho (monolith: StoreType, quyền STORE_TYPES) — vd Kho mở, Kho đóng, Kho tham khảo.</summary>
public sealed class StoreType : NamedCatalogItem
{
    public override int MaxNameLength => 250;
}

/// <summary>Kho tài liệu (monolith: Store, quyền STORES). Mã kho in trên nhãn gáy và phiếu, duy nhất trong đơn vị.</summary>
public sealed class Store : TenantEntity
{
    private Store() { }

    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public long? StoreTypeId { get; private set; }

    /// <summary>Vị trí (monolith: Postion) — vd "Tầng 2, phòng 201".</summary>
    public string? Position { get; private set; }

    /// <summary>Sức chứa (số bản) — chỉ để hiển thị, không chặn đăng ký.</summary>
    public int? Capacity { get; private set; }

    public static Store Create(string code, string name, long? storeTypeId, string? position, int? capacity)
    {
        var store = new Store();
        store.Update(code, name, storeTypeId, position, capacity);
        return store;
    }

    public void Update(string code, string name, long? storeTypeId, string? position, int? capacity)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        Code = c.Length is > 0 and <= 20 ? c : throw new BusinessRuleException("CODE_INVALID", "Mã kho bắt buộc, tối đa 20 ký tự.");
        var n = (name ?? "").Trim();
        Name = n.Length is > 0 and <= 250 ? n : throw new BusinessRuleException("NAME_INVALID", "Tên kho bắt buộc, tối đa 250 ký tự.");
        StoreTypeId = storeTypeId;
        var p = position?.Trim();
        Position = string.IsNullOrEmpty(p) ? null : p.Length <= 500 ? p : throw new BusinessRuleException("POSITION_INVALID", "Vị trí tối đa 500 ký tự.");
        Capacity = capacity is null or >= 0 ? capacity : throw new BusinessRuleException("CAPACITY_INVALID", "Sức chứa không được âm.");
    }
}
