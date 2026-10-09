namespace ELIBAPI.Core.DTOs.Elasticsearch;

public class EbookItemDocument
{
    public long    EbookId        { get; set; }
    public string? Title          { get; set; }
    public string? Author         { get; set; }
    public string? PublishDate    { get; set; }
    public string? Publisher      { get; set; }
    public string? Isbn           { get; set; }
    public string? Issn           { get; set; }
    public string? Keyword        { get; set; }
    public long?   CollectionId   { get; set; }
    public string? CollectionName { get; set; }
    public long?   SubjectId      { get; set; }
    public string? SubjectName    { get; set; }
    public long?   TopicId        { get; set; }
    public string? TopicName      { get; set; }
    public string? Images         { get; set; }
    public string? Content        { get; set; }
    public string? EbookFileId    { get; set; }
}
