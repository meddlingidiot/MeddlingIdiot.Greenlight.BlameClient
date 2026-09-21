using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Greenlight.BlameClient;

/// <summary>
/// Draws the notice: a dark screen, hazard stripes top and bottom, and a name across the
/// middle in the largest letters that will fit.
/// </summary>
/// <remarks>
/// <para>
/// Everything is measured from the window's own size each frame rather than laid out with
/// controls. The whole thing is six pieces of text and some rectangles, it has to work at
/// 1366×768 and at 5120×1440, and a panel full of controls would have brought a style system
/// with it for no benefit at all.
/// </para>
/// <para>
/// The sizes below are fractions of the screen height, and the text is then shrunk to fit if
/// the name is a long one. Fractions of the height rather than of the width because an
/// ultrawide is not an invitation to draw letters eighteen inches tall.
/// </para>
/// </remarks>
public sealed class BlameCanvas : Control
{
    /// <summary>The screen behind everything. Not quite black — pure black reads as a dead monitor.</summary>
    private static readonly Color Backdrop = Color.FromRgb(12, 10, 11);

    private static readonly Color Hazard = Color.FromRgb(240, 186, 24);
    private static readonly Color HazardDark = Color.FromRgb(22, 18, 14);

    private static readonly Color Alarm = Color.FromRgb(228, 42, 32);
    private static readonly Color AlarmGlow = Color.FromRgb(255, 84, 64);

    private static readonly IBrush Headline = new SolidColorBrush(Color.FromRgb(255, 248, 244));
    private static readonly IBrush Subhead = new SolidColorBrush(Color.FromRgb(236, 196, 190));
    private static readonly IBrush TauntInk = new SolidColorBrush(Color.FromRgb(246, 214, 96));
    private static readonly IBrush Footnote = new SolidColorBrush(Color.FromArgb(150, 236, 228, 226));

    /// <summary>
    /// The lettering. Bold, and the platform's own family: a notice that fell back to a
    /// substitute font on somebody's machine would be a notice that fits on one line here and
    /// runs off the edge there.
    /// </summary>
    private static readonly Typeface Face = new(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);

    private static readonly Typeface LightFace = new(FontFamily.Default, FontStyle.Normal, FontWeight.Normal);

    private static readonly Typeface TauntFace = new(FontFamily.Default, FontStyle.Italic, FontWeight.SemiBold);

    /// <summary>How tall one diagonal stripe band is, as a fraction of the screen.</summary>
    private const double BandHeight = 0.055;

    public BlameCanvas()
    {
        // The window handles the mouse itself — there is nothing on here to click, and a
        // hit-testable canvas would swallow the click that dismisses the whole thing.
        IsHitTestVisible = false;
    }

    /// <summary>What is being shown. Null between notices, when the window should not exist anyway.</summary>
    public BlameAlert? Alert { get; set; }

