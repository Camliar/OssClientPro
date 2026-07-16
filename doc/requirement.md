1. 项目概述
开发一个轻量级、跨平台的阿里云 OSS（对象存储）文件管理客户端。该工具旨在提供直观的图形界面，方便用户在 Windows、macOS 和 Linux 系统上管理 OSS 中的文件。客户端需支持多语言（简体中文、English），并允许用户动态切换。

2. 目标平台与技术栈
运行平台：Windows, macOS, Linux
运行环境：.NET 10 (利用 .NET 10 的高性能和最新 AOT 特性优化启动速度与内存占用)
开发语言：C# (使用 C# 13/14 最新特性)
UI 框架：Avalonia UI (v12.x，兼容 .NET 10)
OSS SDK：自研 OssHttpClient（直接调 REST API，HMAC-SHA1 签名 + XDocument 解析，NativeAOT 兼容）
架构模式：MVVM (使用 CommunityToolkit.Mvvm)
多语言方案：使用 .resx 资源文件 + Avalonia ResourceInclude 机制，支持运行时动态切换。
编译发布：Native AOT → 单文件 (~27MB exe)，无需 .NET 运行时

3. 核心功能需求
3.1 客户端配置与初始化
配置管理：用户可在设置界面输入 Endpoint、AccessKeyId、AccessKeySecret 和 Region。
本地持久化：将配置信息保存在本地 config.json 中，下次启动自动加载。
语言设置：配置中包含 Language 字段（如 zh-CN, en-US），应用启动时读取并应用对应语言。
凭据加密：AccessKeyId 和 AccessKeySecret 使用 Windows DPAPI 加密存储，仅当前用户可解密。
自动连接：如果已有有效配置文件，启动时跳过设置页面直接连接。

3.2 存储空间管理
默认 Bucket：支持在设置中指定默认 Bucket，连接后自动选中（避免无 ListBuckets 权限时报错）。
列举 Bucket：展示当前账号下的所有 Bucket 列表（未配置默认 Bucket 时）。
切换 Bucket：点击列表中的 Bucket 后，进入该 Bucket 的文件管理界面。
授权路径：可配置 Bucket + 目录前缀（如 data/project-a/），打开即限定在该路径下浏览；上传时自动归入该路径。

3.3 文件管理
列举文件：分页或滚动加载指定 Bucket 下的文件列表。展示文件名、大小、最后修改时间。
上传文件：支持选择本地文件上传，显示上传进度。
下载文件：选中文件下载至本地，显示下载进度。
批量删除：复选框勾选多个文件 + 全选/取消全选，二次确认后批量删除。
搜索过滤：按文件名实时搜索，搜索框带 🔍 图标，占满工具栏剩余宽度。
生成签名 URL：为私有文件生成带时效的预签名下载链接，自动复制到剪贴板。
状态栏：显示已选数量、文件总数、上次刷新时间。

3.4 多语言支持
默认语言：首次启动跟随系统语言，默认提供简体中文 (zh-CN) 和英文 (en-US)。
动态切换：在顶部菜单或设置页提供语言下拉框，切换后整个应用界面语言立即更新，无需重启应用。
本地化格式：文件大小自动转换为 KB/MB/GB 显示，日期时间格式根据当前语言区域进行格式化。

3.5 日志与异常处理
操作日志：所有 OSS 操作（连接、列表、上传、下载、删除、签名链接）自动记录到 app.log，超过 5MB 自动轮转。
崩溃日志：全局异常捕获，崩溃时写入 crash.log。
友好错误提示：OSS 权限/认证/网络错误自动映射为本地化友好消息（不弹异常窗口）。

4. 非功能性需求
异步编程：所有 OSS API 调用使用 async/await，避免阻塞 UI。
异常处理：捕获 OSS 异常，使用多语言文本进行提示。
跨平台兼容：文件路径处理使用 Path.Combine。
安全存储：敏感凭据 DPAPI 加密，config.json 通过 .gitignore 排除。
日志轮转：app.log 超过 5MB 自动备份为 .old 后重建。

5. 项目结构与多语言设计
建议项目命名为 OssClientPro，包含以下主要目录：
```text
OssClientPro/
├── Assets/
│   └── Languages/
│       ├── Strings.resx          # 默认语言 (如英文 fallback)
│       ├── Strings.zh-CN.resx     # 简体中文资源
│       └── Strings.en-US.resx     # 英文资源
├── Helpers/
│   └── OssExceptionHelper.cs     # OSS 异常 → 友好提示
├── Models/
│   ├── OssConfig.cs               # 包含 Language、DefaultBucket、BasePrefix 属性
│   ├── OssObjectItem.cs           # 包含 IsSelected 复选框属性
│   └── OssConfigSerializerContext.cs  # AOT 兼容的 JSON 源生成器
├── Services/
│   ├── ConfigService.cs           # 读写 config.json + DPAPI 加密
│   ├── LanguageService.cs         # 管理语言切换逻辑
│   ├── LogService.cs              # 文件日志 (app.log)
│   ├── OssHttpClient.cs           # OSS REST API 签名与解析 (NativeAOT 兼容)
│   └── OssService.cs              # 封装 OSS 操作
├── ViewModels/
│   ├── MainViewModel.cs
│   ├── SettingsViewModel.cs
│   └── FileListViewModel.cs       # 含搜索、批量选择、状态栏统计
├── Views/
│   ├── MainWindow.axaml            # 含 DataGrid + 搜索框 + 状态栏
│   └── MainWindow.axaml.cs
├── App.axaml / Program.cs          # 启动 + 全局崩溃处理
├── README.md                       # 英文文档
└── README.zh-CN.md                 # 中文文档
```
5.1 多语言实现逻辑
资源文件：在 Assets/Languages/ 下创建 .resx 文件，以键值对形式存储 UI 文本（如 btn_upload, msg_delete_confirm）。
Avalonia 绑定：在 .axaml 中使用 {Binding LanguageService[btn_upload]} 绑定语言资源。
LanguageService：实现 INotifyPropertyChanged，当用户切换语言时，触发全局属性更改事件，通知所有绑定的 UI 元素刷新文本。

5.2 OssService.cs 核心方法签名

```csharp
public class OssService
{
    public void Initialize(OssConfig config);
    public Task<List<string>> ListBucketsAsync();
    public Task<List<OssObjectItem>> ListObjectsAsync(string bucketName, string? prefix = null);
    public Task UploadFileAsync(string bucketName, string objectName, string localFilePath, IProgress<double> progress);
    public Task DownloadFileAsync(string bucketName, string objectName, string localFilePath, IProgress<double> progress);
    public Task DeleteFileAsync(string bucketName, string objectName);
    public string GeneratePresignedUrl(string bucketName, string objectName, int expireSeconds = 3600);
}
```

6. 开发步骤指引
环境搭建：创建 Avalonia MVVM 项目，安装 NuGet 包。
多语言骨架：创建 .resx 文件，搭建 LanguageService 并在 App.axaml 中注册全局资源。
服务层实现：完成 OssHttpClient.cs（OSS REST API）、OssService.cs 和 ConfigService.cs（含加密）的所有方法。
UI 绑定：完成 MainWindow.axaml 布局，绑定多语言文本，包含左侧 Bucket 列表，右侧文件列表（含复选框列 + 搜索框），顶部工具栏，底部状态栏。
逻辑串联：在 ViewModel 中调用 Service，处理按钮点击事件、进度条更新、批量删除及语言切换逻辑。
发布打包：Native AOT 编译为单文件 exe，安装 VS C++ 工具链后执行 dotnet publish -c Release -r win-x64。
