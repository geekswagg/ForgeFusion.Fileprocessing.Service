namespace ForgeFusion.Fileprocessing.Service.Models;

public sealed class FileTypeCount
{
    public required string FileType { get; init; }
    public required long Count { get; init; }
}
