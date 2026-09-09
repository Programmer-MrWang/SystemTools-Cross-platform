# SystemTools（原版） vs SystemTools.CrossPlatform（跨平台版）代码级对比

> 对比日期：2026 本会话。
>
> **修订注记（本会话后续）**：本文描述的若干“保留但降级/空壳”状态已在同会话按用户裁决完成改造，最新代码以
> [coexistence-notes.md](coexistence-notes.md) 顶部修订注记与 git 工作区为准：自动主题同步与遮挡隐藏已整体删除、
> 插件调试空壳页已删除、设置页/悬浮窗编辑页/AI 对话页/关于页已回退“原版版式扣除已删功能”，页图标/题图/文案随原版恢复。
>
> **二次修订（单一 cipx 全平台）**：工程已固定 `net10.0` 单一 TFM，全部 `#if Platforms_*` 条件编译与
> `*Windows.cs` 条件文件已合并为运行期 `OperatingSystem.IsWindows()` 分派（SystemShutdownMonitor 改用纯
> P/Invoke 消息窗口，不再引用 System.Windows.Forms）；本文 §5/§6 中“条件文件承载”“仅 Windows 编译生效”等旧机制描述均已过时。原版仓库：`E:\My Github Projects\SystemTools`（插件 id `SystemTools`，v3.0.0.0）
> 跨平台版仓库：`E:\My Github Projects\SystemTools-Cross-platform\src\SystemTools.CrossPlatform`（插件 id `SystemTools-Cross-platform`，v1.0.0.0）
> 宿主：`E:\ClassIsland-git-misha`（develop/v2/misha-alpha，net10.0）。对比方法：逐文件读码对比 + 规范化文本 diff。

## 1. 结论速览

| 维度 | 原版 SystemTools | 跨平台版 SystemTools.CrossPlatform |
| --- | --- | --- |
| 目标平台 | 仅 Windows（TFM `net10.0-windows10.0.19041.0`，`Platforms=x64`） | Windows / Linux(X11) / macOS（`net10.0` 基础 TFM，Windows 上条件附加 `-windows10.0.19041.0`） |
| 功能范围 | 行动 62 文件/约 50 个注册行动 + 7 触发器 + 5 规则 + 7 组件 + 7 设置页 + 大量 Windows 服务 | 行动 29 个 + 触发器 2 + 规则 4 + 组件 6 + 设置页 6（+1 占位页）= 41 个可管理功能项 |
| 命名空间 | `SystemTools.*` | `SystemTools.CrossPlatform.*`（功能 ID 全部加 `SystemTools.CrossPlatform.` 前缀） |
| 相同文件 | — | 195 个同名文件中约 98 个仅命名空间/程序集改写，约 92 个有实质差异 |
| 主题 | 3 套（Card-type/ClassWidgets/Notch） | 3 套全保留（PNG 字节相同，仅主题 id 加前缀） |
| 界面 | 各设置页功能完整 | 设置页结构/文案保留，裁剪“液态玻璃、语音、扩展下载、实验性”等整块控件 |

## 2. 顶层差异

### 2.1 manifest.yml
- 原版：`id: SystemTools`、name `SystemTools - Hoshimi Miyabi`、`url/icon/readme/repoOwner/repoName/assetsRoot` 齐备、`supportedOSPlatforms: [Windows]`。
- 跨平台版：`id: SystemTools-Cross-platform`、name `SystemTools 跨平台版`、`icon/readme` 为空、无仓库元数据、`supportedOSPlatforms: [Windows, Linux, macOS]`、版本 `1.0.0.0`。

