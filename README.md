# MeddlingIdiot.Greenlight.BlameClient

Nothing at all, until somebody breaks the build. Then their name across the whole screen.

A runnable reference consumer of the [Greenlight](https://github.com/meddlingidiot/MeddlingIdiot.Greenlight)
SDK, and the shortest possible answer to "what else could you do with a build indicator". It
has no desktop widget, no colour to watch and nothing sitting in the corner being green at
you. It waits. The moment a pipeline goes from working to broken it throws

> **JAMIE**
> **BROKE THE BUILD!**

across every monitor you own, in letters a room can read, with a line underneath about what
they have done this time. The name is whoever the build provider says pushed it. Twelve
seconds later it takes itself away and goes back to being nothing.

That restraint is the whole design. A notice that is always on screen is wallpaper within a
week, and wallpaper has never made anybody fix a build.

## The taunts

Everybody gets a line under their name, off a list that ships in the defaults and is written
into the config file on first run. Some of them stand on their own; some of them use the
culprit's name, which is filled in from the snapshot:

> *Red is your colour, Jamie. You wear it constantly.*

> *Tested locally. On a different machine. In a different language. Last Tuesday.*

> *Git blame was not needed. Git blame has never been needed.*

Nobody is singled out out of the box. If somebody on your team has earned lines of their own,
give them some in `blame.json` — the tray's **Edit the taunts…** opens it. Write `{name}`
anywhere in a line, personal or general, and it becomes the first name off the snapshot.

```jsonc
{
  "Taunts": {
    "Jamie": ["Again, {name}.", "The build did not fail. It gave up."],
    "Priya Raman": ["A rare event, and all the more enjoyable for it."]
  },
  "GeneralTaunts": ["The pipeline would like a word.", "Red is your colour, {name}."]
}
```

Names are matched loosely: an entry filed under `jamie` finds `Jamie`, `JAMIE.DOE` and
`jamie.doe@example.com`, because build providers send all three. A full name beats a first
name, so a team with two Jamies can single one of them out without the other inheriting it.
Lines rotate rather than being picked at random — random will serve the same insult twice
running inside a fortnight, and the second time somebody sees it they stop reading the
insults.

## Running it

```bash
dotnet run --project Greenlight.BlameClient
```

It will sit in the tray doing nothing, which is correct. To see what it actually does:

```bash
dotnet run --project Greenlight.BlameClient -- --demo
```

or click **Show me what it looks like** in the tray menu, which is also how to check a taunt
you have just written reads the way it did in your head. The demonstration blames whoever is
logged in, since there is no snapshot to take a name from and it is only fair.

Windows only: the always-on-top handling and the Run key are Win32. The SDK itself is not —
it is plain .NET, and the twenty lines that talk to it work anywhere.

## When it fires, and when it deliberately does not

The rules are all in [`Blame.cs`](Greenlight.BlameClient/Blame.cs), and most of them exist to
stop this app being unbearable:

- **Once per breakage.** Greenlight sends a snapshot on every change, and a broken build sits
  in every one of them until somebody fixes it. Announcing on each would be a dozen
  full-screen notices over one failure.
- **Nothing about the backlog.** The first snapshot after attaching carries the whole
  retention window. Without this, a machine starting on Monday opens with a full-screen
  accusation about a pipeline that broke on Friday afternoon and was fixed before the pub.
  Turn it on in the tray if you disagree.
- **Nothing older than half an hour**, even in a live snapshot — for a Greenlight that
  reconnects after an outage and re-sends an hour of history as though it were news.
- **Nothing already dismissed** in Greenlight, and nothing still running.
- **Not a partial success.** A non-blocking task failing is somebody's known problem, not a
  public accusation.
- **Fixed and broken again is two events**, and gets two notices. Which it deserves.
- **Greenlight restarting is not four people breaking four pipelines.** Everything is
  forgotten when it goes away, so the snapshot that arrives when it comes back reads as a
  backlog.

When one bad merge takes four pipelines down it names the newest and says *and took 3 more
pipelines with it*, rather than naming one and looking like it missed the rest.

## Getting rid of it

Click it. Press anything. Scroll. It fades from wherever it had got to rather than blinking
out, because a full-screen thing that vanishes on mouse-down leaves people unsure whether
they clicked something underneath it.

It ignores input for the first third of a second, so a click somebody had already committed
to before it landed does not dismiss it unread. And it closes itself regardless — the timer
is the only part of this that genuinely has to work.

Left-clicking the tray icon mutes it, which is the thing you will want in a hurry. A breakage
that happens while muted is remembered as seen, so unmuting does not ambush you with it.

## The tray

- **Watching for breakages** — the mute. Clicking the icon does the same.
- **How long it stays up** — a glance, long enough, uncomfortable, or make it count.
- **How much screen it takes** — all of it, or a banner across the top for when somebody is
  looking at your screen.
- **Which screens** — every monitor, or just the main one above the taskbar.
- **How solid**, **Stripes and movement**, **Make a noise as well** (off by default: in an
  open-plan office a noise is a much bigger act than a picture).
- **Announce what was already broken at startup** — off, for the reason above.
- **Start with Windows** — one value under the current user's `Run` key. Read back from the
  registry every time the menu opens, because you can turn the same thing off in Task
  Manager and a tick remembering what we last wrote would be lying to you.
- **Show me what it looks like**, **Open ⟨name⟩'s breakage**, **Edit the taunts…**,
  **Reload the file**.

## What it does not have

No Azure DevOps client, no GitHub client, no token, no polling loop. Everything it knows
arrives through the SDK, from the Greenlight already running on the machine:

```bash
dotnet add package MeddlingIdiot.Greenlight.Sdk
```

Strip out the drawing and the tray icon and the integration is about twenty lines, all of them
in [`App.cs`](Greenlight.BlameClient/App.cs).

## How it is put together

| | |
|---|---|
| [`App.cs`](Greenlight.BlameClient/App.cs) | The whole Greenlight integration, and the only thing that owns a window |
| [`Blame.cs`](Greenlight.BlameClient/Blame.cs) | Whether a snapshot is news, and whose name goes on it. No Avalonia, so it is testable |
| [`Culprit.cs`](Greenlight.BlameClient/Culprit.cs) | Turning `TriggeredBy` into something you can shout across a room |
| [`TauntBook.cs`](Greenlight.BlameClient/TauntBook.cs) | Who gets ridiculed, and with what |
| [`BlameAlert.cs`](Greenlight.BlameClient/BlameAlert.cs) | One notice, arriving, holding and leaving. Also testable |
| [`BlameCanvas.cs`](Greenlight.BlameClient/BlameCanvas.cs) | The drawing: stripes, glow, and the largest letters that will fit |
| [`BlameWindow.cs`](Greenlight.BlameClient/BlameWindow.cs) | The full-screen window, and every way of waving it off |
| [`BlameTray.cs`](Greenlight.BlameClient/BlameTray.cs) | The tray icon and its menu |

The name is the biggest thing on the screen and **BROKE THE BUILD!** goes underneath it,
because that is the order the sentence is said in. Set the other way round — the accusation
as a strapline above the name, the way a newspaper would — it reads backwards to somebody
glancing at it for half a second: they see what happened and then have to look up to find out
who.

The whole block is measured before any of it is drawn and then centred as one. Laying it out
downwards from a fixed fraction of the height is what leaves a taunt sitting on top of the
small print the first time somebody writes a long one.

The full name, the project and the time sit in the small print rather than in the headline.
**JAMIE** is the joke; the line that survives being screenshotted and sent to somebody has to
say *which* Jamie.

Names are guesswork, and deliberately gentle guesswork. `jamie.doe@example.com` becomes
Jamie Doe, but `Joris van der Berg` and `bell hooks` are left exactly as they arrived —
a blanket title-case pass turns a colleague's name into `Van Der Berg` in letters a foot tall
in front of everybody, which is a worse outcome than a tidy-up not happening.

The deciding is free of Avalonia so all of it can be tested without a window. An alert that
fires twice for one broken build, or names the wrong person, is not something anybody is going
to catch by looking at a screenshot.

```bash
dotnet test
```

## Licence

The code is MIT — see [LICENSE](LICENSE).

**The logo is not.** The MeddlingIdiot mascot icon in
[`Greenlight.BlameClient/Assets`](Greenlight.BlameClient/Assets) is all rights reserved: it is
not under the MIT License, and it is not sharable or reusable in forks or anything else. Fork
the code, bring your own icon. See [TRADEMARKS.md](TRADEMARKS.md).
