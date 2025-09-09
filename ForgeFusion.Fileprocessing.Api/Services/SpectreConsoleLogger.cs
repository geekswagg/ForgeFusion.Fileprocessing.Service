using Spectre.Console;

namespace ForgeFusion.Fileprocessing.Api.Services;

public interface IConsoleLogger
{
    void LogUpload(string fileName, string folder, long size, string? correlationId);
    void LogDownload(string blobName, string? folder, string? correlationId);
    void LogArchive(string blobName, string? fromFolder, string? correlationId);
    void LogListFiles(string? folder, int count);
    void LogGetFileTypes(string? folder, int typeCount);
    void LogGetAudit(string? blobName, string? folder, int? take, int resultCount);
    void LogError(string action, string error, string? correlationId = null);
    void LogValidation(string fileName, string error, string? correlationId = null);
}

public class SpectreConsoleLogger : IConsoleLogger
{
    public void LogUpload(string fileName, string folder, long size, string? correlationId)
    {
        var panel = new Panel(
            new Markup($"[green]?? FILE UPLOAD[/]\n" +
                      $"[white]File:[/] [cyan]{fileName}[/]\n" +
                      $"[white]Folder:[/] [yellow]{folder}[/]\n" +
                      $"[white]Size:[/] [blue]{FormatSize(size)}[/]" +
                      (string.IsNullOrEmpty(correlationId) ? "" : $"\n[white]Correlation ID:[/] [dim]{correlationId}[/]")))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green),
            Header = new PanelHeader($"[bold green]{DateTime.Now:HH:mm:ss}[/]")
        };
        
        AnsiConsole.Write(panel);
    }

    public void LogDownload(string blobName, string? folder, string? correlationId)
    {
        var panel = new Panel(
            new Markup($"[blue]?? FILE DOWNLOAD[/]\n" +
                      $"[white]Blob:[/] [cyan]{blobName}[/]\n" +
                      $"[white]Folder:[/] [yellow]{folder ?? "default"}[/]" +
                      (string.IsNullOrEmpty(correlationId) ? "" : $"\n[white]Correlation ID:[/] [dim]{correlationId}[/]")))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue),
            Header = new PanelHeader($"[bold blue]{DateTime.Now:HH:mm:ss}[/]")
        };
        
        AnsiConsole.Write(panel);
    }

    public void LogArchive(string blobName, string? fromFolder, string? correlationId)
    {
        var panel = new Panel(
            new Markup($"[purple]?? FILE ARCHIVE[/]\n" +
                      $"[white]Blob:[/] [cyan]{blobName}[/]\n" +
                      $"[white]From Folder:[/] [yellow]{fromFolder ?? "default"}[/]" +
                      (string.IsNullOrEmpty(correlationId) ? "" : $"\n[white]Correlation ID:[/] [dim]{correlationId}[/]")))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Purple),
            Header = new PanelHeader($"[bold purple]{DateTime.Now:HH:mm:ss}[/]")
        };
        
        AnsiConsole.Write(panel);
    }

    public void LogListFiles(string? folder, int count)
    {
        var panel = new Panel(
            new Markup($"[orange1]?? LIST FILES[/]\n" +
                      $"[white]Folder:[/] [yellow]{folder ?? "all"}[/]\n" +
                      $"[white]Files Found:[/] [blue]{count}[/]"))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Orange1),
            Header = new PanelHeader($"[bold orange1]{DateTime.Now:HH:mm:ss}[/]")
        };
        
        AnsiConsole.Write(panel);
    }

    public void LogGetFileTypes(string? folder, int typeCount)
    {
        var panel = new Panel(
            new Markup($"[deepskyblue1]?? FILE TYPES[/]\n" +
                      $"[white]Folder:[/] [yellow]{folder ?? "all"}[/]\n" +
                      $"[white]Types Found:[/] [blue]{typeCount}[/]"))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.DeepSkyBlue1),
            Header = new PanelHeader($"[bold deepskyblue1]{DateTime.Now:HH:mm:ss}[/]")
        };
        
        AnsiConsole.Write(panel);
    }

    public void LogGetAudit(string? blobName, string? folder, int? take, int resultCount)
    {
        var panel = new Panel(
            new Markup($"[mediumpurple1]?? AUDIT QUERY[/]\n" +
                      $"[white]Blob:[/] [cyan]{blobName ?? "any"}[/]\n" +
                      $"[white]Folder:[/] [yellow]{folder ?? "any"}[/]\n" +
                      $"[white]Limit:[/] [blue]{take?.ToString() ?? "none"}[/]\n" +
                      $"[white]Results:[/] [green]{resultCount}[/]"))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.MediumPurple1),
            Header = new PanelHeader($"[bold mediumpurple1]{DateTime.Now:HH:mm:ss}[/]")
        };
        
        AnsiConsole.Write(panel);
    }

    public void LogError(string action, string error, string? correlationId = null)
    {
        var panel = new Panel(
            new Markup($"[red]? ERROR[/]\n" +
                      $"[white]Action:[/] [cyan]{action}[/]\n" +
                      $"[white]Error:[/] [red]{error}[/]" +
                      (string.IsNullOrEmpty(correlationId) ? "" : $"\n[white]Correlation ID:[/] [dim]{correlationId}[/]")))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Red),
            Header = new PanelHeader($"[bold red]{DateTime.Now:HH:mm:ss}[/]")
        };
        
        AnsiConsole.Write(panel);
    }

    public void LogValidation(string fileName, string error, string? correlationId = null)
    {
        var panel = new Panel(
            new Markup($"[orange3]??  VALIDATION ERROR[/]\n" +
                      $"[white]File:[/] [cyan]{fileName}[/]\n" +
                      $"[white]Error:[/] [orange3]{error}[/]" +
                      (string.IsNullOrEmpty(correlationId) ? "" : $"\n[white]Correlation ID:[/] [dim]{correlationId}[/]")))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Orange3),
            Header = new PanelHeader($"[bold orange3]{DateTime.Now:HH:mm:ss}[/]")
        };
        
        AnsiConsole.Write(panel);
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}