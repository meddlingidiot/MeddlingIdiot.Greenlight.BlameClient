using System.Text.Json;
using System.Text.Json.Serialization;

namespace Greenlight.BlameClient;

/// <summary>
/// The alert, read from a JSON file the user can edit. Written out with the defaults the
/// first time it is missing — including the taunts — so that "where do I add a line for
/// Priya" has an answer that does not involve rebuilding anything.
/// </summary>
/// <remarks>
/// Kept in AppData rather than beside the executable: the executable lives under <c>bin</c>,
/// which a rebuild is entitled to delete, and a team's accumulated insults are not something
/// to lose to <c>dotnet clean</c>.
/// </remarks>
public sealed class BlameConfig
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Greenlight.Blame", "blame.json");

    /// <summary>
    /// How long the notice stays up before it takes itself away.
    /// </summary>
    /// <remarks>
    /// Twelve seconds. Long enough that somebody coming back with a coffee still catches it,
    /// short enough that it cannot hold a screen share hostage. A notice that waited for a
    /// click would be a notice that covers a locked machine all night.
    /// </remarks>
    public double SecondsOnScreen { get; set; } = 12;

    /// <summary>
    /// Whether the alert covers everything, or only sits along the top of the screen.
    /// </summary>
    /// <remarks>
    /// Full screen by default, because that is what was asked for and it is the only version
    /// that works from the other side of a room. The banner is for people who have discovered
    /// what the full-screen one does during a demonstration to a client.
    /// </remarks>
    public bool FullScreen { get; set; } = true;

    /// <summary>Which screen it goes on. The whole virtual desktop by default — every monitor.</summary>
    public AreaChoice Area { get; set; } = AreaChoice.FullScreen;

    /// <summary>Overall opacity. Below about 0.6 the thing underneath starts winning.</summary>
    public double Opacity { get; set; } = 0.94;

    /// <summary>
    /// Whether the red hazard stripes crawl and the headline pulses, or whether it all just
    /// sits there.
    /// </summary>
    /// <remarks>
    /// On. The movement is what makes it register out of the corner of an eye, which is the
    /// entire job; off is for the one person on every team for whom this sort of thing is not
    /// a joke but a headache.
    /// </remarks>
    public bool Animate { get; set; } = true;

    /// <summary>
    /// Play the system's alert sound when the notice appears.
    /// </summary>
    /// <remarks>
    /// Off by default. In an open-plan office a noise is a much bigger act than a picture, and
    /// it should be somebody's decision rather than something they discover at 9:40 on a
    /// Monday.
    /// </remarks>
    public bool Sound { get; set; }

    /// <summary>
    /// Announce whatever was already broken when this app started.
    /// </summary>
    /// <remarks>
    /// Off. See <see cref="BlameWatcher.AnnounceBacklog"/> — on, every boot opens with an
    /// accusation about something that may well have been fixed on Friday.
    /// </remarks>
    public bool AnnounceBacklog { get; set; }

    /// <summary>
    /// Ignore a failure that finished longer ago than this many minutes, even when it arrives
    /// in a live snapshot.
    /// </summary>
    public double StaleAfterMinutes { get; set; } = 30;

    /// <summary>
    /// Lines for particular people, keyed by name. Matched loosely — case, dots and an email
    /// domain make no difference — and matched on the first name when the full name has no
    /// entry of its own.
    /// </summary>
    public Dictionary<string, List<string>> Taunts { get; set; } =
        TauntBook.DefaultPersonal.ToDictionary(
            entry => entry.Key,
            entry => new List<string>(entry.Value),
            StringComparer.OrdinalIgnoreCase);

    /// <summary>Lines for everybody else. Kept mild, because they have not earned worse.</summary>
    public List<string> GeneralTaunts { get; set; } = [.. TauntBook.DefaultGeneral];

    /// <summary>The taunt book these settings describe. Rebuilt whenever the file is reloaded.</summary>
    public TauntBook Book() => new(Taunts, GeneralTaunts);

    /// <summary>How long the notice stays up, clamped into something survivable.</summary>
    public TimeSpan OnScreen() => TimeSpan.FromSeconds(Math.Clamp(SecondsOnScreen, 2, 120));

    /// <summary>
    /// Load the file, writing the defaults out first if it is not there. A file that cannot be
    /// read or parsed falls back to the defaults rather than refusing to start: a stray comma
    /// should not cost anybody the whole app.
    /// </summary>
    public static BlameConfig Load(string? path = null)
    {
        var file = path ?? DefaultPath;

        try
        {
            if (!File.Exists(file))
            {
                var fresh = new BlameConfig();
                fresh.Save(file);
                return fresh;
            }

            var loaded = JsonSerializer.Deserialize<BlameConfig>(File.ReadAllText(file), Json);
            if (loaded is null) return new BlameConfig();

            // A file written before one of these existed deserializes it as null, and so does a
            // hand edit that deleted a block. Neither should be a crash on the next failure —
            // which, being the next failure, is the worst possible moment to find out.
            var defaults = new BlameConfig();
            loaded.Taunts ??= defaults.Taunts;
            loaded.GeneralTaunts ??= defaults.GeneralTaunts;

            loaded.Opacity = Math.Clamp(loaded.Opacity, 0.3, 1.0);
            loaded.StaleAfterMinutes = Math.Clamp(loaded.StaleAfterMinutes, 1, 1440);

            return loaded;
        }
        catch
        {
            return new BlameConfig();
        }
    }

    public void Save(string? path = null)
    {
        var file = path ?? DefaultPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, JsonSerializer.Serialize(this, Json));
        }
        catch
        {
            // An app that cannot write its config still runs perfectly well on the defaults.
        }
    }

    /// <summary>Take on everything from a freshly-read file, in place.</summary>
    /// <remarks>
    /// Copied into this instance rather than swapping it for the new one: the tray is holding
    /// this object, and it is the tray's menu that has to keep agreeing with the file.
    /// </remarks>
    public void CopyFrom(BlameConfig other)
    {
        SecondsOnScreen = other.SecondsOnScreen;
        FullScreen = other.FullScreen;
        Area = other.Area;
        Opacity = other.Opacity;
        Animate = other.Animate;
        Sound = other.Sound;
        AnnounceBacklog = other.AnnounceBacklog;
        StaleAfterMinutes = other.StaleAfterMinutes;
        Taunts = other.Taunts;
        GeneralTaunts = other.GeneralTaunts;
    }
}
