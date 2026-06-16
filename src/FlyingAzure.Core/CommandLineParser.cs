namespace FlyingAzure.Core;

public enum ScreensaverMode
{
    Run,
    Configure,
    Preview,
}

public readonly record struct ParsedArgs(ScreensaverMode Mode, nint WindowHandle);

public static class CommandLineParser
{
    public static ParsedArgs Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new ParsedArgs(ScreensaverMode.Configure, 0);
        }

        string first = args[0].Trim();
        string flag = (first.Length >= 2 ? first[..2] : first).ToLowerInvariant();

        return flag switch
        {
            "/s" or "-s" => new ParsedArgs(ScreensaverMode.Run, 0),
            "/p" or "-p" => new ParsedArgs(ScreensaverMode.Preview, ParseHandle(first, args)),
            "/c" or "-c" => new ParsedArgs(ScreensaverMode.Configure, ParseHandle(first, args)),
            _ => new ParsedArgs(ScreensaverMode.Configure, 0),
        };
    }

    private static nint ParseHandle(string first, string[] args)
    {
        int colon = first.IndexOf(':');
        if (colon >= 0 && long.TryParse(first[(colon + 1)..], out long inline))
        {
            return (nint)inline;
        }

        if (args.Length >= 2 && long.TryParse(args[1], out long separate))
        {
            return (nint)separate;
        }

        return 0;
    }
}
