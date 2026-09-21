using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Greenlight.Sdk;
using Greenlight.Sdk.Protocol;

namespace Greenlight.BlameClient;

/// <summary>
/// The whole of the Greenlight integration, which is the point of the sample: attach, ask
/// <see cref="BlameWatcher"/> whether anybody has just broken something, and put their name on
/// the screen if they have.
/// </summary>
/// <remarks>
/// The window, the drawing and the tray icon are ordinary Avalonia and have nothing to do with
/// Greenlight — the integration is still the twenty-odd lines in
/// <see cref="StartWatchingGreenlight"/>.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class App : Application
{
    private GreenlightClient? _greenlight;
    private BlameWindow? _window;
    private BlameTray? _tray;
    private BlameConfig _config = new();
    private BlameWatcher _watcher = new();
    private TauntBook _taunts = new(null, null);

    /// <summary>Whether a breakage would raise a notice. Off is the tray's mute.</summary>
    private bool _watching = true;

    /// <summary>Whether there is a Greenlight attached, for the tray's tooltip.</summary>
    private bool _attached;

    /// <summary>Whether anything is broken right now, for the same.</summary>
    private bool _broken;

    /// <summary>
    /// The last person named, and what they broke. Kept so the tray can offer to open it after
    /// the notice has gone — which is when somebody actually wants it, because while it is up
    /// they are reading it.
    /// </summary>
    private Blame? _last;

    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // This app has no window for almost all of its life. On the default setting the
            // process would exit the moment the first notice faded out, taking the tray icon
            // and the whole point of it with it.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _config = BlameConfig.Load();
            ApplyConfig();

            // If Windows is set to start this, make sure it is still pointed at the right
            // executable. An update moves the versioned copy out from under an older
            // registration, and the symptom — an app that watches nothing and says nothing —
            // is one nobody would notice until the morning somebody needed it.
            WindowsStartup.Refresh();

            _tray = new BlameTray(_config)
            {
                IsWatching = () => _watching,
                OnSetWatching = watching =>
                {
                    _watching = watching;

                    // Muting takes down whatever is on screen. Somebody reaching for the tray
                    // while a notice is up is asking for it to stop now, not next time.
                    if (!watching) _window?.Alert.Dismiss();

                    ShowState();
                },
                OnConfigChanged = ApplyConfig,
                OnReloadConfig = ReloadConfig,
                OnDemonstrate = Demonstrate,
                OnOpenLastBuild = OpenLastBuild,
                OnQuit = () => desktop.Shutdown(),
            };

            ShowState();
            StartWatchingGreenlight();

            // --demo puts one up straight away. For the screenshots, for anybody who has just
            // cloned this and would like to know what it does before wiring it to a real
            // Greenlight, and for checking a taunt reads the way it did in your head.
            if (desktop.Args?.Contains("--demo", StringComparer.OrdinalIgnoreCase) is true) Demonstrate();

            desktop.Exit += async (_, _) =>
            {
                _tray?.Dispose();
                if (_greenlight is not null) await _greenlight.DisposeAsync();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void StartWatchingGreenlight()
    {
        _greenlight = new GreenlightClient();

        // Both of these arrive on a background thread — the SDK says so, loudly, and this is
        // what it means in practice. Opening a window from the pipe's thread would throw, and
        // it would throw at the exact moment somebody broke a build, which is the one moment
        // this app has to work.
        _greenlight.Changed += (_, e) => Consider(e.Snapshot);

        _greenlight.AvailabilityChanged += (_, e) =>
        {
            var attached = e.Availability == GreenlightAvailability.Connected;

            // Everything is forgotten when Greenlight goes away, so the snapshot that arrives
            // when it comes back is read as a backlog rather than as news. Greenlight
            // restarting under a Velopack update is not the entire team breaking the build at
            // 3am.
            if (!attached) _watcher.Forget();

            Dispatcher.UIThread.Post(() =>
            {
                _attached = attached;
                if (!attached) _broken = false;
                ShowState();
            });
        };

        // Deliberately not awaited and deliberately not guarded: StartAsync returns as soon as
        // the background loop is running, and an absent Greenlight is not an error. This sits
        // quietly in the tray until one turns up.
        _ = _greenlight.StartAsync();
    }

    /// <summary>
    /// A snapshot arrived. Everything that decides anything is in <see cref="BlameWatcher"/>;
    /// this is the part that owns a window.
    /// </summary>
    private void Consider(GreenlightSnapshot snapshot)
    {
        var blame = _watcher.Consider(snapshot, DateTimeOffset.Now);
        var broken = snapshot.Status == GreenlightStatus.Red;

        Dispatcher.UIThread.Post(() =>
        {
            _attached = true;
            _broken = broken;

            // Asked after the snapshot has been considered rather than before, so that a
            // breakage which happened while muted is remembered as seen and does not ambush
            // somebody the moment they unmute.
            if (blame is not null && _watching) Announce(blame);
            else if (blame is not null) _last = blame;

            ShowState();
        });
    }

    /// <summary>
    /// Put a notice on the screen. UI thread only.
    /// </summary>
    /// <remarks>
    /// One at a time, and the newest wins. Two people breaking two pipelines inside twelve
    /// seconds is rare, and a queue of full-screen notices would be a machine somebody has to
    /// wait out — so the one already up is cut short and replaced, which at least reads as the
    /// news having moved on.
    /// </remarks>
    private void Announce(Blame blame)
    {
        _last = blame;

        _window?.Close();
        _window = null;

        var alert = new BlameAlert(blame, _taunts.Next(blame.Who), _config.OnScreen());

        _window = new BlameWindow(_config, alert);
        _window.Done += (_, _) =>
        {
            _window = null;
            ShowState();
        };

        _window.Show();
    }

    /// <summary>
    /// A made-up breakage, for the tray's "show me what it looks like".
    /// </summary>
    /// <remarks>
    /// Names whoever is logged in, run through the same tidying a provider's name gets. There
    /// is no snapshot to take a name from, nobody else's name belongs in a demonstration, and
    /// the person who clicked "show me" is the one person guaranteed to find it funny rather
    /// than finding it on somebody else's screen.
    /// </remarks>
    private void Demonstrate() =>
        Dispatcher.UIThread.Post(() => Announce(new Blame(
            BuildId: -1,
            Who: Culprit.Full(Environment.UserName),
            FirstName: Culprit.First(Environment.UserName),
            Pipeline: "Nightly integration",
            Project: "Demonstration",
            Branch: "main",
            WebUrl: string.Empty,
            BrokeAt: DateTimeOffset.Now,
            AlsoBroken: 0)));

    private void OpenLastBuild()
    {
        if (_last is null) return;

        // Through the window when one is up, so that opening the build also starts the fade:
        // somebody who has gone to look at it has finished with the notice.
        if (_window is not null) _window.OpenBuild();
        else Browse.Open(_last.WebUrl);
    }

    /// <summary>
    /// Take up the settings. The watcher is rebuilt rather than adjusted — its two knobs are
    /// <c>init</c>-only, because a watcher whose rules changed halfway through a snapshot
    /// would be the hardest possible thing to reason about.
    /// </summary>
    /// <remarks>
    /// Deliberately keeps no state across the rebuild. Changing a setting while a pipeline is
    /// broken means the next snapshot is read as a first one and its backlog is swallowed,
    /// which is the right answer: somebody who has just turned a setting on is looking at the
    /// tray, not waiting to be told about a build they already know about.
    /// </remarks>
    private void ApplyConfig()
    {
        _taunts = _config.Book();

        _watcher = new BlameWatcher
        {
            AnnounceBacklog = _config.AnnounceBacklog,
            StaleAfter = TimeSpan.FromMinutes(Math.Clamp(_config.StaleAfterMinutes, 1, 1440)),
        };
    }

    /// <summary>Re-read the file, for taunts added by hand while this was running.</summary>
    private void ReloadConfig()
    {
        _config.CopyFrom(BlameConfig.Load());
        ApplyConfig();
        ShowState();
    }

    private void ShowState() => _tray?.ShowState(_attached, _broken, _last?.FirstName);
}