### 2.2 项目文件（csproj）
| 项 | 原版 | 跨平台版 |
| --- | --- | --- |
| TargetFramework | `net10.0-windows10.0.19041.0` 固定 | `net10.0` 基础 + 条件 Windows TFM（导入宿主 `CrossPlatformProps.props`，按运行/发布平台展开 TFM 与 `Platforms_Windows/Linux/MacOs` 编译符号） |
| WinForms | `UseWindowsForms=true`（关机监视消息窗口、确认框 SendKeys） | 无 |
| CsWin32 / Windows SDK | `Microsoft.Windows.CsWin32`（P/Invoke 源码生成）+ 直接 ntdll/user32 调用 | 无（集中到 `*Windows.cs` 条件文件） |
| 原生依赖包 | DlibDotNet、OpenCvSharp×3、System.Management、System.Speech | 全部移除 |
| 子项目/资源 | VoskWorker 子工程（复制到输出）、ThirdParty/LiquidGlassAvaloniaUI（sksl 着色器）、black.html、huifu/jinyongshubiao 脚本、icon.png/title.png/version.json/README 复制 | 全部移除；额外引用 Avalonia/FluentAvalonia 等仅编译期包（ExcludeAssets runtime/native），`CreateCipx` 保留 |
| SDK 引用 | 默认本地宿主源码 ProjectReference + PowerShell 剥离宿主依赖 | 默认 NuGet `ClassIsland.PluginSdk 2.1.1.1`（`UseLocalClassIslandSdk` 可切回本地） |

### 2.3 标识与注册
- 设置分组：原版 `systemtools.settings`（运行期反射注册，图标 `\uE079`，名 “SystemTools 设置”）；跨平台版 `SystemTools.CrossPlatform.settings`（`services.AddSettingsPageGroup("SystemTools.CrossPlatform.settings", "\uE079", "SystemTools 设置")`），图标与显示名相同。
- 页面 id：`systemtools.settings.main/more/aiChat/floating/about/pluginDebug` → `SystemTools.CrossPlatform.settings.*`。页图标：主设置页原版 E079/E078、悬浮窗编辑页原版 EA37/EA37，跨平台版这两页页图标改为空字符串 “”/“”；更多功能选项 E28E、AI 对话 EFFF、关于 E9E4、插件调试 E2C8 两版相同。跨平台版各页带 `[Group("SystemTools.CrossPlatform.settings")]` 特性。
- 通知提供方/频道：GUID 全部更换（避免与原版同装冲突），`NotificationProviderInfo` 名称/图标 `\uE9FB` 不变。
- 主题 id：`Card-type-component`→`SystemTools.CrossPlatform.Card-type-component`、`classwidgets`→`SystemTools.CrossPlatform.classwidgets`、`notch-style`→`SystemTools.CrossPlatform.notch-style`；Banner 字段原版走 ThemeBannerCacheService 缓存路径，跨平台版置空。
- 行动树根组 “SystemTools 行动”\uE079（两版相同）。

## 3. 行动菜单树逐项对照（图标为 Segoe Fluent 字形；文字完全一致）

### 跨平台版保留的 10 组（顺序 = 原版中相对顺序）
| 组（图标） | 项目（图标） | 说明 |
| --- | --- | --- |
| 电源选项… (EDE8) | 计时关机(E4C4)、高级计时关机(E4D2)、取消关机计划(E4CC)、锁定屏幕(EAF0)、立即重启(E0BD)、立即关机(EDE9)、睡眠(F44B) | 7 项全保留，文字/图标逐项一致 |
| 文件操作… (E759) | 复制(E6AB)、移动(E6E7)、删除(E61D) | 一致 |
| 实用工具… (E352) | 退出进程(E0DE)、拉起自定义系统通知(E3E4) | 原“屏幕截图(EEE7)/禁用硬件设备(E09F)/启用硬件设备(E0AD)”删除；显示名经“文案去 Windows 化”裁决由「拉起自定义Windows通知」改写为「拉起自定义系统通知」（ID 与图标 E3E4 不变） |
| 悬浮窗设置… (EA37) | 显示悬浮窗(EA37)、切换悬浮窗层级(E9A8)、切换悬浮窗配置方案(E9A8)、切换悬浮窗主题(E790) | 一致 |
| 媒体工具… (E342) | 后台播放音频(EBCC) | 原“设置系统音量(F013)”（+FFmpeg 摄像头抓拍 E39E）删除 |
| 更多功能选项… (E28E) | 自动切换 ClassIsland 主题(E5CB)、遮挡文字时隐藏主界面(EEE3) | 原“自动播放(EE81)”删除 |
| 高级自动化工具… (E01F) | 行动流执行确认(E01D)、触发指定触发器(EAB7)、开关自动化(E051) | 一致 |
| AI 功能… (EFFF) | 显示AI对话框(E8C3) | 原“启用语音唤醒 AI(ED53)/唤醒语音对话 AI(EFF9)”删除 |
| 其他工具… (E32C) | 沉浸式时钟(E4D2) | 一致 |
| ClassIsland… (E5CB) | 清除全部提醒(E029)、加载临时课表(E6A1)、打开应用设置(EF27)、打开档案编辑(E699)、打开换课窗口(E13B) | 原“重启应用为管理员身份(EF53)”删除 |

