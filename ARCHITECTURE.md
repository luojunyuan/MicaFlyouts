# Mica Flyouts 架构说明

本文是 `Mica Flyouts` 的实现约束和维护指南。项目是原
`FluentFlyout` WPF 应用的纯移植：用户界面、视觉样式、布局、快捷键、窗口行为、
动画时序和功能语义必须保持一致，变化仅限于底层技术栈改为 WinUI 3 +
Microsoft UI Reactor。原项目位于 `C:\Users\kimika\source\repos\FluentFlyout`；
遇到本文没有明确列出的视觉或交互细节时，以原项目的 XAML 和窗口代码为准。

内购功能不属于新项目：不迁移 Store、授权、产品 ID、价格、购买流程、Premium
文案或付费限制。原来被 Premium 限制的非付费能力在新项目中直接可用。

## 1. 技术基线

| 项目 | 约束 |
| --- | --- |
| 目标框架 | `net10.0-windows10.0.22621.0` |
| UI | WinUI 3，`UseWinUI=true` |
| Reactor | `Microsoft.UI.Reactor 0.1.0-preview.13` |
| Windows App SDK | 只允许 `Microsoft.WindowsAppSDK.WinUI`；当前中央版本为 `2.3.6`，必须保持单一版本并验证与 Reactor preview.13 的兼容性 |
| 设置控件 | `CommunityToolkit.WinUI.Controls.SettingsControls 8.3.260402-preview2`；社区控件必须使用 preview 包，不得混用稳定旧包 |
| 托盘 | `H.NotifyIcon.WinUI 2.5.0-dev.2`，由项目内 `HNotifyIconTray` 封装 |
| 音频 | `NAudio 2.3.0`，只允许出现在 `Infrastructure/Audio` |
| Win32 | `Microsoft.Windows.CsWin32 0.3.298`，所有 Win32 P/Invoke 由生成代码提供 |
| 打包 | `WindowsPackageType=None`，`WindowsAppSDKSelfContained=true`，不生成 MSIX，不依赖 Store |
| 架构 | x64、ARM64；运行时标识为 `win-x64`、`win-arm64` |

项目必须保持以下属性，不得为了消除警告而关闭它们：

```xml
<CsWin32RunAsBuildTask>true</CsWin32RunAsBuildTask>
<DisableRuntimeMarshalling>true</DisableRuntimeMarshalling>
<PublishAot>true</PublishAot>
<PublishTrimmed>true</PublishTrimmed>
<TrimMode>full</TrimMode>
<IsAotCompatible>true</IsAotCompatible>
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
<CsWinRTAotOptimizerEnabled>true</CsWinRTAotOptimizerEnabled>
```

源码中禁止出现 `[DllImport]`、`[LibraryImport]`、手写 `extern`、WPF 类型、
`dynamic`、`Activator.CreateInstance`、运行时程序集扫描和依赖反射自动发现的
UI 表单。

## 2. 项目布局

构建产物 `bin/`、`obj/` 不属于源码结构，下面列出需要维护的文件：

