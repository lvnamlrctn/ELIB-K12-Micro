using System.Collections.Generic;

namespace ELIBAPI.Core.DTOs.Response;

public class ModuleItemResponse
{
    public string? Title { get; set; }
    public string? Icon { get; set; }
    public string? Link { get; set; }
    public bool? Expanded { get; set; }
    public List<ModuleItemResponse>? Children { get; set; }
}

public class ModuleTreeResponse
{
    public long     Id          { get; set; }
    public string?  Name        { get; set; }
    public long?    ParentId    { get; set; }
    public string?  ParentName  { get; set; }
    public string?  Link        { get; set; }
    public string?  Icon        { get; set; }
    public string?  Language    { get; set; }
    public string?  PortalId    { get; set; }
    public long?    Group       { get; set; }
    public int?     SortOrder   { get; set; }
    public string?  ModuleCode  { get; set; }
    public string?  Type        { get; set; }
    public string?  FolderPage  { get; set; }
    public int?     Status      { get; set; }
    public Guid     PublicId    { get; set; }
    public bool     HasChildren { get; set; }
    public int      ChildCount  { get; set; }
    public List<ModuleTreeResponse> Children { get; set; } = [];
}
