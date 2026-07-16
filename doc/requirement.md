1. 项目概述
开发一个轻量级、跨平台的阿里云 OSS（对象存储）文件管理客户端。该工具旨在提供直观的图形界面，方便用户在 Windows、macOS 和 Linux 系统上管理 OSS 中的文件。客户端需支持多语言（简体中文、English），并允许用户动态切换。

2. 目标平台与技术栈
运行平台：Windows, macOS, Linux
运行环境：.NET 10 (利用 .NET 10 的高性能和最新 AOT 特性优化启动速度与内存占用)
开发语言：C# (使用 C# 13/14 最新特性)
UI 框架：Avalonia UI (v11.x，兼容 .NET 10)
OSS SDK：Aliyun.OSS.SDK.NetCore (NuGet 包)
架构模式：MVVM (使用 CommunityToolkit.Mvvm)
多语言方案：使用 .resx 资源文件 + Avalonia ResourceInclude 机制，支持运行时动态切换。
3. 核心功能需求
3.1 客户端配置与初始化
配置管理：用户可在设置界面输入 Endpoint、AccessKeyId、AccessKeySecret 和 Region。
本地持久化：将配置信息保存在本地 config.json 中，下次启动自动加载。
语言设置：配置中包含 Language 字段（如 zh-CN, en-US），应用启动时读取并应用对应语言。
3.2 存储空间管理
列举 Bucket：展示当前账号下的所有 Bucket 列表。
切换 Bucket：点击列表中的 Bucket 后，进入该 Bucket 的文件管理界面。
3.3 文件管理
列举文件：分页或滚动加载指定 Bucket 下的文件列表。展示文件名、大小、最后修改时间。
上传文件：支持选择本地文件上传，显示上传进度。
下载文件：选中文件下载至本地，显示下载进度。
删除文件：选中文件删除（二次确认）。
生成签名 URL：为私有文件生成带时效的预签名下载链接。
3.4 多语言支持
默认语言：首次启动跟随系统语言，默认提供简体中文 (zh-CN) 和英文 (en-US)。
动态切换：在顶部菜单或设置页提供语言下拉框，切换后整个应用界面语言立即更新，无需重启应用。
本地化格式：文件大小自动转换为 KB/MB/GB 显示，日期时间格式根据当前语言区域进行格式化。
4. 非功能性需求
异步编程：所有 OSS API 调用使用 async/await，避免阻塞 UI。
异常处理：捕获 OSS 异常，使用多语言文本进行弹窗 提示。
跨平台兼容：文件路径处理使用 Path.Combine。
5. 项目结构与多语言设计
建议项目命名为 OssClientPro，包含以下主要目录：
```text
OssClientPro/
├── Assets/
│   └── Languages/
│       ├── Strings.resx          # 默认语言 (如英文 fallback)
│       ├── Strings.zh-CN.resx     # 简体中文资源
│       └── Strings.en-US.resx     # 英文资源
├── Models/
│   ├── OssConfig.cs               # 包含 Language 属性
│   └── OssObjectItem.cs
├── Services/
│   ├── ConfigService.cs           # 读写 config.json
│   ├── LanguageService.cs         # 管理语言切换逻辑
│   └── OssService.cs              # 封装 OSS 操作
├── ViewModels/
│   ├── MainViewModel.cs
│   ├── SettingsViewModel.cs
│   └── FileListViewModel.cs
├── Views/
│   ├── MainWindow.axaml
│   └── ... (其他视图)
└── App.axaml / Program.cs
```
5.1 多语言实现逻辑建议
资源文件：在 Assets/Languages/ 下创建 .resx 文件，以键值对形式存储 UI 文本（如 btn_upload, msg_delete_confirm）。
Avalonia 绑定：在 .axaml 中使用 {Binding Language[btn_upload], Source={StaticResource Localization}}" 或类似机制绑定语言资源。
LanguageService：实现 INotifyPropertyChanged，当用户切换语言时，触发全局属性更改事件，通知所有绑定的 UI 元素刷新文本。
5.2 OssService.cs 核心方法签名建议

```csharp
public class OssService
{
    public void Initialize(OssConfig config);
    public Task<List<Bucket>> ListBucketsAsync();
    public Task<List<OssObjectItem>> ListObjectsAsync(string bucketName, string? prefix = null);
    public Task UploadFileAsync(string bucketName, string objectName, string localFilePath, IProgress<double> progress);
    public Task DownloadFileAsync(string bucketName, string objectName, string localFilePath, IProgress<double> progress);
    public Task DeleteFileAsync(string bucketName, string objectName);
    public string GeneratePresignedUrl(string bucketName, string objectName, int expireSeconds = 3600);
}
```
6. 开发步骤指引
环境搭建：创建 Avalonia MVVM 项目，安装 NuGet 包 (Aliyun.OSS.SDK.NetCore, CommunityToolkit.Mvvm, System.Text.Json)。
多语言骨架：创建 .resx 文件，搭建 LanguageService 并在 App.axaml 中注册全局资源。
服务层实现：完成 OssService.cs 和 ConfigService.cs 的所有方法。
UI 绑定：完成 MainWindow.axaml 布局，绑定多语言文本，包含左侧 Bucket 列表，右侧文件列表，顶部工具栏。
逻辑串联：在 ViewModel 中调用 Service，处理按钮点击事件、进度条更新及语言切换逻辑。