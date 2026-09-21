using System.Diagnostics;
using System.Runtime.Versioning;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Greenlight.BlameClient;

/// <summary>
/// The mascot in the notification area, and the menu hanging off him. For almost all of this
/// app's life it is the only part of it that exists.
/// </summary>
/// <remarks>
/// <para>
/// Which makes the icon do more work than it does in the other clients: it is the only
/// evidence the thing is running at all, and the tooltip is the only way to find out whether
/// it is currently watching, muted, or sat there with no Greenlight to listen to.
/// </para>
/// <para>
/// Avalonia's own <see cref="TrayIcon"/> rather than a tray library, because the sample is
/// meant to be readable — and because a sample that drags in a dependency to draw one icon is
/// making a point nobody asked for.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class BlameTray : IDisposable
{
    private static readonly Uri IconUri = new("avares://Greenlight.BlameClient/Assets/MeddlingIdiot.ico");

    private readonly BlameConfig _config;
    private readonly TrayIcon _tray;
    private readonly NativeMenuItem _status;
    private readonly NativeMenuItem _watching;
    private readonly NativeMenuItem _lastBuild;
    private readonly NativeMenuItem _startup;

    public BlameTray(BlameConfig config)
    {
        _config = config;

        _status = new NativeMenuItem { Header = "Waiting for Greenlight…", IsEnabled = false };

        _watching = new NativeMenuItem
        {
            Header = "Watching for breakages",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = true,
        };
        _watching.Click += (_, _) => SetWatching(!(IsWatching?.Invoke() ?? true));

        // Disabled until something has actually broken. An "open the build" that opens nothing
        // is worse than one that is visibly not available yet.
        _lastBuild = new NativeMenuItem { Header = "Open the last breakage", IsEnabled = false };
        _lastBuild.Click += (_, _) => OnOpenLastBuild?.Invoke();

        // Read from the registry rather than from a setting of ours, every time it is shown:
        // the user can turn this off in Task Manager's Startup tab, and a tick remembering what
        // we last wrote would then be telling them the opposite of the truth.
        _startup = Check("Start with Windows", WindowsStartup.IsEnabled, value => WindowsStartup.Set(value));

        var menu = BuildMenu();
        menu.Opening += (_, _) => _startup.IsChecked = WindowsStartup.IsEnabled();

        _tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(IconUri)),
            ToolTipText = "Greenlight blame — watching",
            Menu = menu,
            IsVisible = true,
        };

        // A left click mutes and unmutes. There is no window to open, a tray icon that does
        // nothing at all when clicked reads as a hung one, and "shut up for a minute" is by
        // some distance the thing somebody will most want from this app in a hurry.
        _tray.Clicked += (_, _) => SetWatching(!(IsWatching?.Invoke() ?? true));
    }

    /// <summary>Whether a breakage would currently raise a notice.</summary>
    public Func<bool>? IsWatching { get; set; }

    /// <summary>Start or stop raising notices. Off is remembered until this is turned back on.</summary>
    public Action<bool>? OnSetWatching { get; set; }

    /// <summary>A setting changed that the app can absorb without a restart, which is all of them.</summary>
    public Action? OnConfigChanged { get; set; }

    /// <summary>Re-read the file, for taunts added by hand.</summary>
    public Action? OnReloadConfig { get; set; }

    /// <summary>
    /// Put a made-up notice on the screen.
    /// </summary>
    /// <remarks>
    /// The only way to find out what this app looks like without breaking a pipeline, and
    /// therefore the item that will be clicked more than every other item combined. It is also
    /// how somebody checks their new taunt reads the way they thought it did.
    /// </remarks>
    public Action? OnDemonstrate { get; set; }

    /// <summary>Open the build page for the most recent breakage.</summary>
    public Action? OnOpenLastBuild { get; set; }

    public Action? OnQuit { get; set; }

    /// <summary>Say what the app is doing, in the tooltip and at the top of the menu.</summary>
    /// <param name="attached">Whether there is a Greenlight to listen to.</param>
    /// <param name="broken">Whether something is broken right now.</param>
    /// <param name="last">The last person named, if anybody has been.</param>
    public void ShowState(bool attached, bool broken, string? last)
    {
        var watching = IsWatching?.Invoke() ?? true;

        _status.Header = !attached
            ? "Greenlight not running — nothing to watch"
            : broken
                ? "Something is broken right now"
                : "Everything is passing";

        _watching.IsChecked = watching;
        _lastBuild.IsEnabled = last is not null;

        _lastBuild.Header = last is null
            ? "Open the last breakage"
            : $"Open {last}'s breakage";

        _tray.ToolTipText = !watching
            ? "Greenlight blame — muted"
            : attached
                ? broken ? "Greenlight blame — something is broken" : "Greenlight blame — watching"
                : "Greenlight blame — no Greenlight";
    }

    public void Dispose()
    {
        _tray.IsVisible = false;
        _tray.Dispose();
    }

    private void SetWatching(bool watching)
    {
        OnSetWatching?.Invoke(watching);
        _watching.IsChecked = IsWatching?.Invoke() ?? watching;
    }

    private NativeMenu BuildMenu() =>
    [
        _status,
        new NativeMenuItemSeparator(),
        _watching,
        new NativeMenuItemSeparator(),
        Submenu("How long it stays up",
            Seconds("A glance (6s)", 6),
            Seconds("Long enough (12s)", 12),
            Seconds("Uncomfortable (25s)", 25),
            Seconds("Make it count (60s)", 60)),
        Submenu("How much screen it takes",
            Shape("All of it", full: true),
            Shape("A banner across the top", full: false)),
        Submenu("Which screens",
            Area("Every monitor", AreaChoice.FullScreen),
            Area("The main one, above the taskbar", AreaChoice.WorkArea)),
        Submenu("How solid",
            Opacity("Solid", 1.0),
            Opacity("Nearly solid", 0.94),
            Opacity("You can see through it", 0.75)),
        Check("Stripes and movement",
            () => _config.Animate,
            value =>
            {
                _config.Animate = value;
                Persist();
                OnConfigChanged?.Invoke();
            }),
        Check("Make a noise as well",
            () => _config.Sound,
            value =>
            {
                _config.Sound = value;
                Persist();
                OnConfigChanged?.Invoke();
            }),
        Check("Announce what was already broken at startup",
            () => _config.AnnounceBacklog,
            value =>
            {
                _config.AnnounceBacklog = value;
                Persist();
                OnConfigChanged?.Invoke();
            }),
        _startup,
        new NativeMenuItemSeparator(),
        Item("Show me what it looks like", () => OnDemonstrate?.Invoke()),
        _lastBuild,
        Item("Edit the taunts…", EditConfig),
        Item("Reload the file", () => OnReloadConfig?.Invoke()),
        new NativeMenuItemSeparator(),
        Item("Quit", () => OnQuit?.Invoke()),
    ];

    // ── the settings ──────────────────────────────────────────────────────────

    private NativeMenuItem Seconds(string header, double seconds) =>
        Choice(header, () => Math.Abs(_config.SecondsOnScreen - seconds) < 0.001, () =>
        {
            _config.SecondsOnScreen = seconds;
            Persist();
            OnConfigChanged?.Invoke();
        });

    private NativeMenuItem Shape(string header, bool full) =>
        Choice(header, () => _config.FullScreen == full, () =>
        {
            _config.FullScreen = full;
            Persist();
            OnConfigChanged?.Invoke();
        });

    private NativeMenuItem Area(string header, AreaChoice area) =>
        Choice(header, () => _config.Area == area, () =>
        {
            _config.Area = area;
            Persist();
            OnConfigChanged?.Invoke();
        });

    private NativeMenuItem Opacity(string header, double opacity) =>
        Choice(header, () => Math.Abs(_config.Opacity - opacity) < 0.001, () =>
        {
            _config.Opacity = opacity;
            Persist();
            OnConfigChanged?.Invoke();
        });

    // ── Menu plumbing ─────────────────────────────────────────────────────────
    // Each option asks the config what it should look like when the menu opens rather than
    // being ticked once at startup: the file is editable by hand and reloadable from this very
    // menu, so anything remembering its own state would start lying almost immediately.

    private static NativeMenuItem Item(string header, Action click)
    {
        var item = new NativeMenuItem { Header = header };
        item.Click += (_, _) => click();
        return item;
    }

    private static NativeMenuItem Submenu(string header, params NativeMenuItem[] items)
    {
        var menu = new NativeMenu();
        foreach (var item in items) menu.Add(item);

        void Retick()
        {
            foreach (var item in items)
                if (item.CommandParameter is Func<bool> isChosen)
                    item.IsChecked = isChosen();
        }

        // Twice, because neither moment is reliable on its own: picking an option has to move
        // the tick off the old one straight away, and opening the menu has to account for the
        // file having been edited behind its back.
        foreach (var item in items) item.Click += (_, _) => Retick();
        menu.Opening += (_, _) => Retick();

        return new NativeMenuItem { Header = header, Menu = menu };
    }

    private static NativeMenuItem Choice(string header, Func<bool> isChosen, Action choose)
    {
        var item = new NativeMenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.Radio,
            IsChecked = isChosen(),

            // Parked here rather than in a dictionary: the menu owns its items, and a second
            // collection to keep in step with it is a second thing to get wrong.
            CommandParameter = isChosen,
        };

        item.Click += (_, _) => choose();
        return item;
    }

    private static NativeMenuItem Check(string header, Func<bool> isOn, Action<bool> set)
    {
        var item = new NativeMenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = isOn(),
        };

        item.Click += (_, _) =>
        {
            set(!isOn());
            item.IsChecked = isOn();
        };

        return item;
    }

    private void Persist() => _config.Save();

    /// <summary>
    /// Open <c>blame.json</c> in whatever the machine opens JSON with.
    /// </summary>
    /// <remarks>
    /// The taunts are the part of this app people will actually want to change, and a list of
    /// sentences is not something to edit through a tray menu. The file is written out on
    /// first run with the general lines already in it, <c>{name}</c> and all, so opening it
    /// shows somebody the shape of what they are adding to rather than an empty object.
    /// </remarks>
    private void EditConfig()
    {
        try
        {
            if (!File.Exists(BlameConfig.DefaultPath)) _config.Save();

            Process.Start(new ProcessStartInfo(BlameConfig.DefaultPath) { UseShellExecute = true });
        }
        catch
        {
            // No editor associated with .json, or the shell refused. Not worth interrupting
            // anybody over — this app does quite enough of that already.
        }
    }
}
