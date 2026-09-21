using System.Diagnostics;

namespace Greenlight.BlameClient;

/// <summary>
/// Opening a build page in whatever the machine uses for the web.
/// </summary>
/// <remarks>
/// Its own file because two places want it — the notice while it is up, and the tray menu
/// after it has gone — and the alternative was a window constructed solely to borrow a method
/// off it.
/// </remarks>
public static class Browse
{
    /// <summary>
    /// Open a URL, or do nothing at all.
    /// </summary>
    /// <remarks>
    /// Nothing here throws. The URL arrives from a build provider by way of the SDK, so it can
    /// be empty, and on a locked-down machine the shell can simply refuse — neither is worth
    /// an error dialog stacked on top of an alert that has already interrupted somebody once.
    /// </remarks>
    public static void Open(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        // Only the two schemes a build page could sensibly be. UseShellExecute will cheerfully
        // launch anything the machine has an association for, and a provider that sent
        // something other than a link should not be able to start a process on somebody's desk.
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // No browser, or the shell refused.
        }
    }
}