### 原版独有（跨平台版无）组
| 组（图标） | 项目 |
| --- | --- |
| 模拟操作… (EA0B) | 模拟键盘(EA0F)、模拟组合键(EA15)、模拟鼠标(E5C1)、键入内容(E4BE)、窗口操作(F4B3) + 子组“常用模拟键”(EA0B)：按下 Alt+F4 / Alt+Tab / Ctrl+Z / Enter / Esc / F11（均 EA0B） |
| 显示设置… (F397) | 复制屏幕(E635)、扩展屏幕(E647)、仅电脑屏幕(E62F)、仅第二屏幕(E641)、黑屏html(E643)、显示桌面(E62F)、调整屏幕亮度(F464) |
| 系统个性化… (F42F) | 切换壁纸(E9BC)、切换主题色(F42F)（切换系统强调色注释未启用） |
| 实验性功能… (E508) | 禁用鼠标(E5C7)、启用鼠标(E5BF)（EnableExperimentalFeatures 门） |

功能抽屉（“启用功能选项”）中跨平台版列出 41 项（行动 29+触发器 2+组件 6+规则 4），名称/组别文字与原版一致（含 “ LED 文本仿真显示框” 的空格细节）；原版另列 歌词显示、USB设备插入时、按下F9时、长时间未操作电脑时、点击主界面时、关键词触发、正在播放媒体音乐、模拟操作/常用模拟键/显示设置/系统个性化各行动、屏幕截图、设置系统音量、摄像头抓拍、自动播放、重启应用为管理员身份、启用/禁用鼠标、语音 AI 等约 25 项。

## 4. 按文件类别差异

