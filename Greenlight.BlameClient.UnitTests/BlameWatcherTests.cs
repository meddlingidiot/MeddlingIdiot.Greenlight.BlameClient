using Greenlight.Sdk.Protocol;

namespace Greenlight.BlameClient.UnitTests;

/// <summary>
/// The rules about when a name goes on the screen. Every one of these is a thing that would
/// be either invisible or mortifying in a screenshot: an alert that fires twice, an alert
/// about Friday, an alert naming the wrong person.
/// </summary>
public class BlameWatcherTests
{
    /// <summary>
    /// The backlog is swallowed by default, so most tests need a watcher that has already
    /// seen one snapshot and is therefore ready to treat the next one as news.
    /// </summary>
    private static BlameWatcher Warm(BlameWatcher? watcher = null)
    {
        var w = watcher ?? new BlameWatcher();
        w.Consider(Snapshots.Of(Snapshots.Passed(1)), Snapshots.Noon);
        return w;
    }

    [Fact]
    public void Announces_a_break_that_happens_while_watching()
    {
        var watcher = Warm();

        var blame = watcher.Consider(
            Snapshots.Of(Snapshots.Broken(2, "Jamie Doe")),
            Snapshots.Noon);

        Assert.NotNull(blame);
        Assert.Equal("Jamie Doe", blame.Who);
        Assert.Equal("Jamie", blame.FirstName);
        Assert.Equal(2, blame.BuildId);
    }

    [Fact]
    public void Says_nothing_when_everything_passes()
    {
        var watcher = Warm();

        Assert.Null(watcher.Consider(Snapshots.Of(Snapshots.Passed(2)), Snapshots.Noon));
    }

    /// <summary>
    /// The one that matters most. Greenlight sends a snapshot on every change — a build
    /// starting, a pull request being opened, a poll finding nothing — and the broken build
    /// stays in every one of them until somebody fixes or dismisses it.
    /// </summary>
    [Fact]
    public void Announces_one_break_once_however_many_snapshots_carry_it()
    {
        var watcher = Warm();
        var snapshot = Snapshots.Of(Snapshots.Broken(2));

        Assert.NotNull(watcher.Consider(snapshot, Snapshots.Noon));
        Assert.Null(watcher.Consider(snapshot, Snapshots.Noon));
        Assert.Null(watcher.Consider(snapshot, Snapshots.Noon));
    }

    [Fact]
    public void Says_nothing_about_what_was_already_broken_when_it_started()
    {
        var watcher = new BlameWatcher();

        Assert.Null(watcher.Consider(Snapshots.Of(Snapshots.Broken(1)), Snapshots.Noon));
    }

    [Fact]
    public void Announces_the_backlog_when_asked_to()
    {
        var watcher = new BlameWatcher { AnnounceBacklog = true };

        Assert.NotNull(watcher.Consider(Snapshots.Of(Snapshots.Broken(1)), Snapshots.Noon));
    }

    /// <summary>
    /// A first snapshot with nothing wrong in it still counts as the first. Otherwise the
    /// genuine break in the second snapshot would be swallowed as though it were history.
    /// </summary>
    [Fact]
    public void A_quiet_first_snapshot_still_counts_as_the_first()
    {
        var watcher = new BlameWatcher();

        watcher.Consider(Snapshots.Of(Snapshots.Passed(1)), Snapshots.Noon);

        Assert.NotNull(watcher.Consider(Snapshots.Of(Snapshots.Broken(2)), Snapshots.Noon));
    }

    [Fact]
    public void Ignores_a_failure_that_finished_hours_ago()
    {
        var watcher = Warm();

        var blame = watcher.Consider(
            Snapshots.Of(Snapshots.Broken(2, finishedAt: Snapshots.Noon.AddHours(-3))),
            Snapshots.Noon);

        Assert.Null(blame);
    }

    /// <summary>
    /// And having ignored it, does not change its mind about it later — an unrelated pipeline
    /// reporting in should not drag a stale failure back into the news.
    /// </summary>
    [Fact]
    public void Never_reconsiders_a_failure_it_has_already_swallowed()
    {
        var watcher = Warm();
        var stale = Snapshots.Broken(2, finishedAt: Snapshots.Noon.AddHours(-3));

        watcher.Consider(Snapshots.Of(stale), Snapshots.Noon);

        Assert.Null(watcher.Consider(Snapshots.Of(stale, Snapshots.Passed(3)), Snapshots.Noon));
    }

    [Fact]
    public void Ignores_a_failure_somebody_has_already_dismissed_in_Greenlight()
    {
        var watcher = Warm();

        var blame = watcher.Consider(
            Snapshots.Of(Snapshots.Broken(2, acknowledged: true)),
            Snapshots.Noon);

        Assert.Null(blame);
    }

    [Fact]
    public void Ignores_a_build_that_is_still_running()
    {
        var watcher = Warm();

        Assert.Null(watcher.Consider(Snapshots.Of(Snapshots.Running(2)), Snapshots.Noon));
    }

    /// <summary>
    /// A non-blocking task failing is somebody's known problem, not a public accusation.
    /// </summary>
    [Fact]
    public void Ignores_a_partial_success()
    {
        var watcher = Warm();
        var partial = Snapshots.Broken(2) with { Result = GreenlightBuildResult.PartiallySucceeded };

        Assert.Null(watcher.Consider(Snapshots.Of(partial), Snapshots.Noon));
    }

