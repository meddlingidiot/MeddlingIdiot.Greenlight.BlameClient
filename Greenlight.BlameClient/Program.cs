using Automation.Velopack;
using Avalonia;
using Velopack;

namespace Greenlight.BlameClient;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Before Avalonia, and before anything else. Velopack's hooks — first run, update,
        // uninstall — are handled inside Run(), which then exits the process; started any later
        // and an install would flash a window on its way past, or miss the hook altogether.
        VelopackApp.Build().Run();
        VelopackBootstrapper.Startup("Greenlight.BlameClient", args);

        // One per session, and the second one leaves without a word. Two copies — the one
        // Windows started and the one somebody launched by hand — would each put up their own
        // notice for the same breakage, each with its own place in the taunt rotation, and each
        // re-claim the top of the z-order every second. What that looks like is one notice
        // flicking between taunts, which reads as a bug in the taunts rather than as two apps.
        //
        // Taken after the Velopack hooks, so an install or an update running beside a copy that
        // is already up is never turned away. Local\ is this logon session, so two people signed
        // in to the same machine each get their own.
        using var single = new Mutex(initiallyOwned: true, @"Local\Greenlight.BlameClient", out var first);
        if (!first) return;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