### 4.1 已删除功能（仅原版有；153 个文件，主要类目）
- 行动 33：AdjustScreenBrightness、AltF4、AltTab、AutoOpenUsbDriveOnInsert、BlackScreenHtml、CameraCapture、ChangeWallpaper、ClickSimulation、CloneDisplay、CtrlZ、Disable/EnableDevice、Disable/EnableMouse、EnableVoiceWakeAi、EnterKey、Esc、ExtendDisplay、ExternalDisplay、F11、InternalDisplay、RestartAsAdmin、ScreenShot、SetVolume、ShowDesktop、SimulateKeyboard、SimulateKeyCombination、SimulateMouse、SwitchSystemAccentColor、SwitchTheme、TypeContent、WakeUpVoiceConversationAi、WindowOperation。
- 触发器 6：UsbDeviceTrigger、HotkeyTrigger、LongIdleTrigger、KeywordTrigger、MainWindowClickTrigger（含各自 Config/Settings/设置控件）；FloatingWindowTrigger 的原版 Triggers\FloatingWindowTriggerConfig.cs 移到跨平台版 Config\ 下（新文件）。
- 规则 1：MediaMusicPlayingRule（+Handler+Settings+SettingsControl，原图标 EDBF “正在播放媒体音乐”）。
- 组件 1：LyricsDisplay（+Settings+两个 SettingsControl axaml）。
- 服务 19：AboutTitleImageCacheService、AiVoiceConversationService、BackgroundLuminanceCalculator、CameraCaptureService、ClassIslandSettingsService、FaceRecognitionService、HotkeyService/IHotkeyService、KeywordSpeechService、LiquidGlassBackdropFactory、MainWindowAreaService、MainWindowBackgroundCaptureService、MainWindowClickService、SpeechRecognitionDependencyPrompt、SystemMemoryCleanupService、ThemeBannerCacheService、UsbAutoPlayService、VoskSpeechService、WindowsHelloService。
- 设置 22+：AccentColor/AdjustScreenBrightness/AutoOpenUsbDriveOnInsert/CameraCapture/ClickSimulation/Copy 等系统类、FaceRecognition/WindowsHello、KeyboardInput/KeyCombination/MouseInput、LongIdle/MainWindowClick/UsbDeviceTrigger、ScreenShot/SetVolume/Theme/TypeContent/Wallpaper/WindowOperation 等。
- 界面：AiChatGlassSurface、AiVoiceConversationOverlayWindow、VoiceWaveformControl、ExperimentalBadge、FaceRecognitionAuthorizer、WindowsHelloAuthorizer、HotkeyRecorderControl、SettingsPageLazy（懒加载壳）、LiquidGlass 相关（AiChatGlassSurface/SettingsPageLazy 等）。
- 资源/脚本：black.html、huifu/jinyongshubiao bat+ps1、icon.png、title.png、version.json、Lyricify Lite README、VoskWorker、ThirdParty/LiquidGlass shaders、Strip-ClassIslandHostDependencies.ps1、.github CI。
- 配置：LiquidGlassSettings/LiquidGlassButtonSettings、HotkeyTriggerConfig 等 5 个原版 Config、Shared/DependencyPaths、FaceRecognitionCredentialCleanup、InjectServices。