```text
MicaFlyouts/
├─ MicaFlyouts.slnx
├─ Directory.Build.props          # 全局分析、中央包管理和语言版本
├─ Directory.Build.targets        # WinUI NativeAOT 资源复制
├─ Directory.Packages.props       # 所有 NuGet 版本的唯一来源
├─ nuget.config
├─ src/MicaFlyouts/
│  ├─ MicaFlyouts.csproj
│  ├─ Program.cs
│  ├─ AppRuntime.cs
│  ├─ app.manifest
│  ├─ App/
│  │  ├─ Bootstrap.cs
│  │  ├─ AppServices.cs
│  │  ├─ AppCommands.cs
│  │  ├─ WindowRegistry.cs
│  │  ├─ UiDispatcher.cs
│  │  └─ HNotifyIconTray.cs
│  ├─ Domain/
│  │  ├─ Geometry.cs
│  │  ├─ StateStore.cs
│  │  ├─ Settings/
│  │  ├─ Media/
│  │  ├─ Volume/
│  │  ├─ LockKeys/
│  │  ├─ Taskbar/
│  │  ├─ Visualizer/
│  │  ├─ Flyouts/
│  │  ├─ Localization/
│  │  ├─ Onboarding/
│  │  ├─ Updates/
│  │  └─ Windows/
│  ├─ Infrastructure/
│  │  ├─ State/
│  │  ├─ Settings/
│  │  ├─ Media/
│  │  ├─ Audio/
│  │  ├─ Interop/
│  │  ├─ Windows/
│  │  ├─ Localization/
│  │  ├─ Notifications/
│  │  ├─ Updates/
│  │  └─ Logging/
│  ├─ Features/
│  │  ├─ Media/
│  │  ├─ Volume/
│  │  ├─ NextUp/
│  │  ├─ LockKeys/
│  │  ├─ Taskbar/
│  │  ├─ Settings/
│  │  └─ Onboarding/
│  ├─ UI/
│  │  ├─ Components/
│  │  ├─ Theme/
│  │  ├─ Animation/
│  │  └─ Toolkit/
│  ├─ Assets/
│  └─ Strings/
└─ tests/
   ├─ MicaFlyouts.Tests/
   ├─ MicaFlyouts.ReactorSelfTests/
   └─ MicaFlyouts.UITests/
```

依赖方向固定为：

```text
Domain  <-  Infrastructure  <-  App
   ^              ^             ^
   └────────── Features ────────┘
                         ^
                         └──── UI（只提供可复用视图构件）
```

`Domain` 不引用 WinUI、Reactor、NAudio、Windows App SDK 或任何 HWND/API。
`Infrastructure` 可以引用平台库，但不能持有 Reactor 控件。`Features` 是唯一
组合状态和视图的业务层。`App` 负责组合根和生命周期，不承载具体页面布局。

## 3. 启动和生命周期

### `Program.cs` 与 `AppRuntime.cs`

`Program.Main` 只设置 `[STAThread]` 并调用 `AppRuntime.Start()`。启动流程必须是：

1. `SingleInstanceService.Acquire()` 创建 `MicaFlyouts` 互斥体。
2. 非首实例只向已有实例发送 `MicaFlyouts_OpenSettings` 事件，然后立即退出。
3. `Bootstrap.Create` 手动构造所有 stores、平台服务和 controller；不引入 DI 容器。
4. 注册 Reactor 内置控件，设置 `ReactorApp.ShutdownPolicy=Explicit`。
5. 通过 `ReactorApp.Run` 打开主媒体窗口，再启动后台服务和托盘。
6. 首次运行打开三步引导；后续运行只保持后台监听。

`AppServices` 是应用组合根，持有所有服务的强引用，并暴露显式命令：打开设置、
显示/切换媒体浮层、退出、媒体控制、音量控制和锁定键窗口。它订阅媒体与设置
快照，负责把后台事件转换成 UI 线程操作。

组件渲染统一通过非空的 `AppRuntime.Services` 访问服务；运行时未初始化或已结束时
抛出 `InvalidOperationException`，不以 `Empty()` 隐藏生命周期错误。可空的
`AppRuntime.Current` 仅用于异步收尾等允许服务不存在的场景。所有 hooks 必须在
业务空状态的提前返回之前调用，不能通过运行时判空跳过 hooks。

托盘由 `AppServices.Tray.cs` 协调：`NIconSymbol` 选择彩色或黑白图标，黑白图标跟随
Windows 任务栏主题；设置变化即时应用显示/隐藏及图标选择。菜单通过 `TrayMenu`
读取现有 `TrayIcon_*Option` 资源，订阅 `LocalizationStore` 在 UI 线程刷新文案、
RTL 和字体；切换语言时安全重建托盘菜单宿主，避免 H.NotifyIcon 的 SecondWindow
菜单副本保留旧条目。资源出处、图标映射及许可证见
`Assets/TrayIcons/README.md`。退出前解除主题/语言订阅并释放托盘宿主。

