using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Greenlight.BlameClient;

/// <summary>
/// The few Win32 calls a full-screen notice needs that Avalonia does not offer directly.
/// </summary>
/// <remarks>
/// Deliberately small, and deliberately not the click-through set the other clients use. This
/// window is the opposite of furniture: it is supposed to be in the way, it is supposed to
/// take the click that dismisses it, and the only thing it borrows from the desk-toy playbook
/// is keeping itself on top.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class AlertNative
{
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExAppWindow = 0x00040000;

    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;

    /// <summary>MB_ICONEXCLAMATION. The system's own "something is wrong" sound.</summary>
    private const uint BeepExclamation = 0x00000030;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint uType);

    /// <summary>
    /// Keep the notice out of Alt+Tab and off the taskbar.
    /// </summary>
    /// <remarks>
    /// It lives for twelve seconds. A taskbar button that appears and disappears in that time
    /// shuffles everything else along twice, and an Alt+Tab entry for it would let somebody
    /// tab <em>back</em> to a notice they had already dismissed.
    /// </remarks>
    public static void HideFromTaskbar(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;

        // Widened through uint first: these are bit patterns, not numbers, and a 32-bit signed
        // value with the top bit set would sign-extend across the 64-bit style word.
        var current = (long)GetWindowLongPtrW(hwnd, GwlExStyle);
        var updated = (current | (long)(uint)WsExToolWindow) & ~(long)(uint)WsExAppWindow;

        if (updated != current) SetWindowLongPtrW(hwnd, GwlExStyle, (IntPtr)updated);
    }

    /// <summary>
    /// Put the window back at the top of the z-order. Worth doing on a timer rather than once.
    /// </summary>
    /// <remarks>
    /// "Topmost" is a band, not a rank, and the shell is in the same band — so the last window
    /// to claim it wins, and the shell claims it whenever somebody touches the taskbar.
    /// Setting the flag once at startup gets you a notice that is on top until the moment
    /// anybody clicks anything, which for this app is most of the time it is on screen.
    /// </remarks>
    public static void KeepOnTop(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    /// <summary>
    /// The system alert sound, if the user has asked for it.
    /// </summary>
    /// <remarks>
    /// <c>MessageBeep</c> rather than <c>System.Media.SystemSounds</c>, which lives in a
    /// desktop-extensions package this project otherwise has no reason to carry. It respects
    /// the user's sound scheme, including the one where they have set it to silence.
    /// </remarks>
    public static void Beep()
    {
        try
        {
            MessageBeep(BeepExclamation);
        }
        catch
        {
            // A missing sound is not worth a crash on top of a broken build.
        }
    }
}
