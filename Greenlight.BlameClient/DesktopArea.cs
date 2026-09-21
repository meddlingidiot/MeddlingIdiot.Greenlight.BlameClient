using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Greenlight.BlameClient;

/// <summary>A rectangle in physical screen pixels.</summary>
public readonly record struct AreaBounds(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

/// <summary>How much of the screen the notice is allowed to take.</summary>
public enum AreaChoice
{
    /// <summary>
    /// Everything, every monitor, taskbar included. The default, and the honest one: an alert
    /// that politely stops at the taskbar is an alert somebody can ignore by looking slightly
    /// downwards.
    /// </summary>
    FullScreen,

    /// <summary>
    /// The desktop as far as the taskbar, on the primary monitor only. For people who would
    /// like to be able to reach the Start button while being shouted at.
    /// </summary>
    WorkArea,
}

/// <summary>
/// Works out the rectangle the notice covers.
/// </summary>
[SupportedOSPlatform("windows")]
public static class DesktopArea
{
    private const uint SpiGetWorkArea = 0x0030;

    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfoW(uint uiAction, uint uiParam, ref Rect pvParam, uint fWinIni);

    /// <summary>Where the notice goes, in physical pixels.</summary>
    public static AreaBounds Find(AreaChoice choice)
    {
        if (choice == AreaChoice.WorkArea)
        {
            var work = new Rect();
            if (SystemParametersInfoW(SpiGetWorkArea, 0, ref work, 0))
            {
                var width = work.Right - work.Left;
                var height = work.Bottom - work.Top;
                if (width > 0 && height > 0) return new AreaBounds(work.Left, work.Top, width, height);
            }
        }

        // The virtual screen rather than the primary one. Somebody with three monitors has
        // three places they might be looking, and a notice on one of them is a notice they
        // will find out about from a colleague.
        var x = GetSystemMetrics(SmXVirtualScreen);
        var y = GetSystemMetrics(SmYVirtualScreen);
        var cx = GetSystemMetrics(SmCxVirtualScreen);
        var cy = GetSystemMetrics(SmCyVirtualScreen);

        return cx > 0 && cy > 0 ? new AreaBounds(x, y, cx, cy) : new AreaBounds(0, 0, 1280, 720);
    }
}
