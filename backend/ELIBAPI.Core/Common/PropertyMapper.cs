namespace ELIBAPI.Core.Common;

public static class PropertyMapper
{
    private static readonly HashSet<string> _auditFields =
    [
        "IsDelete", "CreatedRowBy", "UpdateRowBy", "CreatedRowDate", "UpdatedRowDate",
        "TenantId", "PublicId", "Id", "Bibid", "BibDataId", "BibId", "ID"
    ];

    public static void Map<TSource, TTarget>(TSource source, TTarget target)
        where TSource : class
        where TTarget : class
    {
        var sourceProps = typeof(TSource).GetProperties();
        foreach (var sp in sourceProps)
        {
            if (_auditFields.Contains(sp.Name)) continue;
            var tp = typeof(TTarget).GetProperty(sp.Name);
            if (tp == null || !tp.CanWrite) continue;
            try { tp.SetValue(target, sp.GetValue(source)); }
            catch { /* type mismatch — skip */ }
        }
    }
}

