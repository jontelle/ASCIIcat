# ASCIIcat v0.2 requirements

ASCIIcat is a lightweight, local-first Dalamud plugin for preserving and reusing
multi-line ASCII and Unicode art seen in FFXIV chat or found elsewhere.

## Fidelity contract

ASCIIcat must never silently trim, normalize, reorder, wrap, or replace artwork.

- Preserve leading, trailing, and repeated spaces.
- Preserve blank lines and line order.
- Preserve Unicode code points, including non-breaking and ideographic spaces.
- Treat newline sequences as logical line boundaries without changing line text.
- Show tabs and invisible characters diagnostically; convert only after an
  explicit, previewed choice.
- Keep an original snapshot so edits can be reverted.

## v0.2 scope

- Closable, pinnable Dalamud window opened with `/asciicat` or `/acat`.
- Session-only rolling buffer of recent player chat and local `/echo` messages.
- Select consecutive chat lines and save them as one ordered artwork.
- Paste from clipboard or type into a plain multiline editor.
- Exact preview and a diagnostic visible-whitespace preview.
- Local collection with names, tags, favorites, search, edit, and delete.
- Copy one line at a time with progress and passive exact-match verification.
- Send a complete artwork in order after one explicit confirmation.
- Choose Echo, Party, Say, Free Company, Alliance, Yell, or Shout as the
  destination; default to local-only Echo.
- Wait at least one second between lines, show progress, and allow immediate
  cancellation.
- Protect edge padding from FFXIV trimming by substituting same-width NBSP only
  in the outgoing chat payload; keep the saved artwork unchanged.
- Local JSON persistence and timestamped JSON backup export.
- No network access, telemetry, accounts, or cloud services.

## Explicitly out of scope

- Unattended, repeating, or AFK gameplay automation.
- Cloud synchronization or a community gallery.
- Rich text, drawing tools, or image-to-ASCII conversion.
- Recovering chat messages received before ASCIIcat was loaded.
- Silent repair of text copied from a source that already collapsed its spacing.

## Acceptance checks

1. A pasted block round-trips through save/load with identical line contents.
2. Spaces, tabs, blank lines, NBSP, ideographic spaces, and zero-width characters
   survive a save/load cycle unchanged.
3. Recent-chat selection saves lines in chronological order regardless of click order.
4. Copy Next Line copies exactly one stored line and never sends it.
5. Whole-artwork sending requires one confirmation, preserves line order, and
   can be cancelled before the remaining queue is sent.
6. The outgoing representation protects leading and trailing spaces while the
   stored artwork remains byte-for-byte unchanged.
7. Reloading or closing the plugin does not lose saved artwork.
8. The recent-chat buffer is never written to disk.