关闭顺序必须与启动相反：停止键盘 hook、任务栏、Visualizer、音频和媒体监听，
注销更新/通知，释放托盘宿主，关闭 `WindowRegistry` 中所有窗口，最后释放 stores、
单实例和日志。所有关闭路径必须幂等。

### `WindowRegistry`

`WindowRegistry` 用 `WindowKey` 到 `ReactorWindow` 的字典去重窗口。打开已有 key
时只 `Show/Activate`，不重新挂载组件；窗口的 `Closed` 事件移除字典项。当前 key
约定如下：

| key | 用途 |
| --- | --- |
| `main-media` | 主媒体浮层 |
| `volume` | 硬件音量浮层 |
| `volume-mixer` | 展开的应用混音器 |
| `next-up` | 下一曲提示 |
| `lock-keys` | 锁定键提示 |
| `taskbar-widget` | 任务栏媒体 Widget |
| `taskbar-visualizer` | 任务栏 Visualizer |
| `settings` | 设置窗口 |
| `onboarding` | 首次引导 |

### 窗口规格

所有窗口通过 `ReactorApp.OpenWindow(WindowSpec, factory)` 创建。浮层使用
`WindowStyle.None`、`WindowLevel.AlwaysOnTop`、`ResizeMode.NoResize`、
`ShowInTaskbar=false`、`ShowInSwitcher=false`、`NoActivate=true`、圆角和
`BackdropKind.DesktopAcrylic`；设置和引导窗口使用 Mica backdrop。

| 窗口 | 初始尺寸 | 位置和备注 |
| --- | --- | --- |
| 主媒体浮层 | 310×116 | 手动初始位置为屏幕外；显示时按六种位置计算，支持紧凑布局、封面背景和进度条 |
| 音量浮层 | 240×50 | 可在主媒体浮层上方或屏幕底部显示；悬停/冷却/自动隐藏保持原逻辑 |
| 应用混音器 | 420×420，最小 320×240 | 普通 Mica 窗口，列出主音量和应用会话 |
| Next Up | 310×50，最大 400 宽 | 标题/艺术家动态测量，当前标题变化时复用同一 key |
| Lock Keys | 160×50 | 按本地化文本扩展宽度，跟随首选显示器 |
| Taskbar Widget | 100×40 | 初始屏幕外，随后 reparent 到 Explorer 任务栏 |
| Taskbar Visualizer | 84×40 | 与 Widget 独立 reparent，支持左右/上下布局 |
| 设置 | 900×700，最小 750×300 | `NavigationView`、搜索、深链接和页面滚动 |
| 引导 | 900×600，最小 870×500 | 仅 Media、Volume、Lock Keys 三步 |

窗口位置使用 DIP 与物理像素转换：`WindowSpec` 的尺寸交给 Reactor，任务栏和
监视器 API 使用物理像素；转换只能在 `Infrastructure/Interop` 或
`MonitorService` 中进行，不能把 HWND 或 DPI 计算泄漏到 Domain。

## 4. 状态管理

### Store 契约

```csharp
public interface IStateStore<out TSnapshot>
{
    TSnapshot Snapshot { get; }
    Action Subscribe(Action listener);
}
```

`Infrastructure/State/StateStore<TSnapshot>` 使用不可变快照、原子替换和
copy-on-write 订阅者列表；`SetSnapshot` 和 `Update` 都拒绝 null，并在 Dispose
后抛出 `ObjectDisposedException`。面向功能的 `SnapshotStore<T>` 只做薄封装。

当前 stores：

