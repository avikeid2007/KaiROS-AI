# Contributing to KaiROS AI

Thank you for your interest in contributing to **KaiROS AI**! 🎉 

KaiROS AI is a privacy-first, on-device AI assistant designed for Windows and Android, powered by [LLamaSharp](https://github.com/SciSharp/LLamaSharp) and [llama.cpp](https://github.com/ggerganov/llama.cpp). We welcome all kinds of contributions: bug reports, documentation updates, feature requests, code improvements, and new model recommendations.

---

## 📜 Table of Contents

- [Code of Conduct](#-code-of-conduct)
- [How Can I Contribute?](#-how-can-i-contribute)
- [Development Environment & Prerequisites](#-development-environment--prerequisites)
- [Building the Projects](#-building-the-projects)
  - [Desktop (WinUI 3)](#desktop-winui-3)
  - [Mobile (Android - .NET MAUI)](#mobile-android---net-maui)
- [Key Architectural Guidelines](#-key-architectural-guidelines)
  - [MVVM Pattern](#mvvm-pattern)
  - [Android 16 KB Page Alignment](#android-16-kb-page-alignment)
  - [Model Catalog Contributions](#model-catalog-contributions)
  - [Secrets & Configuration](#secrets--configuration)
- [Pull Request Process](#-pull-request-process)
- [Reporting Issues & Bugs](#-reporting-issues--bugs)
- [Style Guide & Conventions](#-style-guide--conventions)

---

## 🤝 Code of Conduct

We are committed to providing a welcoming, inclusive, and harassment-free environment for everyone. Please be respectful, considerate, and collaborative in all discussions, issues, and pull requests.

---

## 💡 How Can I Contribute?

1. **Report Bugs**: If you find an issue, submit a detailed bug report via GitHub Issues.
2. **Suggest Features**: Open a feature request to propose new capabilities or enhancements.
3. **Submit Pull Requests**: Fix bugs, refactor code, improve UI/UX, or add documentation.
4. **Propose Models**: Suggest modern, efficient GGUF models that run well locally on desktop or mobile hardware.

---

## 💻 Development Environment & Prerequisites

Before you start building KaiROS AI locally, ensure you have:

### General Requirements
- **Windows 10 (1903+)** or **Windows 11** (x64)
- **Git** installed and configured
- **Visual Studio 2022** (17.12+ recommended) or **VS Code** with C# Dev Kit

### Desktop Requirements (`KaiROS.AI.WinUI`)
- **.NET 9 SDK** ([Download](https://dotnet.microsoft.com/download/dotnet/9.0))
- **Windows App SDK** (version 1.8+)
- *(Optional)* **NVIDIA CUDA Toolkit 12** if developing CUDA acceleration features

### Mobile Requirements (`KaiROS.Mobile`)
- **.NET 10 SDK** ([Download](https://dotnet.microsoft.com/download/dotnet/10.0))
- **.NET MAUI Android Workload**:
  ```bash
  dotnet workload install maui-android
  ```
- **Android SDK / Command-line Tools** with API Level 34–36 support
- **Android Emulator** or physical device running Android 7.0+ (API 24+)

---

## 🛠️ Building the Projects

Clone the repository to your local machine:

```bash
git clone https://github.com/avikeid2007/KaiROS-AI.git
cd KaiROS-AI
```

### Desktop (WinUI 3)

The active desktop app is located in `KaiROS.AI.WinUI/`:

```bash
cd KaiROS.AI.WinUI
dotnet restore
dotnet build -c Debug
dotnet run -c Debug
```

To build a release package:
```bash
dotnet build -c Release -p:Platform=x64
```

### Mobile (Android - .NET MAUI)

The mobile app is located in `KaiROS.Mobile/`:

```bash
cd ../KaiROS.Mobile
dotnet restore
dotnet build -c Debug -f net10.0-android
```

To deploy and run on a connected Android device or running emulator:
```bash
adb devices
dotnet build -c Debug -f net10.0-android -t:Run
```

---

## 🏗️ Key Architectural Guidelines

### MVVM Pattern
- Both Desktop and Mobile projects use **CommunityToolkit.Mvvm**.
- Avoid code-behind UI logic in `.xaml.cs` files wherever possible. Use `[ObservableProperty]`, `[RelayCommand]`, and dependency injection through ViewModels.

### Android 16 KB Page Alignment
- Google Play mandates **16 KB memory page size support** for all apps targeting modern Android.
- All native shared libraries (`.so`) packaged into the Android APK/AAB must be ELF-aligned to at least `0x4000` (16 KB).
- The project includes an automated Roslyn MSBuild task (`AlignElfLibrariesTo16Kb`) and `BundleConfig.json` configured with `PAGE_ALIGNMENT_16K`.
- When adding or upgrading native NuGet dependencies (e.g., SQLite, LLamaSharp backends), verify 16 KB compliance before submitting.

### Model Catalog Contributions
- The desktop model catalog is defined in `KaiROS.AI.WinUI/appsettings.json`.
- The mobile model catalog is defined in `KaiROS.Mobile/Resources/Raw/model_catalog.json`.
- Any model added to the catalogs **must**:
  1. Be a valid **GGUF** format file (typically `Q4_K_M` or quantized for efficiency).
  2. Have a **public, unauthenticated direct-download URL** (no login or token required, e.g., Hugging Face direct `resolve/main/...` links).
  3. Support HTTP `Range` requests for resumable downloads.
  4. Specify accurate `SizeBytes`, `MinRamGB`, and appropriate descriptions.

### Secrets & Configuration
- **Never commit secrets**, tokens, or private DSN keys to git.
- Sentry telemetry DSNs and signing keys are injected dynamically during CI/CD release builds via environment variables.

---

## 🚀 Pull Request Process

1. **Fork the repository** and create a feature branch from `main`:
   ```bash
   git checkout -b feat/your-feature-name
   ```
2. **Follow conventional commit messages**:
   - `feat:` A new feature
   - `fix:` A bug fix
   - `docs:` Documentation changes
   - `refactor:` Code refactoring without behavioral changes
   - `perf:` Performance improvements
   - `chore:` Build scripts, dependencies, or tool updates
3. **Test locally**:
   - Ensure the solution builds with **0 errors**.
   - Test your changes on Windows and/or Android.
4. **Keep PRs focused**: Each pull request should address a single concern, bug, or feature.
5. **Open the PR**: Provide a clear description of the problem solved, changes made, and testing steps.

---

## 🐛 Reporting Issues & Bugs

When reporting an issue, please include:
- A descriptive title and summary.
- Steps to reproduce the behavior.
- Expected vs. actual behavior.
- System information:
  - **OS:** Windows 10/11 version or Android version & device model.
  - **Hardware:** CPU, Total RAM, and GPU (NVIDIA model, VRAM).
  - **Model:** Name and quantization of the LLM in use.
- Any relevant logs or screenshots.

---

## 🎨 Style Guide & Conventions

- Target **C# 12 / 13** language features (file-scoped namespaces, collection expressions, pattern matching).
- Use clear, descriptive naming for classes, properties, methods, and variables.
- Write readable, maintainable code with helpful XML documentation on public APIs and complex algorithms.

Thank you for helping make KaiROS AI better! ❤️
