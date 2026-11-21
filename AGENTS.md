# AGENTS.md

## Project
- Single-project Avalonia desktop app on .NET 10 / C# 12 for viewing and editing Xbox 360 Aurora `.asset` and FSD `.assets` files. One csproj, no solution, no tests, no CI.
- UI is code-behind (no MVVM framework). Entry flow: `Program.cs` -> `App.axaml(.cs)` -> `MainWindow.axaml(.cs)`.
- `MainWindow.axaml` declares the shell and named menu/tab controls; tab contents and menu handlers are created in `MainWindow.axaml.cs` via `new XControl(this)`. The Boxart, Background, Icon/Banner, and Screenshots controls each own an `AuroraAsset.AssetFile`.
- Asset code: `Classes/AuroraAsset.cs` (RXEA container read/write, big-endian), `Classes/FSDAsset.cs` (FSDA reader), `Classes/XenosTextureCodec.cs` (texture codec). `Classes/FTPOperations.cs` handles FTP and `Content.db`; online lookups are in `XboxUnity.cs`, `XboxAssetDownloader.cs`, and `InternetArchiveAssetDownloader.cs`.
- FTP UI is disabled: `FtpAssetsTab` is a placeholder TextBlock and the `FtpUpload` menu item has no handler, although the FTP/DB backend code remains.

## Commands
- Build (the only automated check): `dotnet build` -> `bin/Debug/net10.0/linux-x64/`. There is no test project or sample asset fixture; `dotnet test` does nothing, so verify asset changes by building and manually loading/saving a real file.
- Run: `dotnet run` (needs X11/Wayland; not usable headless).
- Linux publish: `dotnet publish -c Release -r linux-x64 --self-contained true -o publish`. `app.debspec` packages `publish/**`, so refresh it before building the .deb.
- Windows/Wine publish: `dotnet publish -c Release -r win-x86 --self-contained true` -> `bin/Release/net10.0/win-x86/publish/`.
- Packaging config: `AuroraAssetEditorLinux.pupnet.conf` (PupNet) and `app.debspec`. Release version appears in the pupnet conf, `app.debspec`, and `my-app-staging/DEBIAN/control`; update all three when bumping.
- Requires the .NET 10 SDK. README's ".NET 8 SDK or higher" is stale (`net10.0`).

## Asset conversion constraints
- `PhoenixTools.AuroraAssetDll` is a managed shim; it never loads `AuroraAsset.dll` or `msvcr100.dll`. Those DLLs are copied only for Windows RIDs and nothing P/Invokes them, so README's "publish for Windows and run under Wine" workaround is stale.
- The codec only handles uncompressed 32-bit ARGB Xenos textures. Compressed decode returns `false`; `Encode(..., useCompression: true)` returns `false`.
- Therefore **Settings > Use Compression is non-functional**: loading an image with it checked silently leaves the asset entry unchanged. Leave it off unless you implement compressed Xenos tiling.
- `AuroraAssetDll.ProcessDDSToImage` always returns `false`, so DDS entries in FSD `.assets` do not load.

## Gotchas
- Two `GlobalState` classes exist. The live one is `AuroraAssetEditorLinux.GlobalState` in `MainWindow.axaml.cs` (`GameData`); `Helpers/GlobalState.cs` (`Game`) is dead code. Edit the live one.
- `App.axaml` pins `RequestedThemeVariant="Light"` as a fix for blank UI on Wayland. Do not switch it back to `Default` without testing Wayland.
- `publish/` and `my-app-staging/` are checked-in build/package snapshots (~110 MB each); `Deploy/OUT/` is checked in too. `dotnet build` does not update them. Do not hand-edit them or `git add` regenerated binaries; avoid recursive searches in those directories.
- Logs go to the current working directory (`error.log`, `app.log`); FTP settings are typically `~/.config/AuroraAssetEditor/ftp.json`.
- `Data/*.py` are unreferenced scratch scripts (some malformed), not build inputs.
- Build noise is expected: NU1902/NU1903 warnings for `SixLabors.ImageSharp` 3.1.12, and Avalonia core 12.1.1 with `Avalonia.Controls.DataGrid` 11.0.10. The build succeeds; do not "fix" these casually.
- Default branch is `master`. `origin/main` is an unrelated history (no merge base), so do not merge it blindly.