- `SettingsStore`：设置快照和持久化。
- `MediaStore`：所有会话、当前 focused session、活动曲目和下一曲候选。
- `VolumeStore`：默认输出设备、主音量、静音和应用会话。
- `LockKeyStore`：Caps/Num/Scroll/Insert 状态。
- `TaskbarStore`：目标任务栏、方向、矩形、Explorer 可用性和显示内容。
- `VisualizerStore`：柱状图、baseline、peak 和内容可见性。
- `LocalizationStore`：语言、RTL、字体和资源版本。
- `OnboardingStore`：当前步骤、完成步骤和完成标志。
- `UpdateStore`：版本检查结果。

Reactor 组件只能通过 `UseExternalStore` 订阅：

```csharp
var snapshot = UseExternalStore(
    services.MediaStore.Subscribe,
    () => services.MediaStore.Snapshot);
```

后台线程产生的快照先调用 `UiDispatcher.EnqueueOrRun`，再更新 store。组件不
直接订阅 NAudio、GSMTC、Win32 或文件事件。设置修改统一使用
`SettingsStore.Update(s => s with { ... })`，由设置订阅者触发主题、语言、启动项、
任务栏和窗口副作用。

## 5. Domain 模型

`Domain` 中的 record 是跨线程传递的稳定数据契约：

- `SettingsSnapshot`：所有可持久化设置，使用 init-only 属性。
- `MediaSessionSnapshot`、`MediaTrackSnapshot`、`MediaTimelineSnapshot`：不包含
  WinRT session 引用；媒体控制通过 Infrastructure service 执行。
- `VolumeSnapshot`、`ApplicationVolumeSnapshot`：不暴露 `MMDevice` 或
  `AudioSessionControl`。
- `TaskbarSnapshot`、`TaskbarLayoutInput/Result`：使用 `PixelRect` 和明确的方向。
- `LockKeySnapshot`、`LockKeyVisualState`：包含纯计算所需的开关状态。
- `VisualizerSnapshot`：只含不可变柱值、baseline 和时间戳。
- `LocalizationSnapshot`、`OnboardingSnapshot`、`UpdateSnapshot`。

纯函数必须留在 Domain：

- `MediaRules`：应用过滤、focused session 选择、Next Up 去重、时间线格式化。
- `FlyoutPositionCalculator`：六种位置、底部边距和音量 OSD 预留。
- `TaskbarLayoutCalculator`：水平/垂直任务栏、DPI、Widget/native padding、
  legacy width、固定宽度和 Visualizer 相邻位置。
- `LockKeyLayout`：最小宽度、开启/关闭 opacity、指示条宽度和文本。
- `FftProcessor`：4096 点 FFT、Hamming window、灵敏度、peak 衰减和 bar 数量。
- `SettingsValidator.Normalize`：范围修正、列表清洗、语言/字体默认值和 UUID。

任何纯函数不得读取 `AppRuntime.Current`、静态窗口、系统设置或当前线程 UI。

## 6. Infrastructure 服务

### 媒体

`MediaSessionService` 直接使用 `Windows.Media.Control`，维护 GSMTC 会话订阅，
将 WinRT 对象转换为 Domain 快照。它负责：

- 获取所有会话并选择 focused session；跳过关闭会话。
- 应用黑名单/白名单：显示名全等匹配，AppUserModelId 使用不区分大小写的子串，
  空条目永不匹配。
- 读取标题、艺术家、专辑、封面、播放状态、循环/随机、控制能力和时间线。
- 执行上一首、播放/暂停、下一首、循环、随机、seek 和播放器刷新。
- 根据设置暂停其他会话、抑制全屏应用、控制进度刷新和 Next Up 候选。

`MediaPlayerResolver` 只使用静态 `Process` 和显式类型，不使用原项目的 dynamic
Shell fallback。浏览器或 Shell 激活若需要 UI Automation，必须使用源生成 COM
接口/ComWrappers，并保持在 Infrastructure 边界内。

### 音量

