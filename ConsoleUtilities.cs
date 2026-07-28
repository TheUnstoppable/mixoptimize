namespace mixoptimize;

public static class ConsoleUtilities
{
    extension(AnsiConsole)
    {
        public static void WarningLine(string format, params object[]? args)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]{string.Format(format, args!)}[/]");
        }
        
        public static void ErrorLine(string format, params object[]? args)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{string.Format(format, args!)}[/]");
        }
    }
}