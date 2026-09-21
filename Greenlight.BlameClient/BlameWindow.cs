using System.Diagnostics;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace Greenlight.BlameClient;

/// <summary>
/// The notice itself: an undecorated, always-on-top window over the whole desktop for as long
/// as it takes to read a name.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the other Greenlight clients this window is <em>not</em> click-through. It is meant
/// to be in the way, and anything a person does — a click, a key, the pointer wheel — takes
/// it away again. A full-screen window that ignored input would be indistinguishable from a
/// crashed machine, and somebody would eventually pull the power out of one.
/// </para>
/// <para>
/// It closes itself. The timer is the only thing that has to work: everything else here is a
/// convenience, but a notice that stayed up because a dismiss handler did not fire is a
/// support call and, if it happens during a demonstration, a very memorable one.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class BlameWindow : Window
{
    /// <summary>
    /// How tall the banner version is, as a fraction of the screen. Enough for a name and the
    /// line under it and nothing else.
    /// </summary>
    private const double BannerShare = 0.26;

    private readonly BlameConfig _config;
    private readonly BlameCanvas _canvas;
    private readonly DispatcherTimer _frames;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private TimeSpan _lastFrame;
    private TimeSpan _lastHousekeeping;

    /// <summary>
    /// How long the notice ignores input after appearing.
    /// </summary>
    /// <remarks>
    /// A third of a second. Somebody mid-click when it lands would otherwise dismiss it with
    /// a click they had already committed to and never see what it said — which is both the
    /// most annoying possible failure and the easiest one to hit, because the notice arrives
    /// precisely when a person is busy doing something else.
    /// </remarks>
    private static readonly TimeSpan Deaf = TimeSpan.FromMilliseconds(330);

    public BlameWindow(BlameConfig config, BlameAlert alert)
    {
        _config = config;
        Alert = alert;

        Title = "Greenlight blame";
        WindowDecorations = WindowDecorations.None;
        CanResize = false;
        ShowInTaskbar = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;

        // Transparent so the fades work against whatever is underneath: the backdrop is drawn,
        // not set, precisely so it can arrive and leave.
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];

        _canvas = new BlameCanvas
        {
            Alert = alert,
            Animated = config.Animate,
            Opacity = Math.Clamp(config.Opacity, 0.3, 1.0),
        };
        Content = _canvas;

        _frames = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _frames.Tick += OnFrame;
    }

    /// <summary>What is on screen.</summary>
    public BlameAlert Alert { get; }

    /// <summary>Raised once, when the notice has finished fading and the window has closed.</summary>
    public event EventHandler? Done;

    /// <summary>
    /// Open the build page the notice is about, if there is one, and start dismissing.
    /// </summary>
    /// <remarks>
    /// Reachable from the tray rather than from the notice itself. A clickable link on a
    /// window whose entire surface means "make this go away" is a trap — the one thing
    /// somebody wants from a full-screen alert is confidence about what clicking it does.
    /// </remarks>
    public void OpenBuild()
    {
        Alert.Dismiss();
        Browse.Open(Alert.Blame.WebUrl);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        var hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        AlertNative.HideFromTaskbar(hwnd);
        AlertNative.KeepOnTop(hwnd);

        LayOut();

        // Focus, so that a key press dismisses it. This does take the caret out of whatever
        // somebody was typing in, which is rude — and is the feature: an alert nobody has to
        // acknowledge is an alert nobody reads. It is also why every key dismisses rather than
        // only Escape, so the next thing they type puts them back.
        Activate();
        Focus();

        if (_config.Sound) AlertNative.Beep();

        _lastFrame = _clock.Elapsed;
        _frames.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _frames.Stop();
        base.OnClosed(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Wave(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        Wave(e);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Wave(e);
    }

    /// <summary>
    /// Put the window over the screen. Re-run on a timer, because a notice is quite likely to
    /// arrive while somebody is plugging a monitor in.
    /// </summary>
    private void LayOut()
    {
        var area = DesktopArea.Find(_config.Area);
        if (area.IsEmpty) return;

        var scaling = RenderScaling <= 0 ? 1 : RenderScaling;

        // Position is physical, Width and Height are logical. Mixing those up puts the notice
        // at a plausible-looking but wrong size on every scaled display, which is most of them
        // — and for this app "wrong size" means somebody's actual work showing round the edge
        // of the thing that was supposed to be covering it.
        Position = new PixelPoint(area.X, area.Y);
        Width = area.Width / scaling;
        Height = (_config.FullScreen ? area.Height : area.Height * BannerShare) / scaling;
    }

    private void Wave(RoutedEventArgs e)
    {
        if (Alert.Seconds < Deaf.TotalSeconds) return;

        Alert.Dismiss();
        e.Handled = true;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        var elapsed = now - _lastFrame;
        _lastFrame = now;

        if (now - _lastHousekeeping >= TimeSpan.FromSeconds(1))
        {
            _lastHousekeeping = now;

            // Re-claimed every second for the twelve seconds this exists. The shell takes the
            // top of the band back whenever anybody touches the taskbar, and a notice that had
            // quietly slipped behind the window it was accusing somebody about would be worse
            // than no notice at all.
            AlertNative.KeepOnTop(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
            LayOut();
        }

        Alert.Advance(elapsed);

        if (Alert.IsFinished)
        {
            _frames.Stop();
            Close();
            Done?.Invoke(this, EventArgs.Empty);
            return;
        }

        _canvas.InvalidateVisual();
    }
}
