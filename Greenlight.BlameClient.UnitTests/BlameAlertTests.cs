namespace Greenlight.BlameClient.UnitTests;

/// <summary>
/// How long the notice lasts and how it comes and goes. The only one of these that is
/// genuinely serious is that it finishes: a full-screen window that did not close is a
/// machine somebody has to restart, and they will be doing it in front of whoever they were
/// presenting to at the time.
/// </summary>
public class BlameAlertTests
{
    private static readonly Blame Broken = new(
        BuildId: 1,
        Who: "Jamie Doe",
        FirstName: "Jamie",
        Pipeline: "CI",
        Project: "Greenlight",
        Branch: "main",
        WebUrl: "https://example.com/build/1",
        BrokeAt: DateTimeOffset.UnixEpoch,
        AlsoBroken: 0);

    private static BlameAlert Alert(double seconds = 12) =>
        new(Broken, "Again, Jamie.", TimeSpan.FromSeconds(seconds));

    /// <summary>Frames small enough that the per-frame clamp never bites.</summary>
    private static void Run(BlameAlert alert, double seconds)
    {
        for (var i = 0; i < seconds * 20; i++) alert.Advance(TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public void Starts_invisible_and_arrives()
    {
        var alert = Alert();

        Assert.Equal(0, alert.Opacity, 3);

        Run(alert, 1);
        Assert.Equal(1, alert.Opacity, 3);
    }

    [Fact]
    public void Takes_itself_away_when_its_time_is_up()
    {
        var alert = Alert(seconds: 6);

        Run(alert, 5);
        Assert.False(alert.IsFinished);

        Run(alert, 2);
        Assert.True(alert.IsFinished);
    }

    [Fact]
    public void Fades_rather_than_vanishing_at_the_end()
    {
        var alert = Alert(seconds: 6);

        Run(alert, 5.8);

        Assert.InRange(alert.Opacity, 0.01, 0.99);
    }

    [Fact]
    public void A_dismissal_fades_it_out_rather_than_closing_it_instantly()
    {
        var alert = Alert();
        Run(alert, 2);

        alert.Dismiss();
        Assert.True(alert.IsDismissed);
        Assert.False(alert.IsFinished);

        Run(alert, 0.2);
        Assert.InRange(alert.Opacity, 0.01, 0.99);

        Run(alert, 1);
        Assert.True(alert.IsFinished);
    }

    /// <summary>
    /// Dismissing during the arrival should not make it brighten first — the fade runs down
    /// from wherever it had actually got to.
    /// </summary>
    [Fact]
    public void A_dismissal_mid_arrival_never_runs_the_fade_backwards()
    {
        var alert = Alert();
        Run(alert, 0.1);

        var wasAt = alert.Opacity;
        Assert.InRange(wasAt, 0.01, 0.99);

        alert.Dismiss();
        Run(alert, 0.05);

        Assert.True(alert.Opacity <= wasAt);
    }

    [Fact]
    public void Dismissing_twice_changes_nothing()
    {
        var alert = Alert();
        Run(alert, 2);

        alert.Dismiss();
        Run(alert, 0.2);
        var after = alert.Opacity;

        alert.Dismiss();
        Assert.Equal(after, alert.Opacity, 3);
    }

    /// <summary>
    /// A notice configured to last a quarter of a second would be a flash nobody could read
    /// and everybody would report as a fault.
    /// </summary>
    [Fact]
    public void A_silly_duration_is_floored_at_something_readable()
    {
        var alert = Alert(seconds: 0.1);

        Run(alert, 0.6);
        Assert.False(alert.IsFinished);
    }

    /// <summary>
    /// A laptop coming out of sleep hands you a single frame several minutes long. Without the
    /// clamp the notice would jump from fully lit to gone with no fade, which reads as the app
    /// having crashed.
    /// </summary>
    [Fact]
    public void A_machine_waking_up_does_not_skip_the_notice_straight_to_gone()
    {
        var alert = Alert();
        Run(alert, 1);

        alert.Advance(TimeSpan.FromMinutes(20));

        Assert.False(alert.IsFinished);
    }

    [Fact]
    public void The_headline_overshoots_and_settles()
    {
        var alert = Alert();

        alert.Advance(BlameAlert.SlamIn / 2);
        Assert.True(alert.Punch > 1);

        Run(alert, 1);
        Assert.Equal(1, alert.Punch, 3);
    }

    [Fact]
    public void Time_that_did_not_pass_is_ignored()
    {
        var alert = Alert();

        alert.Advance(TimeSpan.Zero);
        alert.Advance(TimeSpan.FromSeconds(-5));

        Assert.Equal(0, alert.Seconds, 3);
    }
}
