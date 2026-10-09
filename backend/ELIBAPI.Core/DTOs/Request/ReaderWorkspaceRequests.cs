using System.Text.Json;

namespace ELIBAPI.Core.DTOs.Request;

// ==================== READER WORKSPACE (Đợt 9) ====================

public class SaveWorkspaceRequest
{
    public Guid Version { get; set; }
    public JsonElement Projects { get; set; }
    public JsonElement Highlights { get; set; }
}

public class SaveReaderSearchRequest
{
    public string Name { get; set; } = "";
    public UnifiedSearchRequest Query { get; set; } = new();
    public bool AlertsEnabled { get; set; } = true;
}