`AudioService` 对外只提供 Domain 快照和命令。NAudio 仅允许在
`CoreAudioAdapter`、`WasapiLoopbackCaptureAdapter` 和相关适配器中使用。服务负责：

- 延迟初始化默认 Render endpoint，并在设备不可用时周期重试。
- 读取/设置主音量和静音。
- 枚举应用音频会话，设置每个应用音量和静音。
- 监听默认设备变化，刷新快照。
- 隐藏和恢复系统音量 OSD。
- 硬件音量键事件与媒体浮层的排列、悬停延迟和 500ms 冷却。

设备、COM 和 NAudio 异常必须降级为空快照或记录日志，不能让 hook 回调或 UI
线程崩溃。

### Visualizer

`VisualizerService` 使用 WASAPI loopback 采样，经过 `FftProcessor` 处理后写入
有界 `Channel`（容量 2，丢弃旧帧），再由 UI dispatcher 更新 `VisualizerStore`。
默认 30 FPS、4096 FFT；设备切换、登录/解锁、睡眠恢复和无回调 watchdog 都要能
重新建立采集。UI 只消费 `VisualizerSnapshot`，不得在 Reactor render 中运行 FFT。

### 键盘和锁定键

`KeyboardHookService` 是全局低级键盘 hook，监听 Caps Lock、Num Lock、Scroll Lock、
Insert、媒体键和音量键。hook callback 是静态
`[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]` 函数指针，服务实例
通过静态 rooted 字段保持到卸载完成；回调异常必须被吞掉并继续调用
`CallNextHookEx`。Lock Keys 组件只收到 `LockKeySnapshot`。

### 监视器、全屏和启动项

- `MonitorService` 使用 CsWin32 枚举监视器、工作区、DPI 和主显示器。
- `FullscreenService` 使用 `SHQueryUserNotificationState` 检查 D3D 全屏/忙碌状态。
- `StartupRegistration` 只操作当前用户 Run key；失败不阻止应用启动。
- `SingleInstanceService` 使用互斥体和 `MicaFlyouts_OpenSettings` 自动重置事件。

### 任务栏

`TaskbarHostService` 只负责原生 HWND parent、尺寸和位置；React 内容仍由对应的
`ReactorWindow` 持有。它周期检查 Explorer，并在任务栏句柄改变后重新 attach。

`TaskbarAutomationService` 使用最小的 source-generated UI Automation COM 投影，
查找 `TaskbarFrame`、`WidgetsButton` 和 `SystemTrayIcon` 的 bounding rectangle；
失败时回退到 `GetWindowRect`。必须支持：

- `Shell_TrayWnd` 和所有 `Shell_SecondaryTrayWnd`。
- 主/副显示器选择、垂直任务栏和每窗口 DPI。
- native Widgets padding、手动 padding、legacy width 和固定宽度。
- Explorer 重启后的 parent、区域、尺寸和位置恢复。
- Widget 标题滚动、暂停覆盖层、控件位置和 Visualizer 相邻关系。

任务栏服务不能在后台线程调用 `ReactorWindow.Show/SetOpacity/SetPosition`；
发现变化后排队到 UI dispatcher。

### 通知、更新和日志

通知使用 Windows App SDK `Microsoft.Windows.AppNotifications`。更新检查结果写入
`UpdateStore`，通知失败只记录日志。`AppLogger` 封装日志目录和级别，服务不得
依赖 NLog/WPF 全局状态。所有外部进程、文件、Shell 和网络错误都应是可恢复的。

## 7. Reactor Features 与 UI

### 组件拆分

每个 Feature 由一个窗口根组件和一个纯视图组件组成，窗口根只返回功能组件：

