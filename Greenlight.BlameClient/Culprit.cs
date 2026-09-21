namespace Greenlight.BlameClient;

/// <summary>
/// Turns whatever a build provider put in <c>TriggeredBy</c> into something you can shout
/// across a room.
/// </summary>
/// <remarks>
/// <para>
/// The field is free text as far as this app is concerned. Azure DevOps sends a display name,
/// mostly — but it also sends <c>jamie.doe@example.com</c>, <c>Jamie Doe
/// &lt;jamie@example.com&gt;</c>, <c>JAMIE.DOE</c>, <c>Microsoft.VisualStudio.Services.TFS</c>
/// and, when a pipeline is triggered by another pipeline, nothing at all. Every one of those
/// has to come out the other side as a headline.
/// </para>
/// <para>
/// All of this is guesswork, and it is deliberately gentle guesswork: the failure mode of
/// getting too clever here is an alert accusing a named colleague of something a service
/// account did, projected across a monitor in an open-plan office. When the shape is not one
/// of the ones below, the name is left exactly as it arrived.
/// </para>
/// </remarks>
public static class Culprit
{
    /// <summary>
    /// What the headline says when the provider gave us nothing usable.
    /// </summary>
    /// <remarks>
    /// Not "Unknown", which reads like a bug in this app rather than a gap in the data, and
    /// not a blank, which would leave "broke the build!" floating on its own looking like a
    /// rendering fault.
    /// </remarks>
    public const string Nobody = "Somebody";

    /// <summary>
    /// The full name, tidied: "jamie.doe@example.com" becomes "Jamie Doe".
    /// </summary>
    public static string Full(string? triggeredBy)
    {
        var name = (triggeredBy ?? string.Empty).Trim();
        if (name.Length == 0) return Nobody;

        // "Jamie Doe <jamie@example.com>" — the display name is already the good bit, so
        // throw the address away before anything else looks at it.
        var angle = name.IndexOf('<');
        if (angle > 0) name = name[..angle].Trim();

        // A bare address, but only when there is no display name in front of it. Somebody
        // called "Anne O'Brien-Shaw" has punctuation too, and splitting her up would be worse
        // than leaving an address alone.
        var at = name.IndexOf('@');
        if (at > 0 && !name.Contains(' ')) name = name[..at];

        // Nothing but punctuation is not a name, however much of it there is. It would also be
        // invisible to Culprit.Key, so a taunt could never be written for it — and drawing a
        // headline that reads "@ BROKE THE BUILD" would look like this app had fallen over.
        if (!name.Any(char.IsLetterOrDigit)) return Nobody;

        // A space means somebody typed this as a name, and it is already spelled the way they
        // spell it. "Joris van der Berg" and "bell hooks" are not mistakes to be corrected —
        // the only display name touched at all is one shouting, which is a directory export
        // rather than a person.
        if (name.Contains(' '))
        {
            var typed = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (typed.Length == 0) return Nobody;

            return typed.Any(word => word.Any(char.IsLower))
                ? string.Join(' ', typed)
                : string.Join(' ', typed.Select(Capitalise));
        }

        // No space, so this is a handle rather than a name, and its separators are standing in
        // for the spaces: "jamie.doe" and "jamie_doe" both want taking apart.
        var words = name
            .Replace('.', ' ').Replace('_', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (words.Length == 0) return Nobody;

        return string.Join(' ', words.Select(Capitalise));
    }

    /// <summary>
    /// The first name, which is what goes in the headline. "Jamie Doe" is a sentence;
    /// "Jamie" is an accusation.
    /// </summary>
    public static string First(string? triggeredBy)
    {
        var full = Full(triggeredBy);
        var space = full.IndexOf(' ');
        return space > 0 ? full[..space] : full;
    }

    /// <summary>
    /// A name flattened for comparison: lower case, letters and digits only. What the taunt
    /// book's keys are matched on, so that <c>jamie</c> in the config finds
    /// <c>Jamie.Doe@example.com</c> without anybody having to think about it.
    /// </summary>
    public static string Key(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        return string.Concat(name.Where(char.IsLetterOrDigit)).ToLowerInvariant();
    }

    /// <summary>
    /// Only the first letter, and only when the word looks like it was typed by a machine.
    /// </summary>
    /// <remarks>
    /// "doe" wants fixing. "McKenzie", "O'Brien" and "van der Berg" emphatically do not
    /// — a title-case pass that lower-cased the rest of the word would turn a colleague's name
    /// into "Mckenzie" on a screen the whole team is looking at.
    /// </remarks>
    private static string Capitalise(string word)
    {
        if (word.Length == 0) return word;

        var hasUpper = word.Any(char.IsUpper);
        var hasLower = word.Any(char.IsLower);

        // Mixed case is somebody's actual name, spelled the way they spell it. Leave it.
        if (hasUpper && hasLower) return word;

        // ALL CAPS is a directory export shouting, not a person shouting.
        if (hasUpper && !hasLower) return char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();

        return char.ToUpperInvariant(word[0]) + word[1..];
    }
}
