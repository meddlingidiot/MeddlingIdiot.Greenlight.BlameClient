using Greenlight.Sdk.Protocol;

namespace Greenlight.BlameClient;

/// <summary>
/// One person, one broken pipeline, one notice. Everything the alert needs to put itself on
/// the screen, worked out before any window exists.
/// </summary>
/// <param name="BuildId">The provider's build id. What stops the same break being announced twice.</param>
/// <param name="Who">The name as it will be read out loud — see <see cref="Culprit.Full"/>.</param>
/// <param name="FirstName">The headline word. "Jamie", off the front of <paramref name="Who"/>.</param>
/// <param name="Pipeline">The pipeline that broke.</param>
/// <param name="Project">The project it lives in.</param>
/// <param name="Branch">The branch it was building.</param>
/// <param name="WebUrl">Where to go and look, if the provider offered anywhere.</param>
/// <param name="BrokeAt">When it finished failing.</param>
/// <param name="AlsoBroken">
/// How many <em>other</em> pipelines broke in the same breath. Usually zero; a bad merge to
/// main can make it four, and naming one pipeline out of four would read as the app having
/// missed the other three.
/// </param>
public sealed record Blame(
    long BuildId,
    string Who,
    string FirstName,
    string Pipeline,
    string Project,
    string Branch,
    string WebUrl,
    DateTimeOffset BrokeAt,
    int AlsoBroken)
{
    /// <summary>The line under the headline: what exactly it was that they broke.</summary>
    public string What => string.IsNullOrWhiteSpace(Branch) ? Pipeline : $"{Pipeline} on {Branch}";

    /// <summary>
    /// The rest of the damage, when there is any. Null when this was a tidy, single-pipeline
    /// disgrace.
    /// </summary>
    public string? AndTheRest => AlsoBroken switch
    {
        <= 0 => null,
        1 => "and took one more pipeline with it",
        _ => $"and took {AlsoBroken} more pipelines with it",
    };
}

/// <summary>
/// Watches snapshots go past and decides, for each one, whether anybody has just broken
/// something — and if so, who.
/// </summary>
/// <remarks>
/// <para>
/// Every judgement this app makes is in here, and none of it touches Avalonia, a window, a
/// clock or a file. That is on purpose: "fires once per break", "does not shout about
/// Friday's failure on Monday morning" and "announces a pipeline that was fixed and broken
/// again" are all statements somebody can only really check in a test.
/// </para>
/// <para>
/// Not thread-safe, and does not need to be. The SDK raises <c>Changed</c> on a background
/// thread, but it raises it on <em>one</em> background thread at a time, and
/// <see cref="Consider"/> is called from nowhere else.
/// </para>
/// </remarks>
public sealed class BlameWatcher
{
    /// <summary>
    /// Builds already announced, or deliberately swallowed. Ids rather than a high-water
    /// timestamp: providers backfill, an agent's clock and a desk's clock disagree, and
    /// "newer than the last one I saw" would miss a build that finished out of order.
    /// </summary>
    private readonly HashSet<long> _announced = [];

    private bool _seenAnything;

    /// <summary>
    /// Whether the first snapshot after attaching is allowed to raise an alert.
    /// </summary>
    /// <remarks>
    /// Off, and that setting is most of what makes this app bearable. The first snapshot
    /// carries the whole retention window, so a machine starting on Monday would otherwise
    /// open with a full-screen accusation about a pipeline that broke on Friday afternoon and
    /// was fixed before the pub. Off, that backlog is read once, remembered, and never
    /// mentioned — only something that breaks while you are watching gets the screen.
    /// </remarks>
    public bool AnnounceBacklog { get; init; }

    /// <summary>
    /// Ignore builds that finished longer ago than this, even in a live snapshot. Belt and
    /// braces for a Greenlight that reconnects after an outage and re-sends an hour of
    /// history as though it were news.
    /// </summary>
    public TimeSpan StaleAfter { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Forget everything. Called when Greenlight goes away, so the snapshot that arrives when
    /// it comes back is treated as a fresh backlog rather than as a burst of breaking news —
    /// Greenlight restarting under an update is not four people breaking four pipelines.
    /// </summary>
    public void Forget()
    {
        _announced.Clear();
        _seenAnything = false;
    }

    /// <summary>
    /// Look at a snapshot. Returns the break worth announcing, or null — which is the answer
    /// almost every time, because almost every snapshot is somebody's build going green.
    /// </summary>
    /// <param name="snapshot">What Greenlight just said.</param>
    /// <param name="now">
    /// The current time, passed in rather than read off the clock, so that
    /// <see cref="StaleAfter"/> can be tested without waiting half an hour.
    /// </param>
    public Blame? Consider(GreenlightSnapshot snapshot, DateTimeOffset now)
    {
        var broken = snapshot.Builds
            .Where(IsABreak)
            .Where(build => !_announced.Contains(build.Id))
            .OrderByDescending(When)
            .ToList();

        // Taken before anything returns: a first snapshot with nothing broken in it still has
        // to count as having been seen, or the next one would be treated as the first and
        // swallow a genuine break.
        var first = !_seenAnything;
        _seenAnything = true;

        // Whatever happens below, none of these get announced again. Marked even when they are
        // about to be swallowed, because a build held back for being stale would otherwise
        // become news the moment an unrelated pipeline reported in.
        foreach (var build in broken) _announced.Add(build.Id);

        // Runs that are no longer a break — fixed, acknowledged, rebuilding — drop out of the
        // set as they go past, so a pipeline broken, fixed, and broken again by the same
        // person announces itself twice. Which is what it deserves.
        foreach (var build in snapshot.Builds)
            if (!IsABreak(build))
                _announced.Remove(build.Id);

        if (broken.Count == 0) return null;
        if (first && !AnnounceBacklog) return null;

        var culprit = broken[0];
        if (now - When(culprit) > StaleAfter) return null;

        return new Blame(
            culprit.Id,
            Culprit.Full(culprit.TriggeredBy),
            Culprit.First(culprit.TriggeredBy),
            culprit.Pipeline,
            culprit.Project,
            culprit.Branch,
            culprit.WebUrl,
            When(culprit),
            broken.Count - 1);
    }

    /// <summary>
    /// A finished run that failed and that nobody has waved away yet.
    /// </summary>
    /// <remarks>
    /// <see cref="GreenlightBuildResult.PartiallySucceeded"/> is deliberately not a break. It
    /// is a non-blocking task somebody has already decided they can live with, and an app that
    /// threw a colleague's name across the screen over a flaky optional step would be
    /// uninstalled by the end of the first afternoon.
    /// </remarks>
    private static bool IsABreak(GreenlightBuild build) =>
        build is
        {
            Status: GreenlightBuildStatus.Completed,
            Result: GreenlightBuildResult.Failed,
            IsAcknowledged: false,
        };

    /// <summary>
    /// When a run counts as having happened. Queued time is a fallback rather than a default:
    /// a completed build with no finish time is a provider being odd, not a build that
    /// happened at the epoch and is therefore half a century stale.
    /// </summary>
    private static DateTimeOffset When(GreenlightBuild build) => build.FinishedAt ?? build.QueuedAt;
}