    [Fact]
    public void Names_whoever_broke_it_most_recently()
    {
        var watcher = Warm();

        var blame = watcher.Consider(
            Snapshots.Of(
                Snapshots.Broken(2, "Priya Raman", Snapshots.Noon.AddMinutes(-20), "Nightly"),
                Snapshots.Broken(3, "Jamie Doe", Snapshots.Noon, "CI")),
            Snapshots.Noon);

        Assert.NotNull(blame);
        Assert.Equal("Jamie Doe", blame.Who);
        Assert.Equal("CI", blame.Pipeline);
    }

    [Fact]
    public void Counts_the_other_pipelines_that_went_down_with_it()
    {
        var watcher = Warm();

        var blame = watcher.Consider(
            Snapshots.Of(
                Snapshots.Broken(2, "Jamie", Snapshots.Noon, "CI"),
                Snapshots.Broken(3, "Jamie", Snapshots.Noon.AddMinutes(-1), "Nightly"),
                Snapshots.Broken(4, "Jamie", Snapshots.Noon.AddMinutes(-2), "Packaging")),
            Snapshots.Noon);

        Assert.NotNull(blame);
        Assert.Equal(2, blame.AlsoBroken);
        Assert.Equal("and took 2 more pipelines with it", blame.AndTheRest);
    }

    /// <summary>
    /// And having counted them, does not then announce them one at a time as later snapshots
    /// arrive — which is the failure mode that would put four full-screen notices on somebody's
    /// desk over one bad merge.
    /// </summary>
    [Fact]
    public void Does_not_announce_the_others_separately_afterwards()
    {
        var watcher = Warm();
        var snapshot = Snapshots.Of(
            Snapshots.Broken(2, pipeline: "CI"),
            Snapshots.Broken(3, pipeline: "Nightly"));

        Assert.NotNull(watcher.Consider(snapshot, Snapshots.Noon));
        Assert.Null(watcher.Consider(snapshot, Snapshots.Noon));
    }

    /// <summary>
    /// Fixed and broken again is two events, and deserves two notices. This is the reason the
    /// watcher drops ids as they stop being breaks rather than remembering them forever.
    /// </summary>
    [Fact]
    public void Announces_the_same_pipeline_again_once_it_has_been_fixed_and_rebroken()
    {
        var watcher = Warm();

        Assert.NotNull(watcher.Consider(Snapshots.Of(Snapshots.Broken(2)), Snapshots.Noon));

        // The provider reports the same id as passing — a rerun of the same build, which is
        // exactly what somebody pressing "retry" produces.
        watcher.Consider(Snapshots.Of(Snapshots.Passed(2)), Snapshots.Noon);

        Assert.NotNull(watcher.Consider(Snapshots.Of(Snapshots.Broken(2)), Snapshots.Noon));
    }

    [Fact]
    public void Announcing_stops_after_somebody_dismisses_it_in_Greenlight()
    {
        var watcher = Warm();

        Assert.NotNull(watcher.Consider(Snapshots.Of(Snapshots.Broken(2)), Snapshots.Noon));

        // Dismissed, then reported again still dismissed. The id has left the announced set,
        // so the only thing keeping it quiet is the acknowledgement itself.
        var acknowledged = Snapshots.Of(Snapshots.Broken(2, acknowledged: true));

        Assert.Null(watcher.Consider(acknowledged, Snapshots.Noon));
        Assert.Null(watcher.Consider(acknowledged, Snapshots.Noon));
    }

    /// <summary>
    /// Greenlight restarting under an update sends a fresh first snapshot carrying everything.
    /// Treating that as news would mean an update to an unrelated app throwing accusations
    /// across the screen.
    /// </summary>
    [Fact]
    public void Treats_the_snapshot_after_a_reconnection_as_a_backlog()
    {
        var watcher = Warm();

        Assert.NotNull(watcher.Consider(Snapshots.Of(Snapshots.Broken(2)), Snapshots.Noon));

        watcher.Forget();

        Assert.Null(watcher.Consider(Snapshots.Of(Snapshots.Broken(2)), Snapshots.Noon));
    }

    [Fact]
    public void Carries_enough_to_go_and_look_at_the_build()
    {
        var watcher = Warm();

        var blame = watcher.Consider(Snapshots.Of(Snapshots.Broken(7)), Snapshots.Noon);

        Assert.NotNull(blame);
        Assert.Equal("https://example.com/build/7", blame.WebUrl);
        Assert.Equal("Greenlight", blame.Project);
        Assert.Equal("CI on main", blame.What);
        Assert.Null(blame.AndTheRest);
    }

    /// <summary>
    /// A completed build with no finish time is a provider being odd. Falling back to the
    /// queued time rather than to default(DateTimeOffset) is what stops it reading as half a
    /// century stale and being silently swallowed.
    /// </summary>
    [Fact]
    public void Falls_back_to_the_queued_time_when_a_build_has_no_finish_time()
    {
        var watcher = Warm();
        var odd = Snapshots.Broken(2) with { FinishedAt = null, QueuedAt = Snapshots.Noon.AddMinutes(-2) };

        var blame = watcher.Consider(Snapshots.Of(odd), Snapshots.Noon);

        Assert.NotNull(blame);
        Assert.Equal(Snapshots.Noon.AddMinutes(-2), blame.BrokeAt);
    }
}
