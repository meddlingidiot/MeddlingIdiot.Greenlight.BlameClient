# Changelog

All notable changes to this project are documented here.

## [Unreleased]

### Added

- First cut: an app that is invisible until a pipeline breaks, and then throws the name of
  whoever pushed it across every monitor. Twelve seconds, then gone. No desktop widget, no
  colour to watch, nothing sitting in the corner - a notice that is always on screen is
  wallpaper within a week.
- The name in the largest letters that will fit, with **BROKE THE BUILD!** underneath it
  rather than above. Set the other way round it reads backwards to somebody glancing at it:
  they see what happened and then have to look up to find out who.
- Taunts, rotating rather than random. Fourteen general lines ship in the defaults, and
  nobody is singled out by name - `{name}` in any line is filled in with the culprit's first
  name off the snapshot instead. Personal lines for anybody who has earned them go in
  `blame.json`, which is written on first run so it can be edited, and names are matched
  loosely - an entry filed under `jamie` finds `JAMIE.DOE` and `jamie.doe@example.com`,
  because providers send all three.
- A demonstration that blames whoever is logged in, since there is no snapshot to take a name
  from and nobody else's name belongs in it.
- Rotation rather than a random pick. Random will serve the same insult twice running inside
  a fortnight, and the second time somebody sees it they stop reading the insults.
- Once per breakage, however many snapshots carry it. Greenlight sends a snapshot on every
  change and a broken build sits in all of them, so the obvious implementation is a dozen
  full-screen notices over one failure.
- Nothing about the backlog. The first snapshot after attaching carries the whole retention
  window, so without this a machine starting on Monday opens with an accusation about a
  pipeline that broke on Friday afternoon and was fixed before the pub. Switchable from the
  tray for anybody who disagrees.
- Nothing older than half an hour even in a live snapshot, for a Greenlight that reconnects
  after an outage and re-sends an hour of history as though it were news.
- Everything forgotten when Greenlight goes away, so the snapshot that arrives when it comes
  back reads as a backlog. Greenlight restarting under a Velopack update is not four people
  breaking four pipelines at 3am.
- A count of the other pipelines that went down in the same breath - *and took 3 more
  pipelines with it* - rather than naming one out of four and looking like it missed the rest.
- Fixed and broken again announced as two events, which is what it is.
- Partial successes, acknowledged runs and builds still in progress all ignored. A flaky
  non-blocking task is somebody's known problem, not a public accusation.
- Name tidying that stops short of being clever: `jamie.doe@example.com` becomes Jamie Doe,
  `Jamie Doe <jamie@example.com>` loses the address, and an all-capitals
  directory export is quietened - but `Joris van der Berg` and `bell hooks` are left exactly
  as they arrived. A blanket title-case pass turns a colleague's name into `Van Der Berg` in
  letters a foot tall in front of everybody.
- A full name, project and time in the small print. **JAMIE** is the joke; the line that
  survives being screenshotted and sent to somebody has to say which Jamie.
- Dismissal by click, key or wheel, fading from wherever it had got to rather than blinking
  out - a full-screen thing that vanishes on mouse-down leaves people unsure whether they
  clicked something underneath it. Deaf for the first third of a second, so a click already
  committed to before it landed does not dismiss it unread.
- A mute on the tray icon's left click, which is the thing anybody will want in a hurry. A
  breakage that happens while muted is remembered as seen, so unmuting does not ambush you
  with it.
- A banner mode covering the top quarter of the screen, for when somebody else is looking at
  it, and a choice of every monitor or only the main one.
- `--demo` and a tray item to put a made-up notice up, because the alternative way to find out
  what this looks like is to break a build.
- One copy per session. A second launch - the one somebody started by hand beside the one
  Windows started - leaves quietly, rather than putting up a second notice for the same
  breakage that fights the first for the top of the screen and looks like the taunts
  flickering.
- Sound off by default. In an open-plan office a noise is a much bigger act than a picture,
  and it should be somebody's decision rather than something they discover at 9:40 on a
  Monday.
- Tests for all of the deciding and all of the timing: 80 of them, covering which snapshots
  are news, whose name comes out of a provider's free text, who gets which taunt, and that
  the notice always finishes. A full-screen window that did not close is a machine somebody
  restarts in front of whoever they were presenting to.
