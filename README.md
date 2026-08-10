# OssClientPro

> Lightweight cross-platform Aliyun OSS file manager

[🇨🇳 中文](README.zh-CN.md)

## Features

- **Bucket Management**: list / switch buckets, auto-select default bucket
- **File Operations**: upload, download, delete, batch delete (checkbox select)
- **Authorized Path**: configure Bucket + directory prefix to restrict browsing scope
- **i18n**: 简体中文 / English, runtime dynamic switching
- **Search**: real-time filter files by name
- **Preview**: inline file preview (images, text) via toolbar button or right-click context menu
- **Right-Click Menu**: per-row context menu with Preview, Edit, Delete, Copy Content, Copy File Name, Copy File Link, Copy File Info, Download
- **Column Sorting**: click column headers (Name, Size, Last Modified) to sort ascending / descending
- **Time Format**: 12h / 24h / auto (follow OS), configurable in Settings with live preview
- **Pre-signed URL**: generate temporary download links for private files
- **Encryption**: AccessKey credentials encrypted via Windows DPAPI
- **Logging**: all operations logged to `app.log`, crash reports to `crash.log`

## Tech Stack

| Item | Description |
|------|-------------|
| Runtime | .NET 10 |
| UI | Avalonia UI 12 |
| Pattern | MVVM (CommunityToolkit.Mvvm) |
| OSS API | Custom OssHttpClient (HMAC-SHA1 signing + XDocument) |
| Compilation | Native AOT → 27MB single-file exe |
| Encryption | Windows DPAPI (ProtectedData) |

## Build

```bash
# Debug
dotnet run

# Release (single-file NativeAOT)
dotnet publish -c Release -r win-x64
```

> Publishing requires the **"Desktop development with C++"** Visual Studio workload.

## Structure

```
OssClientPro/
├── Assets/Languages/         # i18n .resx files
├── Models/                   # OssConfig, OssObjectItem
├── Services/
│   ├── ConfigService.cs      # Config persistence + DPAPI encryption
│   ├── LanguageService.cs    # Multi-language engine
│   ├── LogService.cs         # File logger (app.log)
│   ├── OssHttpClient.cs      # OSS REST API signing & parsing
│   └── OssService.cs         # OSS operation wrapper
├── Helpers/
│   └── OssExceptionHelper.cs # Exception → friendly messages
├── ViewModels/               # MVVM ViewModels
├── Views/                    # Avalonia XAML views
└── App.axaml / Program.cs    # Entry point
```
