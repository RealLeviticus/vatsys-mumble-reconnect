# MumbleReconnect

A vatSys plugin that detects AFV/Mumble audio disconnects and provides manual and automatic reconnection. Adds a `Mumble Status` dropdown to the menu bar that turns red when disconnected.

## Features
- Detects AFV/Mumble audio connection state via a `Mumble Status` menu bar button.
- Red background indicator when audio is disconnected; default styling when connected.
- Dropdown with a `Reconnect` button (available to all users).
- Optional `Disconnect` button in the dropdown for whitelisted CIDs only.
- Automatic background reconnect attempts with exponential backoff (5/10/20/40/60 seconds) when audio drops while connected to VATSIM, falling back to a slow retry every 60 seconds after the fast attempts are exhausted.
- Prompt on audio loss offering a manual Retry action (only when Mumble is actually disconnected).
- Uses reflection to call internal vatSys Mumble methods (connect/reconnect/disconnect).

## Installation
- Download the plugin from the releases page: https://github.com/RealLeviticus/vatsys-mumble-reconnect/releases
- Right-click the downloaded ZIP file, select `Properties`, and click `Unblock` (if present).
- Unzip the archive.
- Copy the plugin DLL to the base vatSys plugins directory:
  - `[...]\Program Files (x86)\vatSys\bin\Plugins`
- Start vatSys and verify the plugin is installed via: `Info > About`.
- Ensure the following vatSys setting is enabled so the plugin can verify position/activity:
  - __Settings > Activity Privacy > Display current activity as a status message__

Notes:
- Targets .NET Framework 4.7.2 and is compiled with C# 7.3.
- The plugin uses reflection against internal vatSys types (e.g., `vatsys.Mumble`) � incompatible vatSys versions may break functionality.

## Usage
- After installation, open vatSys. A `Mumble Status` button will appear at the far right of the menu bar.
- The button appears with default styling when connected, and turns red with white text when disconnected.
- Click the button to open a dropdown:
  - `Reconnect` -- attempts an immediate reconnect (only available while connected to VATSIM on an ATC position).
  - `Disconnect` -- forces a disconnect (only visible for whitelisted CIDs).
- When the plugin detects an audio loss it will:
  - Show a prompt offering Retry (message box on a separate STA thread).
  - Begin automatic background reconnect attempts (backing off from 5 up to 60 seconds between attempts) when connected to the official VATSIM server on an ATC position.

Behavior details:
- Manual reconnect is only allowed while connected to VATSIM on an ATC position (to prevent abuse).
- After 5 failed fast attempts the plugin logs a single warning and keeps retrying quietly once per minute until the connection is restored.
- A manual `Disconnect` suspends auto-reconnect until the user reconnects manually (or the connection is restored externally).

## Troubleshooting
- Menu not visible: wait a few seconds after vatSys starts. The plugin waits for the UI to be ready before injecting the menu.
- Reconnect fails or reports an error: ensure you are connected to the official VATSIM server on an ATC position and that vatSys internals have not changed.
- Prompt not shown: the prompt runs on an STA background thread; if UI restrictions or security policies prevent message boxes from appearing, check vatSys logs or debug output for reflection exceptions.
- Check the vatSys plugin logs or Debug output for reflection-related exceptions -- they indicate missing or renamed internal members.

## Building from source
- Target: .NET Framework 4.7.2, C# 7.3
- No NuGet dependencies - only .NET Framework and the vatSys host assemblies.
- The plugin exports via MEF: `[Export(typeof(IPlugin))]` and depends on the `vatsys` host assemblies at runtime.
- After building, copy the compiled DLL to the vatSys plugin folder as described above.

## Limitations & Compatibility
- The plugin relies on reflection into internal vatSys types and private members (for example, `vatsys.Mumble` and its instance/methods). Changes to vatSys internals may break functionality.
- Not all vatSys versions expose the same members -- the plugin tries to fail gracefully and logs errors when reflection fails.
- The plugin does not persist any user files or settings beyond what vatSys provides; auto-reconnect behavior is ephemeral and in-memory.

## Contributing / Development notes
- When updating for new vatSys versions, look for renamed fields/methods in the vatSys assemblies (e.g., Mumble instance/method names).
- Keep reflection error handling tolerant -- the host application may change between releases.
- Unit testing of reflection behavior is recommended by mocking the expected vatSys types where possible.

## License & Warranty
- Use at your own risk. The plugin is provided without warranty. See the repository LICENSE file (if present) for license details.
