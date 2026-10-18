# Contributing

Open an issue describing the expected behavior, Windows version and steps to reproduce. Remove personal paths and audio configurations from logs before posting.

Keep changes compatible with the built-in .NET Framework C# compiler (C# 5) and avoid new dependencies without discussing the need. Run `./build.ps1 -Test` before submitting a pull request. Changes to configuration writes should include regression coverage and preserve existing user settings and backups. UI changes should be checked manually at different window sizes and display scaling levels.