| 目录/类型 | 职责 |
| --- | --- |
| `MediaFlyoutComponent` | 封面、标题、艺术家、播放控制、循环/随机和进度条 |
| `VolumeFlyoutComponent` | 主音量滑块、静音、打开混音器 |
| `VolumeMixerComponent` | 主音量和应用会话列表，使用 session key 做稳定 key |
| `NextUpComponent` | 38px 封面、下一曲标题/艺术家和短时提示 |
| `LockKeysComponent` | Lock Keys 文本、状态 opacity、指示条和动画状态 |
| `TaskbarWidgetComponent` | 28px 封面、标题/艺术家、固定宽度和暂停显示 |
| `TaskbarVisualizerComponent` | 固定 84×40 surface、bar、baseline 和 accent |
| `SettingsWindowComponent` | NavigationView、搜索、深链接和页面分发 |
| `OnboardingComponent` | Media/Volume/Lock Keys 三步引导和设置回写 |

设置页当前集中在 `SettingsWindowComponent` 中，以显式 `SettingsPage` 枚举、静态
`SettingsSearchIndex` 和页面函数维护。若文件继续增长，可以按页面拆成同一目录的
`SettingsPageComponent`，但不能引入反射式页面注册或通用 ViewModel 层。

### UI 公共构件

- `FlyoutSurface`：统一圆角、边框、padding、背景层和 surface token。
- `IconButton`：固定 28×28，使用 Segoe Fluent glyph，并设置 AutomationName。
- `CoverImage`：从 byte[] 异步生成 WinUI `BitmapImage`；无图时显示音乐图标。
- `TextComponents`：封面标题、艺术家和任务栏标题的省略/滚动文本。
- `ThemeTokens`：映射 Reactor `ThemeRef`，禁止在 Feature 中散落颜色常量。
- `WindowAnimationCoordinator`：统一窗口目标位置、opacity、topmost 和隐藏。

Reactor DSL 中必须为列表项、进度条、封面和 session 行设置稳定 key。控件事件
直接调用 `AppServices` 或服务命令；不要把 native session 对象放进 Element props。

### SettingsControls wrapper

`UI/Toolkit/SettingsWrappers.cs` 使用源生成包装器：

```csharp
[GenerateReactorWrapper(
    typeof(CommunityToolkit.WinUI.Controls.SettingsCard),
    Exclude = new[] { "Command", "CommandParameter" })]
[WrapElementSlot("HeaderIcon")]
public partial record SettingsCardElement;

[GenerateReactorWrapper(typeof(CommunityToolkit.WinUI.Controls.SettingsExpander))]
public partial record SettingsExpanderElement;
```

启动时调用 `ReactorApp.TryRegisterControlAssembly(typeof(SettingsCard).Assembly)`。
不要使用 `AutoColumns<T>`、PropertyGrid、POCO `UseObservable` 或其它运行时反射
路径，因为这些路径不满足 NativeAOT。

### 视觉和动画

所有视觉值必须从原 WPF XAML/代码迁移，包括：

- 主浮层的 310×116 布局、6px 封面圆角、三种封面背景模糊和 opacity mask。
- Desktop Acrylic/Mica、边框 alpha、文本字号/opacity、按钮间距和紧凑布局。
- 主浮层底部六种位置、音量 OSD 预留边距（普通 80px、上方排列 16px）。
- Show/Hide 的 cubic ease-out、窗口 opacity 和位置偏移。
- Lock Keys 开启时 opacity 1/指示条宽 60，关闭时 opacity 0.2/宽 36，shackle
  rotation 和 bounce keyframe。
- 任务栏标题滚动、暂停覆盖层、Visualizer bar 高度、baseline 和自动隐藏。

优先使用 Reactor 的 `.Transition()`、`.Animate()`、`.Keyframes()` 和 Composition
动画。窗口级移动/透明度由 `WindowAnimationCoordinator` 统一处理；不能把动画
改为瞬时跳变，也不能因为换框架而调整尺寸或间距。

## 8. 设置和本地化

### JSON 设置

