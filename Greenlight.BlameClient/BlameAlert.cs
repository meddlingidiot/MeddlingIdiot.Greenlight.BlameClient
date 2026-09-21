namespace Greenlight.BlameClient;

/// <summary>
/// One notice, from the moment it slams onto the screen to the moment it lets go of it.
/// </summary>
/// <remarks>
/// <para>
/// Holds what is being said and how far through saying it we are. No Avalonia, no clock, no
/// window: it is advanced by elapsed time from outside, which is how the awkward parts —
/// "a dismissed notice still fades rather than vanishing", "the fade never runs backwards",
/// "it ends" — can be tested without a screen.
/// </para>
/// <para>
/// The one property that matters is <see cref="IsFinished"/>. A full-screen window that
/// forgot to close is not a bug somebody works around; it is a machine they have to restart
/// in front of whoever they were presenting to.
/// </para>
/// </remarks>
public sealed class BlameAlert
{
    /// <summary>
    /// How long the notice takes to arrive. Fast, because it is supposed to be startling —
    /// but not instant: a frame-one appearance reads as a rendering glitch, and a quarter of a
    /// second of movement reads as something landing.
    /// </summary>
    public static readonly TimeSpan SlamIn = TimeSpan.FromMilliseconds(220);

    /// <summary>
    /// How long it takes to leave. Longer than it took to arrive, so the screen coming back
    /// does not itself feel like a second event.
    /// </summary>
    public static readonly TimeSpan FadeOut = TimeSpan.FromMilliseconds(450);

    private readonly TimeSpan _life;

    private TimeSpan _elapsed;
    private TimeSpan? _dismissedAt;

    /// <param name="blame">Who, and what they broke.</param>
    /// <param name="taunt">The line underneath. Already chosen — this class has no opinions.</param>
    /// <param name="life">
    /// Total time on screen, fades included. Floored at the two fades plus a moment, because a
    /// notice configured to last a quarter of a second would be a flash nobody could read and
    /// everybody would report as a fault.
    /// </param>
    public BlameAlert(Blame blame, string taunt, TimeSpan life)
    {
        Blame = blame;
        Taunt = taunt;

        var shortest = SlamIn + FadeOut + TimeSpan.FromMilliseconds(400);
        _life = life < shortest ? shortest : life;
    }

    public Blame Blame { get; }

    public string Taunt { get; }

    /// <summary>Seconds since it appeared. What the hazard stripes crawl on.</summary>
    public double Seconds => _elapsed.TotalSeconds;

    /// <summary>Whether it is done and the window should go.</summary>
    public bool IsFinished => Opacity <= 0 && _elapsed > SlamIn;

    /// <summary>
    /// How solid the notice is, 0 to 1: up over <see cref="SlamIn"/>, held, and down over
    /// <see cref="FadeOut"/> at the end — or from wherever a dismissal caught it.
    /// </summary>
    public double Opacity
    {
        get
        {
            if (_dismissedAt is { } dismissed)
            {
                // Faded out from where it actually was rather than from full: dismissing
                // something during its arrival should not make it brighten first.
                var wasAt = Rise(dismissed);
                var through = Progress(_elapsed - dismissed, FadeOut);
                return wasAt * (1 - Ease(through));
            }

            var remaining = _life - _elapsed;
            if (remaining <= TimeSpan.Zero) return 0;
            if (remaining < FadeOut) return Ease(Progress(remaining, FadeOut));

            return Rise(_elapsed);
        }
    }

    /// <summary>
    /// How much bigger than its resting size the headline is drawn, 1 upwards.
    /// </summary>
    /// <remarks>
    /// Overshoots to about 1.06 and settles. The overshoot is the whole character of the
    /// thing — a name that grows into place is an announcement, a name that is simply there is
    /// a label.
    /// </remarks>
    public double Punch
    {
        get
        {
            var through = Progress(_elapsed, SlamIn);
            if (through >= 1) return 1;

            // A single sine hump: 0 at both ends, and a peak a twentieth of the way above
            // resting size in the middle. Cheap, and it does not need a spring solver to
            // explain it.
            return 1 + Math.Sin(through * Math.PI) * 0.06;
        }
    }

    /// <summary>
    /// A slow 0-to-1-to-0 for the glow behind the name, so the notice breathes rather than
    /// glares. Independent of the fades, which are about the whole window.
    /// </summary>
    public double Pulse => (Math.Sin(_elapsed.TotalSeconds * Math.PI * 1.1) + 1) / 2;

    /// <summary>Move it on by however long the last frame took.</summary>
    public void Advance(TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero) return;

        // Clamped, because a laptop coming out of sleep hands you a frame several minutes
        // long, and an alert that had silently finished during the lid being shut would jump
        // from full brightness to gone with no fade at all.
        _elapsed += elapsed > TimeSpan.FromMilliseconds(250) ? TimeSpan.FromMilliseconds(250) : elapsed;
    }

    /// <summary>
    /// Somebody has clicked, or pressed a key, or said enough. Starts the fade wherever it
    /// stands.
    /// </summary>
    /// <remarks>
    /// Deliberately not an instant close. The notice covers the whole screen, and a
    /// full-screen thing that blinks out on mouse-down leaves people unsure whether they
    /// clicked something underneath it.
    /// </remarks>
    public void Dismiss() => _dismissedAt ??= _elapsed;

    /// <summary>Whether somebody has already waved it away.</summary>
    public bool IsDismissed => _dismissedAt is not null;

    private static double Rise(TimeSpan at) => Ease(Progress(at, SlamIn));

    private static double Progress(TimeSpan part, TimeSpan whole) =>
        whole <= TimeSpan.Zero ? 1 : Math.Clamp(part / whole, 0, 1);

    /// <summary>Smoothstep. Ordinary, and it keeps both fades off the linear ramp that reads as cheap.</summary>
    private static double Ease(double t) => t * t * (3 - 2 * t);
}
