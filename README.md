# OssClientPro

> 轻量级跨平台阿里云 OSS 文件管理客户端  
> Lightweight cross-platform Aliyun OSS file manager

## 功能 / Features

- **Bucket 管理 / Bucket Management**：列出 / 切换 Bucket，支持指定默认 Bucket
- **文件管理 / File Operations**：上传、下载、删除、批量删除
- **授权路径 / Authorized Path**：可配置 Bucket + 目录前缀，限定浏览范围
- **多语言 / i18n**：简体中文 / English，运行时动态切换
- **搜索过滤 / Search**：按文件名实时过滤
- **签名链接 / Pre-signed URL**：为私有文件生成临时下载链接
- **安全存储 / Encryption**：AccessKey 凭据使用 Windows DPAPI 加密

## 技术栈 / Tech Stack

| 项 Item | 说明 Description |
|----------|-------------------|
| 运行时 Runtime | .NET 10 |
| UI | Avalonia UI 12 |
| 架构 Pattern | MVVM (CommunityToolkit.Mvvm) |
| OSS API | 自研 OssHttpClient / Custom HMAC-SHA1 + XDocument |
| 编译 Compilation | Native AOT → 27MB 单文件 exe |
| 加密 Encryption | Windows DPAPI (ProtectedData) |

## 构建 / Build

```bash
# Debug
dotnet run

# Release (单文件 NativeAOT / single-file)
dotnet publish -c Release -r win-x64
```

> 发布需要安装 Visual Studio "Desktop development with C++" 工作负载  
> Publishing requires the "Desktop development with C++" VS workload

## 项目结构 / Structure

```
OssClientPro/
├── Assets/Languages/         # 多语言资源 / i18n .resx files
├── Models/                   # OssConfig, OssObjectItem
├── Services/
│   ├── ConfigService.cs      # 配置读写 + DPAPI 加密
│   ├── LanguageService.cs    # 多语言引擎 / language engine
│   ├── LogService.cs         # 操作日志 / file logger
│   ├── OssHttpClient.cs      # OSS REST API 签名与解析
│   └── OssService.cs         # OSS 操作封装
├── Helpers/
│   └── OssExceptionHelper.cs # 异常 → 友好消息
├── ViewModels/               # MVVM ViewModels
├── Views/                    # Avalonia XAML 视图
└── App.axaml / Program.cs    # 入口 / entry point
```