路径为 `%AppData%\MicaFlyouts\settings.json`。`SettingsSnapshot` 使用
`schemaVersion=1`、camelCase 和 `System.Text.Json` source generation：

- 缺失字段使用属性默认值；未知字段忽略。
- 加载后调用 `SettingsValidator.Normalize`。
- 时长范围 `0..10000`；手动任务栏 padding `-9999..9999`；滚动速度 `1..100`；
  Visualizer 灵敏度、peak `1..3`；bar 数 `1..128`。
- 保存防抖 500ms，先写 `.tmp`，再原子替换并保留 `.bak`。
- 导出排除安装 UUID；导入保留当前 UUID，并再次执行 normalize。
- 不迁移原 `FluentFlyout\settings.xml`。

设置字段按功能分组，但 JSON 字段名保持原属性语义：Media、Volume、Lock Keys、
Taskbar、Visualizer、System、App Filtering 和 Advanced。`Uuid` 是内部持久化字段；
Session ID、Visualizer 当前内容和其它运行时状态不写入文件。

### 本地化

`Strings/<language>/App.resw` 保留原有语言：

```text
ar, ca, cs, de, en-US, es, fi, fr, he, hi, hr, hu, id, it, ja, ko, nl,
pl, pt-BR, ru, si, sk, ta, th, tr, uk, vi, zh-CN, zh-TW
```

`LocalizationService` 使用 Reactor `ReswResourceProvider`，`LocaleProvider` 包裹
根组件，组件通过 `UseIntl()` 或显式 `MessageKey` 读取字符串。`ar`、`he` 必须设置
RTL，并保留原语言字体 fallback。设置搜索和菜单使用静态索引，避免运行时扫描。

## 9. CsWin32 和 NativeAOT 规则

`Infrastructure/Interop/NativeMethods.txt` 是 Win32 API 的唯一声明清单，且必须在
`.csproj` 中作为 `AdditionalFiles` 显式加入。当前覆盖窗口、监视器、DPI、Shell
hook、键盘 hook、DWM/GDI、线程/事件等待、UI Automation 创建和系统通知状态等
API，包括：

```text
FindWindow, FindWindowEx, EnumWindows, EnumThreadWindows, GetClassName,
SetParent, GetParent, GetWindowRect, SetWindowRgn, SetWindowPos, ShowWindow,
GetWindowLongPtr, SetWindowLongPtr, GetWindowThreadProcessId,
SetForegroundWindow, IsIconic, GetDpiForWindow, GetDpiForMonitor,
EnumDisplayMonitors, EnumDisplayDevices, GetMonitorInfo, MonitorFromWindow,
MonitorFromPoint, ScreenToClient, RegisterShellHookWindow,
DeregisterShellHookWindow, RegisterWindowMessage, SetWindowsHookEx,
UnhookWindowsHookEx, CallNextHookEx, GetKeyState, GetKeyboardState, GetCursorPos,
GetForegroundWindow, GetModuleHandle, keybd_event, CreateRectRgn, CombineRgn,
DeleteObject, DwmExtendFrameIntoClientArea, DwmGetWindowAttribute,
SetLayeredWindowAttributes, OpenProcess, CloseHandle, CreateEvent, SetEvent,
WaitForMultipleObjects, SHQueryUserNotificationState, KBDLLHOOKSTRUCT,
CoCreateInstance
```

`NativeWindowApi` 是唯一托管封装边界；其它服务只能调用它的语义方法，不得直接
散落 `Windows.Win32.PInvoke`。CsWin32 句柄、结构和枚举必须使用生成类型。

`DisableRuntimeMarshalling` 下禁止 `Marshal.StructureToPtr`、
`Marshal.GetDelegateForFunctionPointer` 和依赖 `StringBuilder` 的封送。blittable
结构使用 `unsafe`、`sizeof`、`stackalloc` 和 `MemoryMarshal`。所有 unmanaged 回调
都使用静态函数指针，且回调目标必须在卸载前保持 rooted。

