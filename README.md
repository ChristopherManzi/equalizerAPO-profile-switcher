# Equalizer Profile Switcher

A small Windows desktop app for switching [Equalizer APO](https://sourceforge.net/projects/equalizerapo/) profiles. Choose your configuration and a folder of `.txt` profiles; the app creates a button for each profile and updates the list as files are added, renamed or removed.

Written in C# with Windows Forms and .NET Framework 4.8. No NuGet packages, installer, background service or telemetry.

## Build and run

Requires Windows 10/11 with **.NET Framework 4.8** and Windows PowerShell. Equalizer APO must already be installed and configured for your audio device.

```powershell
git clone https://github.com/ChristopherManzi/equalizerAPO-profile-switcher.git
cd equalizerAPO-profile-switcher
powershell -NoProfile -ExecutionPolicy Bypass -File ./build.ps1 -Test
./artifacts/EqualizerProfileSwitcher.exe
```

The build uses the C# compiler supplied with Windows/.NET Framework. A .NET SDK or Visual Studio is not needed. CI also builds the executable and uploads it as a workflow artifact; GitHub sign-in may be required to download that artifact. There is no signed installer.

## Usage

1. Select your Equalizer APO `config.txt`. The standard installation location is detected when present; other drives and custom locations can be selected.
2. Select a dedicated folder containing your APO profile `.txt` files. Use **Open profiles folder** to edit that folder. A harmless starter profile is created on first launch; additional examples are in `examples/`.
3. Click a profile. Its filename becomes the button label. The check mark shows the profile referenced by this tool's managed file.
4. Optionally enable **Always on top** or **Minimize after applying**. Preferences are stored in `%LOCALAPPDATA%\EqualizerProfileSwitcher\settings.json`.

The profile list refreshes every second. Editing a profile's content needs no rebuild; click the profile again to trigger a configuration update, particularly when using profiles outside APO's watched config directory. The check mark indicates the selected include, not whether APO is installed correctly or processing your audio device.

Each profile is an ordinary APO configuration. It can contain filters, channel commands and `Include:` directives. See the [official configuration reference](https://sourceforge.net/p/equalizerapo/wiki/Configuration%20reference/) for APO commands. Use absolute paths for shared include files to avoid ambiguity. Profiles inherit the device and channel context at the insertion point; add explicit `Device:` and `Channel:` commands to your profiles when needed. This app does not infer speaker layouts, generate surround upmix matrices, or measure audio hardware.

## Configuration changes and backups

On first use, the app appends the following to the selected configuration:

```text
# Equalizer Profile Switcher
Include: equalizer-profile-switcher.txt
```

Existing bytes, comments and directives are preserved. The managed file points to the selected profile using its absolute path. Later switches replace only that managed file. Existing processing in `config.txt` still applies, so a profile named “No additional processing” does **not** disable those existing filters.

Writes use temporary files and atomic replacement, with a backup for each replaced file in `profile-switcher-backups/` beside the configuration. Temporary locks are retried asynchronously. Another instance of this tool cannot write concurrently. Avoid editing the main configuration during the first integration. Backups are retained until you remove them; they may contain local paths.

The target directory must be writable. If Windows denies access under Program Files, grant your account appropriate access to the APO configuration folder or launch the app manually as administrator. The app never elevates automatically. It does not modify APO installation, audio devices or system permissions.

To remove integration, close the app, remove its comment and `Include:` line from your configuration, then remove `equalizer-profile-switcher.txt` and its `.lock` file. To restore earlier configuration, copy the corresponding `.bak` file back over the original with the app closed. Never restore a backup while another editor is saving the same file.

Only use trusted APO profiles. `config.txt` and the managed filename are excluded from profile discovery to prevent direct self-inclusion. General include syntax and indirect include cycles are the profile author's responsibility. UTF-8 (with or without BOM) and BOM-marked UTF-16 configurations are supported. Use UTF-8 when creating new profiles.

## Development

```powershell
./build.ps1 -Test
```

Integration tests use temporary directories and cover discovery, existing-byte preservation, switching, backups, Unicode paths, self-inclusion, missing profiles, file locks, cleanup and UTF-16 integration. They never touch the machine's APO configuration. Playback and live APO integration require manual validation with an installed APO device.

- `src/ProfileService.cs`: discovery and configuration changes
- `src/Settings.cs`: local preferences and first-run defaults
- `src/Program.cs`: resizable, DPI-aware desktop UI
- `tests/`: dependency-free integration test runner

See [CONTRIBUTING.md](CONTRIBUTING.md) for contribution guidelines and [docs/ORIGINAL_ANALYSIS.md](docs/ORIGINAL_ANALYSIS.md) for the original application's behavior and migration notes.

## License

MIT. Equalizer APO is a separate project and is not bundled or maintained by this repository.
