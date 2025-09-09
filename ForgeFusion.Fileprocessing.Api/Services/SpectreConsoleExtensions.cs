using Spectre.Console;

namespace ForgeFusion.Fileprocessing.Api.Services;

public static class SpectreConsoleExtensions
{
    public static void WriteStartupBanner(this IConsoleLogger logger, string environment, string containerName, long? maxFileSize)
    {
        AnsiConsole.Clear();
        
        AnsiConsole.Write(
            new FigletText("ForgeFusion API")
                .Centered()
                .Color(Color.Cyan1));

        var rule = new Rule("[bold yellow]File Processing Service[/]")
        {
            Style = Style.Parse("yellow")
        };
        AnsiConsole.Write(rule);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold]Configuration[/]").Centered())
            .AddColumn(new TableColumn("[bold]Value[/]").Centered());

        table.AddRow("[cyan]Environment[/]", $"[yellow]{environment}[/]");
        table.AddRow("[cyan]Container[/]", $"[blue]{containerName}[/]");
        table.AddRow("[cyan]Max File Size[/]", maxFileSize.HasValue ? $"[green]{FormatSize(maxFileSize.Value)}[/]" : "[dim]No limit[/]");
        table.AddRow("[cyan]Started At[/]", $"[white]{DateTime.Now:yyyy-MM-dd HH:mm:ss}[/]");

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Markup("[green]? API is ready to accept requests![/]"));
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
    }

    public static void WriteShutdownMessage()
    {
        AnsiConsole.WriteLine();
        var rule = new Rule("[bold red]Shutting Down[/]")
        {
            Style = Style.Parse("red")
        };
        AnsiConsole.Write(rule);
        
        AnsiConsole.Write(
            new Panel(
                new Markup("[red]?? ForgeFusion API Stopped[/]\n" +
                          $"[white]Shutdown Time:[/] [yellow]{DateTime.Now:yyyy-MM-dd HH:mm:ss}[/]"))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Red)
            });
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