# Original application and migration

The original `Equalizer` application is a .NET Framework 4.8 Windows Forms utility with three fixed buttons. It rewrites `C:\Program Files\EqualizerAPO\config\config.txt` with one of three hard-coded templates:

| Button | Enabled includes |
| --- | --- |
| 2.1 → 7.1 | `2TO7.txt`, `FILTER.txt` |
| 5.1 → 7.1 | `5TO7.txt`, `FILTER.txt` |
| OFF | `FILTER.txt` |

The code comments call the first option stereo-to-7.1 while the button says 2.1-to-7.1. The tool itself contains no DSP or upmix implementation. Those behaviors depend entirely on the external include files, which were not present in the supplied source. The original OFF option disables the upmix includes but keeps `FILTER.txt` enabled.

The original borderless window has a fixed size and screen position, stays on top, and minimizes after successful writes. It retries file access by sleeping on the UI thread and reports errors to the console of a GUI process. It truncates the configuration before writing, so existing unrelated settings are lost and failed writes can leave incomplete content.

This independent copy replaces the fixed templates with profile discovery, configurable paths, a resizable standard window, visible errors, asynchronous retries and backed-up atomic writes. The original source folder is left unchanged and is not included in the public repository.

## Recreating the three original modes

First locate your own `2TO7.txt`, `5TO7.txt` and `FILTER.txt`; they cannot be recovered from this source. Create a separate profiles directory and put three wrapper `.txt` files there. Use actual absolute paths to your existing files, for example:

```text
# Stereo to 7.1.txt
Include: D:\Audio\APO\2TO7.txt
Include: D:\Audio\APO\FILTER.txt
```

The second wrapper includes `5TO7.txt` and `FILTER.txt`; the third includes only `FILTER.txt`. Review existing `config.txt` includes before applying: if those files are already enabled there, remove the duplicates manually after making a backup. The switcher's additive integration intentionally preserves existing processing, so it cannot automatically undo an already active upmix elsewhere in the configuration.

Dynamic behavior means the app discovers profile files and adapts the interface to their names and count. Automatic audio-content detection or speaker-layout detection is outside this version's scope.
