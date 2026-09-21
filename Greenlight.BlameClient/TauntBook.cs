namespace Greenlight.BlameClient;

/// <summary>
/// The line printed under the name, and the only part of this app with an opinion.
/// </summary>
/// <remarks>
/// <para>
/// Everybody gets something; some people get something personal. The book is keyed on a
/// flattened name (see <see cref="Culprit.Key"/>), so an entry filed under <c>jamie</c> finds
/// <c>Jamie</c>, <c>JAMIE.DOE</c> and <c>jamie.doe@example.com</c> without anybody having to
/// write three keys. A first-name key matches every Jamie on the team, which is either
/// exactly what was wanted or an argument for using the full name — both are spelled the
/// same way in the file.
/// </para>
/// <para>
/// Nobody is singled out by default. The app ships with general lines only, and the personal
/// block is the team's to fill in — an insult with a name baked into it belongs in somebody's
/// own config, not in a public repository. The general lines can still be personal: a
/// <c>{name}</c> in any line is replaced with the first name off the snapshot, so "Red is your
/// colour, {name}" lands on whoever actually did it.
/// </para>
/// <para>
/// Lines rotate rather than being picked at random. Random is worse than it sounds: over a
/// fortnight it will serve the same line twice in a row at least once, and the second time
/// somebody sees an identical insult they stop reading the insults. Rotation also means the
/// line on screen is a pure function of how many times that person has broken the build,
/// which is the sort of thing a test can hold on to.
/// </para>
/// </remarks>
public sealed class TauntBook
{
    /// <summary>Where each person is up to in their own list.</summary>
    private readonly Dictionary<string, int> _cursors = new(StringComparer.Ordinal);

    private readonly Dictionary<string, IReadOnlyList<string>> _personal;
    private readonly IReadOnlyList<string> _general;

    /// <summary>
    /// Build a book from the config's lists. Empty lists fall back to the defaults, so that
    /// deleting a block in the JSON leaves you with an app that still says something rather
    /// than an app with a blank space under the name.
    /// </summary>
    public TauntBook(IDictionary<string, List<string>>? personal, IList<string>? general)
    {
        _personal = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (var (name, lines) in personal ?? new Dictionary<string, List<string>>())
        {
            var usable = (lines ?? []).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
            if (usable.Length == 0) continue;

            // Keyed on the flattened name so the lookup can be a plain dictionary hit rather
            // than a scan with fuzzy matching on every alert.
            _personal[Culprit.Key(name)] = usable;
        }

        var generals = (general ?? []).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        _general = generals.Length > 0 ? generals : DefaultGeneral;
    }

    /// <summary>
    /// What the app says when nobody has written anything for this person, which out of the
    /// box is everybody. <c>{name}</c> is filled in with the first name off the snapshot.
    /// </summary>
    /// <remarks>
    /// Ribbing rather than cruelty: these land on whoever broke the build, including the new
    /// starter on their second day, so they poke at the build and at the moment rather than at
    /// the person. Anybody who has earned worse can be given it in the personal lists.
    /// </remarks>
    public static IReadOnlyList<string> DefaultGeneral { get; } =
    [
        "The pipeline would like a word.",
        "Push, pray, repeat.",
        "It compiled on somebody's machine, presumably.",
        "Everyone has seen this. Everyone.",
        "This is now a team activity.",
        "{name}. Again. At this point it is less a mistake and more a lifestyle.",
        "Tested locally. On a different machine. In a different language. Last Tuesday.",
        "The build did not fail. It gave up.",
        "{name} has broken this pipeline so often it files them under weather.",
        "Somewhere a unit test is writing a strongly worded letter.",
        "Red is your colour, {name}. You wear it constantly.",
        "Git blame was not needed. Git blame has never been needed.",
        "It is not the breaking. It is the confidence.",
        "Try breaking it deliberately next time, {name}. You would probably miss.",
    ];

    /// <summary>
    /// The personal lists this app ships with: none. Nobody is picked on by default.
    /// </summary>
    /// <remarks>
    /// Still written out to the config file on first run, as an empty block, so that the
    /// answer to "can I add my own" is a file somebody can open, and the answer to "can you
    /// take mine out" does not involve a rebuild.
    /// </remarks>
    public static IReadOnlyDictionary<string, List<string>> DefaultPersonal { get; } =
        new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>What a line writes where the culprit's first name should go.</summary>
    public const string NamePlaceholder = "{name}";

    /// <summary>
    /// The next line for this person, with <c>{name}</c> filled in. Never throws, never returns
    /// null, never returns the same line twice running unless that person has exactly one line
    /// to their name.
    /// </summary>
    /// <param name="who">
    /// The name as it will be shown. Matched on the full name first and the first name second,
    /// so a book with an entry for "Jamie Doe" and another for "Jamie" gives the specific one
    /// to Doe and the general one to every other Jamie.
    /// </param>
    public string Next(string? who)
    {
        var lines = For(who, out var key);

        var cursor = _cursors.GetValueOrDefault(key);
        _cursors[key] = cursor + 1;

        // Modulo on the way out rather than on the way in, so the stored cursor is a count of
        // how many times this person has been named — which is worth having, and which a
        // wrapped index would have thrown away.
        var line = lines[cursor % lines.Count];

        // The first name, because that is the word in the headline: the line underneath should
        // be talking to the same person, not to "Jamie Doe" when the headline says JAMIE. With
        // no usable name this is Culprit.Nobody, so the line still reads as a sentence.
        return line.Replace(NamePlaceholder, Culprit.First(who), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether anybody has written something specifically for this person. The tray uses it to
    /// say so; nothing else cares.
    /// </summary>
    public bool KnowsAbout(string? who) => For(who, out _) != _general;

    private IReadOnlyList<string> For(string? who, out string key)
    {
        var full = Culprit.Key(who);

        if (full.Length > 0 && _personal.TryGetValue(full, out var exact))
        {
            key = full;
            return exact;
        }

        var first = Culprit.Key(Culprit.First(who));

        if (first.Length > 0 && _personal.TryGetValue(first, out var byFirstName))
        {
            key = first;
            return byFirstName;
        }

        // Everyone unnamed shares one cursor, on purpose: the general lines are a rotation
        // through the team's collective incompetence rather than a private tally, and a
        // per-stranger cursor would open with the same first line every single time.
        key = string.Empty;
        return _general;
    }
}
