# ASCIIcat

ASCIIcat is a local-first Dalamud plugin for collecting, repairing, organizing,
and carefully reusing multi-line ASCII and Unicode art.

The alpha includes:

- exact clipboard/manual import;
- a small whitespace-preserving editor;
- recent-chat capture while the plugin is loaded;
- a searchable local collection;
- diagnostic rendering of invisible whitespace;
- one-confirmation, rate-limited sending to Echo, Party, Say, Free Company,
  Alliance, Yell, or Shout;
- automatic protection of leading and trailing spaces from FFXIV's chat parser;
- immediate cancellation and visible send progress; and
- a manual Copy Next Line fallback with passive exact-match progress.

ASCIIcat stores its library locally and has no accounts, telemetry, cloud sync,
or non-game network service. Chat is sent only after the user selects a
destination and confirms the whole-artwork send. The default destination is
local-only `/echo`, and the minimum delay is one second between lines.

## Install on another Windows PC

1. Install and launch FFXIV through XIVLauncher with Dalamud enabled.
2. Download the `ASCIIcat-0.2.0-alpha.zip` file from the GitHub release.
3. Extract the ZIP into a permanent folder, such as
   `Documents\DalamudDevPlugins\ASCIIcat`.
4. In FFXIV, open Dalamud Settings with `/xlsettings`, choose **Experimental**,
   and add the extracted ASCIIcat folder under **Dev Plugin Locations**.
5. Open `/xlplugins`, locate ASCIIcat in the developer-plugin section, and
   enable it.
6. Type `/asciicat` or `/acat` in the FFXIV chat box to open ASCIIcat.

ASCIIcat's saved-art library is local to each computer. To move an existing
library, use ASCIIcat's **Export timestamped JSON backup** button on the old
computer and keep that backup with your personal files. The release ZIP does
not contain anyone's saved artwork or chat history.

## Development

The plugin targets Dalamud API 15 through `Dalamud.NET.Sdk/15.0.0` and .NET 10.

```powershell
dotnet build ASCIIcat.slnx
dotnet run --project tests\ASCIIcat.Core.Tests
```

For local Dalamud testing, add the packaged Debug output directory under
Dalamud Settings > Experimental > Dev Plugin Locations.