    /// <summary>
    /// Whether the stripes crawl and the glow breathes.
    /// </summary>
    /// <remarks>
    /// Off holds both at a fixed point rather than removing them, so the notice still looks
    /// like itself — the setting is for somebody who finds moving things on a screen
    /// unpleasant, and a stripped-back version would read to everyone else as the app having
    /// half failed to draw. The arrival is not affected: a full-screen window appearing and
    /// disappearing instantly is harder to sit through than one that moves.
    /// </remarks>
    public bool Animated { get; set; } = true;

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var alert = Alert;
        if (alert is null) return;

        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0) return;

        var opacity = alert.Opacity;
        if (opacity <= 0) return;

        using var fade = context.PushOpacity(opacity);

        DrawBackdrop(context, width, height, alert, Animated);
        DrawStripes(context, width, height, alert, Animated);
        DrawWords(context, width, height, alert);
    }

    /// <summary>
    /// The dark screen, and the red breathing into the middle of it.
    /// </summary>
    /// <remarks>
    /// The glow is a stack of flat translucent rectangles rather than a gradient brush: a
    /// handful of bands at a low alpha reads as light coming off the headline, costs nothing,
    /// and does not depend on which of the gradient properties the current Avalonia calls
    /// what.
    /// </remarks>
    private static void DrawBackdrop(DrawingContext context, double width, double height, BlameAlert alert, bool animated)
    {
        context.DrawRectangle(new SolidColorBrush(Backdrop, 0.93), null, new Rect(0, 0, width, height));

        var centre = height * 0.44;
        var reach = height * 0.36;
        var strength = 0.05 + (animated ? alert.Pulse : 0.5) * 0.05;

        for (var band = 0; band < 14; band++)
        {
            // Widest and faintest first, so the bright core is laid down last and nothing
            // washes over it.
            var spread = reach * (1 - band / 14.0);
            var alpha = strength * (band / 14.0) * 0.6;

            context.DrawRectangle(
                new SolidColorBrush(AlarmGlow, alpha), null,
                new Rect(0, centre - spread, width, spread * 2));
        }
    }

    /// <summary>
    /// The hazard stripes along the top and bottom, crawling sideways while the notice is up.
    /// </summary>
    /// <remarks>
    /// Drawn as thick diagonal lines clipped to the band, rather than as a tiled brush. The
    /// crawl is then one number added to an offset, and it does not need a brush transform
    /// that behaves differently across backends.
    /// </remarks>
    private static void DrawStripes(DrawingContext context, double width, double height, BlameAlert alert, bool animated)
    {
        var band = Math.Max(14, height * BandHeight);

        foreach (var top in new[] { 0.0, height - band })
        {
            var rect = new Rect(0, top, width, band);
            using var clip = context.PushClip(rect);

            context.DrawRectangle(new SolidColorBrush(HazardDark), null, rect);

            var step = band * 1.15;
            var pen = new Pen(new SolidColorBrush(Hazard), band * 0.52);

            // Crawls only while the notice is animated, and always in the same direction on
            // both bands — counter-rotating stripes read as a decorative pattern rather than
            // as machinery that has stopped.
            var drift = animated ? alert.Seconds * band * 0.9 % step : 0;

            // Starting a band's width to the left of the screen and running a band past the
            // right, because each line is drawn at 45 degrees and its ends are outside the
            // rectangle it fills.
            for (var x = -band - step + drift; x < width + band; x += step)
                context.DrawLine(pen, new Point(x, top + band), new Point(x + band, top));
        }
    }

    /// <summary>
    /// The name, what they broke, and the line about it — stacked, and centred as one block.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The name goes first and "BROKE THE BUILD!" underneath it, because that is the order the
    /// sentence is said in. The obvious alternative — the accusation as a kicker above the
    /// name, the way a newspaper sets a strapline — reads backwards on a screen somebody
    /// glances at for half a second: they see "BROKE THE BUILD" and then have to look up to
    /// find out who.
    /// </para>
    /// <para>
    /// The whole stack is measured before any of it is drawn and then centred in the space
    /// between the footnotes and the top band. Laying it out downwards from a fixed fraction
    /// of the height is what leaves a taunt sitting on top of the small print as soon as
    /// somebody writes a long one, or four pipelines break at once and the block grows a line.
    /// </para>
    /// </remarks>
    private static void DrawWords(DrawingContext context, double width, double height, BlameAlert alert)
    {
        var blame = alert.Blame;
        var room = width * 0.88;
        var middle = width / 2;

        var name = Text(blame.FirstName.ToUpperInvariant(), Face, height * 0.19, Headline, room);
        var kicker = Text("BROKE THE BUILD!", Face, height * 0.075, new SolidColorBrush(Alarm), room);
        var what = Text(blame.What, LightFace, height * 0.042, Subhead, room);
        var more = blame.AndTheRest is { } rest ? Text(rest, LightFace, height * 0.030, Footnote, room) : null;
        var taunt = Text(alert.Taunt, TauntFace, height * 0.038, TauntInk, room);

        // The huge name carries a lot of leading it does not want — a 200px font measures
        // nearer 270px tall — so the stack advances by less than its measured height. Anything
        // else leaves a hole under the name that reads as a missing line.
        var nameBlock = name.Height * 0.84;

        var gapAfterKicker = height * 0.05;
        var gapAfterWhat = height * 0.012;
        var gapAroundRule = height * 0.028;

        var stack = nameBlock + kicker.Height + gapAfterKicker + what.Height + gapAfterWhat
                    + (more?.Height + gapAfterWhat ?? 0)
                    + gapAroundRule * 2 + taunt.Height;

        var band = Math.Max(14, height * BandHeight);
        var floor = height - band - FootnoteReserve(height);
        var y = band + (floor - band - stack) / 2;

        // The name punches out of its own centre. Scaling the font size instead would re-lay
        // the glyphs every frame and make the word visibly jitter as it settled.
        var nameCentre = new Point(middle, y + nameBlock / 2);

        using (context.PushTransform(
                   Matrix.CreateTranslation(-nameCentre.X, -nameCentre.Y)
                   * Matrix.CreateScale(alert.Punch, alert.Punch)
                   * Matrix.CreateTranslation(nameCentre.X, nameCentre.Y)))
        {
            // Drawn from a top that accounts for the leading trimmed off the block, so the
            // glyphs sit where the arithmetic above thinks they do.
            DrawCentred(context, name, middle, y - (name.Height - nameBlock) / 2);
            DrawCentred(context, kicker, middle, y + nameBlock);
        }

        y += nameBlock + kicker.Height + gapAfterKicker;

        DrawCentred(context, what, middle, y);
        y += what.Height + gapAfterWhat;

        if (more is not null)
        {
            DrawCentred(context, more, middle, y);
            y += more.Height + gapAfterWhat;
        }

        // A rule between the facts and the opinion. The taunt is the one line on screen that
        // somebody wrote rather than something a build server reported, and it should not read
        // as part of the report.
        var rule = width * 0.10;
        context.DrawLine(
            new Pen(new SolidColorBrush(Alarm, 0.55), Math.Max(1, height * 0.003)),
            new Point(middle - rule, y + gapAroundRule),
            new Point(middle + rule, y + gapAroundRule));

        DrawCentred(context, taunt, middle, y + gapAroundRule * 2);

        DrawFootnotes(context, width, height, alert);
    }

    /// <summary>
    /// How much room the small print takes along the bottom, so the block above knows where to
    /// stop. Arithmetic rather than a measurement, because the two lines it covers are both a
    /// fixed fraction of the height and neither ever wraps.
    /// </summary>
    private static double FootnoteReserve(double height) => height * 0.022 * 2.6 + height * 0.05;

    /// <summary>The small print: who exactly, where, and how to make it go away.</summary>
    private static void DrawFootnotes(DrawingContext context, double width, double height, BlameAlert alert)
    {
        var blame = alert.Blame;
        var room = width * 0.8;
        var size = Math.Max(11, height * 0.022);
        var band = Math.Max(14, height * BandHeight);

        // The full name lives down here rather than in the headline. "JAMIE" is the joke; the
        // line that survives being screenshotted and sent to somebody has to say which Jamie,
        // in which project, at what time.
        var who = string.IsNullOrWhiteSpace(blame.Project)
            ? $"{blame.Who} · {blame.Pipeline} · {blame.BrokeAt.ToLocalTime():HH:mm}"
            : $"{blame.Who} · {blame.Project} / {blame.Pipeline} · {blame.BrokeAt.ToLocalTime():HH:mm}";

        var dismiss = Text(
            alert.IsDismissed ? "Right you are." : "Click anywhere, or press any key.",
            LightFace, size * 0.92, Footnote, room);

        DrawCentred(context, dismiss, width / 2, height - band - dismiss.Height - height * 0.018);

        var detail = Text(who, LightFace, size, Footnote, room);
        DrawCentred(context, detail, width / 2, height - band - dismiss.Height - detail.Height - height * 0.026);
    }

    /// <summary>
    /// Lay a line out, shrinking it until it fits the width it has been given.
    /// </summary>
    /// <remarks>
    /// Measured for real and re-measured once at the corrected size, rather than estimated
    /// from a character count. Names are the whole point of this app and they are wildly
    /// different widths — "Bo" and "Krzysztof" at the same font size are not the same problem,
    /// and a headline clipped at the edge of the screen would be the first thing anybody
    /// noticed about it.
    /// </remarks>
    private static FormattedText Text(string text, Typeface face, double size, IBrush brush, double room)
    {
        var laid = new FormattedText(
            text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, size, brush);

        if (laid.Width <= room || laid.Width <= 0 || room <= 0) return laid;

        return new FormattedText(
            text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            face, size * room / laid.Width, brush);
    }

    private static void DrawCentred(DrawingContext context, FormattedText text, double centreX, double top) =>
        context.DrawText(text, new Point(centreX - text.Width / 2, top));
}
