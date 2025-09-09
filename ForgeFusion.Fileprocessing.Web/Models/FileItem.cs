namespace ForgeFusion.Fileprocessing.Web.Models;

public sealed class FileItem
{
    public required string Name { get; init; }
    public required string Folder { get; init; }
    public required long ContentLength { get; init; }
    public required string ContentType { get; init; }
    public required DateTimeOffset LastModified { get; init; }
}