### 4.2 保留但跨平台化改造（有实质 diff）
| 文件 | 原版 | 跨平台版 |
| --- | --- | --- |
| 电源族 7 个行动 + 设置 | 直接 Process 启动 shutdown/rundll32；立即重启/关机用 ntdll RtlAdjustPrivilege + ExitWindowsEx；WinForms SendKeys 自动确认；看门狗轮询 “ClassIsland.Desktop.exe” | 统一收敛到新增 `Actions/SystemPowerCommandWindows.cs`（#if Platforms_Windows）与 `SystemPowerCommandStub.cs`（#if !Platforms_Windows，返回 false/-1）；行动先 `OperatingSystem.IsWindows()` + 命令存在性预检，失败发宿主 Toast “…在当前平台不可用/命令不可用/未执行，已跳过执行”后正常结束；立即重启/关机改 `shutdown /g|/s /t 0`；取消关机识别退出码 1116（“当前没有活动的关机计划”）；睡眠 1500ms 有界等待超时按“已发起未确认”；高级计时关机删掉 cmd 倒计时包装进程与 1 秒看门狗，改“先 cancel 再按总秒数 /s /t”，宿主退出路径经 Plugin.OnAppStopping 调 `AdvancedShutdownAction.CancelPlanOnAppStopping`；对话框/进度条动画 UI（AdvancedShutdownDialog/ExtendShutdownDialog/悬浮窗倒计时）逐行保留 |
| SystemShutdownMonitor | NativeWindow 监听 WM_QUERYENDSESSION/WM_ENDSESSION | 双分支条件文件：Windows 分支保留原实现，非 Windows no-op（IsSessionEnding=false） |
| SystemMotionPreferences | user32 SystemParametersInfo | Windows 分支保留，非 Windows no-op 分支 |
| Copy/Move/Delete | 文件夹走 cmd/robocopy（/copyall 含 ACL/审核），TrimEnd('\\') | BCL 递归复制/Directory.Move（同卷）+跨卷回退递归复制删源/Directory.Delete；`Path.TrimEndingDirectorySeparator`；部分完成如实报错；ACL/审核元数据不再复制 |
| KillProcess 行动/设置控件 | 行动本体已是 BCL `Process.GetProcessesByName().Kill()`（taskkill 整段仅注释保留），设置控件“查看正在运行的进程”用 tasklist 子进程 | 行动本体两版一致；设置控件改用 `Process.GetProcesses()` 枚举（进程名+PID 定宽文本），弹窗/“复制全部”按钮/失败文案一致；XP 侧把注释掉的 taskkill 残留也删掉 |
| FullscreenClock 设置控件 | Process.Start(UseShellExecute) 打开 GitHub | `ILauncherService.LaunchUrl` |
| 内存自动清理 | 非 Windows 整体不执行（GC 也在守卫后）；psapi EmptyWorkingSet DllImport | GC/阈值测量三平台执行；工作集修剪经新增 `IProcessMemoryMaintenanceService`→`ProcessMemoryMaintenanceNativeWindows.cs`(#if Platforms_Windows)/`NativeNoOp`(#if !)/`ProcessMemoryMaintenanceService` 选择适配器；日志 `WorkingSetTrimmed=false` |
| 自动主题同步服务 | 2 秒计时器 + 连续背景捕获 + 亮度计算 + ClassIslandSettingsService.SetTheme | 后端全裁（C 档），开关可保存；服务 ApplyConfig/Start/Stop 保留签名，实际执行 = 记录 “探测后端不可用，停止自动同步” + Toast “自动主题同步不可用/保持当前主题”一次 |
| 主界面遮挡检测服务 | 连续背景捕获 + Windows.Media.Ocr + GDI 处理 + 隐藏主界面 | 同降级：Toast “遮挡检测已降级/未启用”，主界面保持可见；生命周期 API（Suspend/Resume 租约等）保留空实现 |
| 悬浮窗服务 FloatingWindowService | 2478 行：LiquidGlass 外观/捕获（94 处）、WinEvent 钩子（17）、鼠标钩子、SetWindowPos、拖拽重排、层级自动重检 | 1601 行：仅经典外观；层级只显式应用（RecheckWindowLayer 在启动/属性变化/手动切换时），原 WinEvent/鼠标钩子全删；按钮行拖拽（⋮⋮ 把手、DragOver/Drop）删除，改“添加行/在下方插入一行/删除行”按钮；触摸拖动把手（dots）保留；通过宿主 `IWindowPlatformService`（2 处）做跨平台窗口服务 |
| 悬浮窗编辑页 | 顶部 “设置 · 悬浮窗” 题头（EFBF 图标）；方案选择内嵌在布局卡；外观里“悬浮窗样式（经典/液态玻璃实验性 F4AB/F4BD + 实验徽章）”、玻璃模糊(F4AB)/玻璃折射(F047)/背景刷新频率(E823)/按钮弹性(E2FC) 4 条 Slider；行规则集/按钮规则集 在设置页内嵌编辑器 | 题头删；新增独立“配置方案”卡（图标 E9A8 由方案行动图标复用），含新建(E00D)/打开文件夹(E88D)/删除(E61D)按钮+当前方案只读项；布局卡加“显示悬浮窗”开关说明文字（无拖拽措辞）；外观仅保留缩放(E10B)/图标大小(E721)/文字大小(F26F)/透明度(E772)/主题(E5CB 四项，第4项“自适应背景”E520 保留但描述注明“映射为跟随宿主明暗”)/阴影(E20B)/一直显示拖动把手(E94F)；行/按钮规则集经抽屉编辑；层级组 (EA2F) 置底(E0CB)/置顶(E197)+“层级设置频率”(E125)四项下拉保留并注明“钩子驱动的层级自动重检未启用…仅保持配置兼容”；底部 ©2026 文字删 |
| AI 对话浮窗/设置页 | SplitView 历史面板（右）、气泡附件缩略图（Image/E8A5 文件图标）、Markdown 渲染助手回复、流式进度条、空态 EFFF 图标“开始一段新对话”、按钮排：附件(E00C)/返回底部(E09F)/语音输入(EB80,带激活高亮样式)/发送(EF11)/停止(EE67)、标题可编辑、时间显示、页脚“内容由AI生成，请仔细甄别”；液态玻璃背景 + AiChatGlassSurface 气泡 | 全部经典外观；历史改为左侧 220 宽 ListBox + “历史”ToggleButton；消息气泡简化（无 markdown/进度条/缩略图）；删除语音输入(EB80)、空态图标、当前模型文字显示等；“内容由AI生成”页脚删；保留 共享回复到提醒开关、复制(E58B)/修改(ECA7)/保存重新回答/取消(EC9F)/重试、拖放遮罩 |
| 主设置页 | 头部题字 “SystemTools”+E078“功能 · 主设置”；卡：启用功能选项(E079)、实验性功能(E508+ExperimentalBadge)、更多功能选项……(E28E，带 IsClickEnabled 绑定)、启用悬浮窗功能(EA37)、AI 服务(F005，含 供应商/APIKey/API地址/模型/语音唤醒AI(ED53)/AI唤醒词/当前使用模型(Vosk 检查按钮)/AI对话悬浮窗样式(磨砂/液态玻璃))、启用扩展功能(E4E2：FFmpeg 开关+下载 190MB、人脸识别开关+下载 138MB、Windows Hello 开关、下载语音识别服务 52MB、下载语音识别模型下拉、进度条)、底部 “Programmer_Wang ©2026” | 保留：启用功能选项抽屉（搜索/DataGrid 4列：启用/类型/所属组别/名称、提示条 F430、取消/应用并重启(E06D)）、更多功能选项…（E28E）、启用悬浮窗功能、AI 服务四项基础配置+“当前使用模型”改为只读 AiModel 文本；删除：实验性功能、语音唤醒/唤醒词/语音样式 ComboBox、扩展功能整卡（下载器）、底部文字 |
| 更多功能选项页 | ClassIsland 外观(E51E)：自动切换主题(E5CB)/遮挡文字(EEE3)；虚拟放学(ED55) 触发时间 TimePicker+持续时间 NumericUpDown；性能(EE21)：自动清理 ClassIsland 内存(E97B)、自动清理内存（管理员）(EE21+ExperimentalBadge+阈值 50-99%+“一键清理”按钮)；其他工具(F4C5)：自动播放(EE81) | 保留：外观两卡+虚拟放学+自动清理 ClassIsland 内存；删除管理员清理整卡与自动播放卡；无 SettingsPageLazy 懒加载壳 |
| 关于页 | 顶部渐变遮罩题图（TitleImage 由 AboutTitleImageCacheService 缓存 title.png，LinearGradient 淡出）+ StickyScrollViewer；信息卡：SystemTools {版本} - Hoshimi Miyabi、3 链接按钮（插件市场 E03B / GitHub E0EA / 沉浸式时钟 E4D2，指向原版市场与仓库页）；TabStrip compact：帮助(E236)/插件介绍(F19C)/更新日志(E163)；帮助页含 “Lyricify Lite 适配帮助”卡(E236, EC2E) | 删除题图与缓存服务与 Lyricify Lite 帮助卡；改为普通 ScrollViewer + 页首 “关于 SystemTools” 文字；其余（含 5 连击进调试页 URI 改为 CrossPlatform id、README.md/README-1.md/README-2.md 三个 markdown 标签、反馈渠道三链接）保留 |
| 插件调试页 | 376 行 XAML + 204 行 cs：液态玻璃全部参数（37 个 NumericUpDown/Slider…）、外观预设 1/2、语音唤醒调试按钮 | 仅占位：标题“插件调试”+“源插件调试选项在本跨平台版本中未提供。”（页图标 E2C8、隐藏标题不变） |

### 4.3 主题与组件
- 3 套主题完整保留（CardTypeComponent/ClassWidgets/NotchStyle 全部 axaml/cs/manifest 逐行一致或仅 xmlns/namespace 改写）；3 张 PNG（上课/无课程/课间休息）MD5 与原件完全一致；ClassWidgets 主题 manifest 与 Style 文件里的主题 id 改为带前缀。
- ClassWidgetsCard.axaml：删掉 “跟随宿主是否启用背景材质（liquid-glass 开关）” 的可见性绑定（IsVisible=$self.…IsBackgroundMaterialEnabled），CW2 边框恒显示（与悬浮窗液态玻璃被裁剪对应）。
- 6 个组件（NetworkStatus/ClipboardContent/LocalQuote/NextClassDisplay/BetterCarouselContainer/ScrollingText）的 axaml 与逻辑均与原版一致（仅命名空间/程序集改写）；对应的组件设置控件与 Model 也一致。

### 4.4 配置与数据
- MainConfigData（聚合配置根）：JSON 属性名与原版同名（enable…字典/悬浮窗布局等），但删除实验性、FFmpeg、人脸/Windows Hello、语音唤醒、公告、液态玻璃三件套、系统内存清理等成员；新增 actionFlow 确认/延迟窗口位置、currentFloatingWindowProfile、悬浮窗外观/位置/层级成员（与原版旧布局字段相同名以支持一次性迁移）；原版 RestartPropertyChanged 事件不引入，改为各开关显式 Save+RequestRestart。
- 悬浮窗配置方案目录：原版 DependencyPaths 共享缓存目录 → 跨平台版 `PluginConfigFolder\FloatingWindowProfiles\`（首次启动由 MigrateFromLegacyConfig 从自己旧布局字段生成 Default.json 一次）。
- auto.json（行动进行时状态/触发指定触发器）仍读写各自插件安装目录同名文件，互不干扰。
- 配置不互相迁移：跨平台版首次运行为全默认值。

### 4.5 其它实质改动（小事例）
- AboutSettingsViewModel 等视图模型基本照搬；AiChatSettingsViewModel 删语音输入相关成员（47 处 → 0），CurrentModelName/IsStreaming/通知共享逻辑两版相同。
- AiChatWindowService 构造函数去掉 VoskSpeechService/MainWindowBackgroundCaptureService 依赖。
- FullscreenClockSettings 的设置类型加了注释；ShowToastAction 两版逐字节同构（均走宿主 IDesktopToastService）。
- Rules：原版处理器是 Plugin 分部类静态方法（HandleXxx），跨平台版改为独立 RuleHandler 静态类（Handle），内容一致；4 个规则注册文案/图标（E342/E6B1/E69D/E4CA）不变。
- Trigger 文件名/命名空间落点变化：原 Config/ActionInProgressTriggerConfig.cs 命名空间 Triggers → 跨平台版 Config 命名空间（内容同）。
- 版本检查服务：逻辑同（首次安装/更新各发一次宿主 Toast），导航 URI 指向跨平台版 about 页；两版 csproj 差异意味着跨平台版输出目录没有 version.json/README.md/icon.png——首次运行会自己生成 version.json，而 About 页 markdown 标签将显示“未找到”占位文案，插件信息卡图标取不到 icon.png（manifest icon 也为空）。
- 通知文本/标题仍写 “SystemTools”（如 “计时关机在当前平台不可用…”）与插件显示名“SystemTools 跨平台版”不完全一致（细节保留原版文案）。

## 5. 构建与宿主契约要点
- 跨平台版导入宿主 `CrossPlatformProps.props`：开发机按 OS 展开 TFM 并注入 `Platforms_Windows/Linux/MacOs` 编译符号；插件内 Windows 专属代码以 `#if Platforms_Windows` 条件文件承载（SystemPowerCommandWindows、ProcessMemoryMaintenanceNativeWindows、SystemShutdownMonitor 的 Win 分支、SystemMotionPreferences Win 分支），非 Windows 配 no-op 存根。
- 宿主双分支/缺失接口按登记处理：无 ISystemPowerService 消费 → 插件本地实现；G2 无会话结束事件 → 关机监视仅 Windows（NativeWindow）；macOS 部分接口 Stub。
- 设置页懒加载壳（SettingsPageLazy）、StickyScrollViewer 题图、ExperimentalBadge 等原版私有控件未迁移，页面相应视觉（题图、懒加载闪烁壳、实验徽章）随之消失。

## 附：仅原版存在文件清单（153）
见会话分析时生成清单；类别同 4.1。