NativeAOT 禁止：

- `dynamic`、运行时类型名解析、`Activator.CreateInstance` 和程序集扫描。
- 依赖反射的 JSON、正则、页面/控件注册和属性编辑器。
- WPF `System.Windows.Automation`、`System.Drawing.Bitmap`、WPF Bitmap 类型。
- 在 UI 线程执行 WASAPI/FFT、进程枚举或网络请求。

发布后必须由 `Directory.Build.targets` 的 `_CopyWinUIResourcesForAot` 把生成的
`.xbf` 和项目 `.pri` 复制到 `$(PublishDir)`；否则 unpackaged AOT 应用会在主题或
模板加载时失败。发布目录还必须包含 `WindowsAppSDKSelfContained` 的 native
payload。

## 10. 测试和验收

### 单元测试：`MicaFlyouts.Tests`

覆盖设置默认值/normalize/JSON 原子保存/备份/导入 UUID、StateStore 订阅幂等性、
媒体过滤和 focused session、Next Up 去重、时间线格式化、六种浮层位置、DPI/OSD
边距、任务栏水平/垂直布局、Lock Keys 宽度/状态、FFT/peak/30 FPS 限流。

### Reactor self-test：`MicaFlyouts.ReactorSelfTests`

使用 fake stores 挂载每个窗口组件，验证业务空状态不会抛异常、Element tree、稳定 key、
SettingsCard/SettingsExpander wrapper、导航菜单和搜索路由、托盘嵌入资源以及
事件回调；另验证运行时未初始化时组件明确抛出异常。self-test 不依赖真实播放器、
音频设备或 Explorer。

### Windows UI/E2E：`MicaFlyouts.UITests`

在交互式 Windows 会话中验证媒体控制/seek/循环/随机、硬件音量和系统 OSD、应用
混音器、Caps/Num/Scroll/Insert、Next Up、Explorer 重启恢复、多显示器/垂直任务栏、
100/125/150/200% DPI、浅色/深色主题、全部语言与 RTL、设置深链接/搜索、首次引导、
单实例第二进程打开设置。截图需与原 WPF 版本比较窗口尺寸、布局、颜色和动画终态。

### 发布门禁

```powershell
dotnet build MicaFlyouts.slnx -c Release
dotnet publish src\MicaFlyouts\MicaFlyouts.csproj -c Release -r win-x64
dotnet publish src\MicaFlyouts\MicaFlyouts.csproj -c Release -r win-arm64
```

门禁包括：

- x64/ARM64 NativeAOT 发布成功，无新的 IL2xxx/IL3xxx/CsWinRT 错误。
- restore graph 不出现 `Microsoft.WindowsAppSDK` 聚合包；WinUI 版本只有一个，
  Reactor 为 `0.1.0-preview.13`，SettingsControls 为 preview2。
- 源码扫描不出现 `[DllImport]`、`[LibraryImport]`、`System.Windows.*`、WPF bitmap、
  Store/Premium 代码或未生成的 Win32 extern。
- 发布目录包含项目 `.pri`、全部需要的 `.xbf` 和 WinUI native payload。
- 无播放器、无音频设备、全屏应用、Explorer 重启、托盘不可用等降级场景可安全运行。

## 11. 明确不应加入的设计

- 不恢复 WPF、MicaWPF、WPF-UI、WPF Tray 或原 XML serializer。
- 不新增全局事件总线、通用 MVVM 基类、Redux/Flux 容器或服务定位器。
- 不让 Feature 直接调用 HWND、NAudio、GSMTC 或文件系统。
- 不把设置控件自动发现、页面反射注册或动态绑定引入 AOT 路径。
- 不恢复任何 Premium、Store 授权、购买限制或付费文案。
- 不因为使用 Reactor 而改变原项目的尺寸、颜色、字体、动画持续时间、默认值或
  用户可见交互。
