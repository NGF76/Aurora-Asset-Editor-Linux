# Aurora Asset Editor for Linux

**Fork of Aurora Asset Editor by Swizzy** – Ported to Avalonia UI for cross-platform (Linux) support.

---

## Status

**This project is currently under active development.**  
Some features may be incomplete or unstable. Contributions and feedback are welcome.

---

## About This Project

This project is a Linux port of the original Aurora Asset Editor created by Swizzy.

The original tool was built using WPF (Windows Presentation Foundation) and was only available for Windows. This version replaces the UI framework with Avalonia UI, allowing the application to run natively on Linux (and other platforms like macOS and Windows) with the same core functionality.

---

## Credits & License

- **Original Author**: Swizzy  
  The original source code was released as open-source and freely available for the community.  
  Original repository: https://github.com/XboxUnity/AuroraAssetEditor

- **Linux Port Author**: NGF76  
  - Replaced WPF with Avalonia UI (11.0.10)  
  - Converted all .xaml files to .axaml  
  - Updated C# code-behind to use Avalonia namespaces and APIs  
  - Adapted file dialogs, drag-and-drop, and clipboard for cross-platform compatibility  
  - Tested and optimized for Linux (Ubuntu / Debian-based)

- **UI Framework**: Avalonia UI – MIT License

---

## Tech Stack

- .NET 10
- C# 12
- Avalonia UI 11.0.10
- SixLabors.ImageSharp (replaced System.Drawing)
- FluentFTP
- MessageBox.Avalonia

---

## Installation & Build

**Prerequisites**  
- .NET 8 SDK or higher  
- Git

**Clone & Build**

git clone https://github.com/NGF76/Aurora-Asset-Editor-Linux.git
cd AuroraAssetEditorLinux
dotnet restore
dotnet build
dotnet run

---

## Supported Platforms

- Windows (x86/32-bit native converter) – Compatible
- Linux (x64) – UI and managed features work; uncompressed Aurora `.asset` images use the native C# codec
- macOS (x64) – Untested (should work)

### Aurora asset conversion on Linux

`AuroraAsset.dll` and `msvcr100.dll` are 32-bit Windows PE libraries. They cannot
be loaded by a native Linux .NET process. The managed C# codec handles the
uncompressed ARGB variant, but existing compressed Xenos textures still require
the original converter.

For full Aurora asset conversion, publish the Windows build and run that build
with Wine (including Wine's 32-bit support):

```bash
dotnet publish -c Release -r win-x86 --self-contained true
wine bin/Release/net10.0/win-x86/publish/AuroraAssetEditorLinux.exe
```

The Windows build copies `AuroraAsset.dll` and `msvcr100.dll` beside the
executable automatically. A native Linux implementation would require a
separate converter for Aurora's Xbox texture format; these Windows DLLs are not
replaceable with one another or with a standard PNG/DDS decoder.

---

## To-Do / Roadmap

- [ ] Complete conversion of all WPF controls to Avalonia
- [ ] Fix remaining build errors
- [ ] Improve performance for large asset files
- [ ] Add more locale/language support
- [ ] Package as .deb / .AppImage for easy Linux installation
- [ ] Write user documentation

---

## Disclaimer

This project is a fork of the original work by Swizzy. All credits for the original logic, asset structure, and FTP handling belong to the original author. The UI and cross-platform compatibility changes are the work of this fork's maintainer (NGF76).

This project is not affiliated with the original author or XboxUnity. It is shared under the same open-source spirit of the original work.

---

## License

This project is released under the same open-source terms as the original work.  
Please refer to the original repository for licensing details.

---

## Contributing

Feel free to open issues or submit pull requests. Contributions are welcome.

---

## Final Thanks

- Swizzy – for creating the original tool and sharing it with the community.
- Avalonia Team – for making cross-platform .NET UI development possible.
