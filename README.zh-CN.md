# OssClientPro

> 轻量级跨平台阿里云 OSS 文件管理客户端

[🇺🇸 English](README.md)

## 功能

- **Bucket 管理**：列出 / 切换 Bucket，支持指定默认 Bucket 自动选中
- **文件操作**：上传、下载、删除、批量删除（复选框勾选）
- **授权路径**：配置 Bucket + 目录前缀，打开即限定在该路径下浏览
- **多语言**：简体中文 / English，运行时动态切换
- **搜索过滤**：按文件名实时搜索
- **文件预览**：工具栏按钮或右键菜单弹窗预览（图片、文本）
- **右键菜单**：每行文件支持右键上下文菜单，提供预览、编辑、删除、复制内容、复制文件名、复制文件链接、复制文件信息、下载功能
- **列排序**：点击列标题（文件名、大小、最后修改时间）进行升序/降序排序
- **签名链接**：为私有文件生成临时预签名下载 URL
- **安全存储**：AccessKey 凭据使用 Windows DPAPI 加密
- **操作日志**：所有操作记录到 `app.log`，崩溃写 `crash.log`

## 技术栈

| 项 | 说明 |
|----|------|
| 运行时 | .NET 10 |
| UI | Avalonia UI 12 |
| 架构 | MVVM (CommunityToolkit.Mvvm) |
| OSS API | 自研 OssHttpClient (HMAC-SHA1 签名 + XDocument 解析) |
| 编译 | Native AOT → 27MB 单文件 exe |
| 加密 | Windows DPAPI (ProtectedData) |

## 构建

```bash
# Debug 运行
dotnet run

# Release 发布 (单文件 NativeAOT)
dotnet publish -c Release -r win-x64
```

> 发布需要安装 Visual Studio **"Desktop development with C++"** 工作负载。

## 项目结构

```
OssClientPro/
├── Assets/Languages/         # 多语言 .resx 资源文件
├── Models/                   # OssConfig, OssObjectItem
├── Services/
│   ├── ConfigService.cs      # 配置读写 + DPAPI 加密
│   ├── LanguageService.cs    # 多语言引擎
│   ├── LogService.cs         # 文件日志 (app.log)
│   ├── OssHttpClient.cs      # OSS REST API 签名与解析
│   └── OssService.cs         # OSS 操作封装
├── Helpers/
│   └── OssExceptionHelper.cs # 异常 → 友好提示
├── ViewModels/               # MVVM ViewModels
├── Views/                    # Avalonia XAML 视图
└── App.axaml / Program.cs    # 入口
```
