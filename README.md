# Aurora Asset Editor for Linux

**Fork of Aurora Asset Editor by Swizzy** – Ported to Avalonia UI for cross-platform (Linux) support.

---

## About This Project

This project is a Linux port of the original Aurora Asset Editor created by Swizzy.

The original tool was built using WPF (Windows Presentation Foundation) and was only available for Windows. This version replaces the UI framework with Avalonia UI, allowing the application to run natively on Linux (and other platforms like macOS and Windows) with the same core functionality.

---

## Credits & License

- **Original Author**: Swizzy  
  The original source code was released as open-source and freely available for the community.  
  Original repository: https://github.com/XboxUnity/AuroraAssetEditor

- **Linux Port Author**: [Your Name / GitHub Username]  
  - Replaced WPF with Avalonia UI (11.0.10)  
  - Converted all .xaml files to .axaml  
  - Updated C# code-behind to use Avalonia namespaces and APIs  
  - Adapted file dialogs, drag-and-drop, and clipboard for cross-platform compatibility  
  - Tested and optimized for Linux (Ubuntu / Debian-based)

- **UI Framework**: Avalonia UI – MIT License

---

## Tech Stack

- .NET 8
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

git clone https://github.com/YOUR_USERNAME/AuroraAssetEditorLinux.git
cd AuroraAssetEditorLinux
dotnet restore
dotnet build
dotnet run

---

## Supported Platforms

- Windows (x64) – Compatible
- Linux (x64) – Fully tested
- macOS (x64) – Untested (should work)

---

## Disclaimer

This project is a fork of the original work by Swizzy. All credits for the original logic, asset structure, and FTP handling belong to the original author. The UI and cross-platform compatibility changes are the work of this fork's maintainer.

This project is not affiliated with the original author or XboxUnity. It is shared under the same open-source spirit of the original work.

---

## License

This project is released under the same open-source terms as the original work.  
Please refer to the original repository for licensing details.

---

## Future Plans

- Add DeepSeek AI integration for smart asset suggestions
- Improve performance for large asset files
- Add more locale/language support
- Package as .deb / .AppImage for easy Linux installation

---

## Contributing

Feel free to open issues or submit pull requests. Contributions are welcome.

---

## Final Thanks

- Swizzy – for creating the original tool and sharing it with the community.
- Avalonia Team – for making cross-platform .NET UI development possible.
