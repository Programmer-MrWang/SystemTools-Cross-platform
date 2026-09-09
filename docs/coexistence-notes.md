# SystemTools 跨平台版 与原版 SystemTools 同装差异说明

> **修订注记（ShowToast 行动改名与失败提示）**：按用户裁决，「拉起自定义Windows通知」行动显示名/菜单/功能抽屉统一改写为**「拉起自定义系统通知」**（行动 ID SystemTools.CrossPlatform.ShowToast 与图标 E3E4 不变；.tang 案卷中的历史验收记录为该轮时点存档，不作回改）。该行动本就经宿主 PlatformServices.DesktopToastService 跨平台抽象实现（Windows WinRT / Linux org.freedesktop.Notifications / macOS NSUserNotification），三平台通用；新增失败可见化：桌面通知通道异常时，经插件应用内通知渠道提示「自定义通知显示失败」（不递归调用已失败的桌面 Toast 通道）。

> **修订注记（电源族三平台改造）**：按用户裁决，电源选项组 7 项由“Windows 专属 + 非 Windows 一律降级跳过”重做为**“可实现即实现”**口径（§2.1 随之整体改写，本会话前关于电源族“非 Windows 一律提示不可用”的描述作废）：
> - 仍为单一 `net10.0` cipx 三平台通用；平台差异全部运行期分派，无新增条件编译文件。
> - **三平台均实现**：计时关机、高级计时关机、取消关机计划、立即重启、立即关机、睡眠。Windows 保持 OS 级 shutdown.exe/rundll32.exe 原语义；Linux（X11 与 Wayland 均注册；电源动词走 systemd `systemctl`——loginctl 自始不提供电源动词，曾误用报 `Unknown command verb`；elogind 系无 systemctl 时回退其 loginctl；非 root 经 polkit，拒绝如实提示）；macOS 关机/重启经 osascript → System Events（首次弹系统“自动化”授权询问，拒绝如实失败并引导授权），睡眠经 `pmset sleepnow`。
> - **定时关机族在 Linux/macOS 为“应用内计划”**（`ShutdownPlanCenter`，到点调用立即关机原语；宿主退出即取消——与 Windows“宿主退出即取消 OS 计划”的用户可见语义一致）：非 root 无系统级定时关机原语。
> - **仅部分平台可实现**：锁定屏幕 —— Windows（rundll32）、Linux（`loginctl lock-session`）注册；macOS 无非 root 公共途径，**不注册不显示**。
> - 注册/行动菜单/功能抽屉统一经 `SystemPowerCapability` 能力门过滤：本机不支持的电源行动不注册、不显示（如 Linux 无 systemctl/loginctl 或非图形会话、macOS 锁定屏幕）。
> - 代码面：`Actions\SystemPowerCapability.cs`（能力探测）、`Actions\ShutdownPlanCenter.cs`（应用内计划）、`Actions\SystemPowerCommand.cs`（平台分派重写，`PowerCommandResult` 带失败详情）。

> **修订注记（本会话）**：按用户裁决完成一轮“跨平台清理 + 对齐原版”改造，以下条目已过时并作废：
> - 「自动切换 ClassIsland 主题」与「遮挡文字时隐藏主界面」两功能已**整体删除**（行动/菜单项/设置页开关/功能抽屉条目/空壳服务/配置成员全部移除），不再有“不可用 Toast”降级行为（原 §2.4 及 §5.3 中相关描述作废）。
> - 空壳「插件调试」页已删除（含 Plugin.cs 注册与关于页信息卡 5 连击导航入口）。
> - 行动菜单组由 10 组减为 **9 组**（“更多功能选项…”组随上述两行动删除）；功能抽屉可枚举项由 41 改为 **39**（行动 27 + 触发器 2 + 组件 6 + 规则 4）；注册设置页为 5 页。
> - 关于页恢复原版题图（title.png 内置 + AboutTitleImageCacheService 缓存下载）与图标（icon.png，manifest icon 已指向）；恢复主题预览图服务 ThemeBannerCacheService 并给三个主题 Banner 赋值。
> - AI 对话设置页、AI 对话悬浮窗、悬浮窗编辑页、主设置页等按“原版版式扣除已删功能（液态玻璃/语音/拖拽/下载器）”回退对齐；页图标、页头、页脚与文案随原版恢复；“已批降级/跨平台版本提示”等内部术语已从用户可见文案清除（悬浮窗“主题·自适应背景”“层级设置频率”两处仅作如实短注）。
> - **单一 cipx 全平台改造**：csproj 固定 `net10.0`（不再按构建机 OS 自动升 Windows/macOS TFM，不再导入宿主 CrossPlatformProps.props）；SystemShutdownMonitor 的 WinForms NativeWindow 分支改写为纯 P/Invoke 隐藏消息窗口（运行期分派）；SystemPowerCommandWindows/Stub、ProcessMemoryMaintenanceNativeWindows/NoOp、SystemMotionPreferences 的 `#if` 条件对全部合并为单一运行期分派实现并删除条件文件。同一 cipx 三平台通用的验证门禁：产物 DLL 二进制不含 `System.Windows.Forms`、runtimeconfig `tfm` 为 `net10.0`（旧 Windows-TFM 产物与“macOS 报 System.Windows.Forms 缺失”问题一并消除）。


本文面向**同时安装（同装）**「SystemTools 跨平台版」（新插件）与原「SystemTools」插件的 ClassIsland 用户，说明两插件并存时的行为差异。新插件支持 Windows、Linux（X11）与 macOS；原插件仅限 Windows。本文描述以当前交付版本源码为准，逐条附可复核的文件行号或登记条目号。

> 口径约定（自“单一 cipx 全平台”改造起）：插件只发布 `net10.0` 单一目标框架产物（同一份 cipx 安装到 Windows/Linux/macOS 均可运行）。平台差异全部在运行期用 `OperatingSystem.IsWindows()` 判断承载（SystemShutdownMonitor / SystemPowerCommand / ProcessMemoryMaintenanceNative / SystemMotionPreferences），已不再有 `#if Platforms_*` 条件编译或 `*Windows.cs` 条件文件；Windows 专属行为（工作集修剪、会话结束监视等）在非 Windows 上按 no-op/跳过分支运行；电源族自“电源族三平台改造”轮起按 §2.1 三平台矩阵交付（见顶部修订注记），不再一律 no-op。

---

## 一、并存形态：两插件、三平台各自独立

两插件可同时安装、同时启用，互不覆盖对方的注册与数据。新插件不读写原插件的任何配置、方案或状态文件；反之亦然。

### 1.1 插件与功能标识完全独立

| 项 | 原插件 | 新插件（跨平台版） |
| --- | --- | --- |
| 插件 id | `SystemTools` | `SystemTools-Cross-platform`（manifest.yml:11） |
| 显示名 | SystemTools | SystemTools 跨平台版（manifest.yml:12） |
| 功能 ID 前缀 | `SystemTools.*` | `SystemTools.CrossPlatform.*` |
| 设置页分组 | 自有分组 | 「SystemTools 设置」（独立设置页组，Plugin.cs:160） |
| 支持平台 | Windows | Windows / Linux（X11）/ macOS（manifest.yml:20） |

行动菜单树根组名为「SystemTools 行动」，其下 10 个组：电源选项…、文件操作…、实用工具…、悬浮窗设置…、媒体工具…、更多功能选项…、高级自动化工具…、AI 功能…、其他工具…、ClassIsland…（Plugin.cs:471-545；组内条目与启用门见 Plugin.cs 注册面）。同装时原插件的菜单组按其自身注册并列显示，互不混排。

### 1.2 配置与状态文件互不读写

| 文件/目录 | 用途 | 隔离方式 | 依据 |
| --- | --- | --- | --- |
| 主配置（MainConfigData） | 各设置页选项、功能启用字典 | 存于各自插件独立配置目录（`GlobalConstants.PluginConfigFolder`），不共用 | p1-03 §2-D7 |
| `auto.json` | 「行动进行时」触发器状态 /「触发指定触发器」行动写入 | 读写方均取自身插件安装目录（`Assembly.Location` 同目录），两插件目录不同，文件同名但互不读写 | ActionInProgressTrigger.cs:31-38；TriggerCustomTriggerAction.cs:31-38；06 条目 14/21 |
| `version.json` | 各插件自身的版本检查与更新通知 | 存于各自插件安装目录（`PluginFolder`） | VersionCheckService.cs:14-20；06 条目 32 |
| 悬浮窗配置方案 | 按钮行/规则集等方案文件 | 存于新插件独立配置目录下 `FloatingWindowProfiles\`；原插件使用的共享缓存目录不再复用 | FloatingWindowProfileManager.cs:32-42（p1-03 §2-D8） |

两插件的「启用/禁用」「行动流」「版本提醒」等状态因此完全独立：禁用或卸载其中一个插件不影响另一个。

---

## 二、Windows 专属能力与各平台降级行为

新插件对原插件中依赖 Windows 原生能力的功能按“**可实现即实现、仅部分平台可实现则平台适配、否则不注册不显示**”交付：Windows 上行为与原版一致（或按登记口径等效）；非 Windows 上凡可实现者提供平台实现，凡不可实现者不注册不显示，均不抛未处理异常、不伪造成功。逐项如下（登记条目号可在案卷证据中复核；电源族详见 §2.1 三平台矩阵）。

### 2.1 电源族 7 项（计时/高级关机、取消关机、锁屏、重启、关机、睡眠）——三平台矩阵

电源族按“**能实现则实现；仅部分平台可实现则平台适配并在该平台注册；否则不注册、不显示**”交付。
注册/行动菜单/功能抽屉统一经 `Actions\SystemPowerCapability.cs` 能力门过滤（启动期惰性探测：环境变量与
文件检查，不派生进程、不触发授权询问）；执行期结果判定分四类并如实记录/提示，不伪造成功：

- 成功（exit 0）；
- 有界等待超时 → “已发起、未确认”（睡眠/系统即将断电等场景）；
- 系统拒绝授权（polkit、macOS 自动化 TCC）→ 提示「未执行：系统拒绝了请求（可能需要系统授权）」并附详情；
- 命令失败 → 提示「未执行/未生效」并记日志。

| 功能 | Windows | Linux（X11/Wayland，非 root，systemd/elogind） | macOS（非 root） |
| --- | --- | --- | --- |
| 计时关机 | `shutdown /s /t N`（OS 级计划） | 应用内计划 `ShutdownPlanCenter`，到点 `systemctl poweroff`（elogind 回退 loginctl） | 应用内计划（同上），到点 System Events 关机 |
| 高级计时关机 | 计划/取消/立即三操作，对话框随源 | 同左（计划落应用内，对话框跨平台） | 同左 |
| 取消关机计划 | `shutdown /a`（1116=无计划文案） | 取消应用内计划（无计划同 1116 文案） | 同左 |
| 锁定屏幕 | `rundll32 user32.dll,LockWorkStation` | `loginctl lock-session`（需桌面环境监听 lock 信号） | **不注册不显示**（非 root 无公共途径） |
| 立即重启 | `shutdown /g /t 0` | `systemctl reboot`（elogind 回退 loginctl） | System Events restart（TCC） |
| 立即关机 | `shutdown /s /t 0` | `systemctl poweroff`（elogind 回退 loginctl） | System Events shut down（TCC） |
| 睡眠 | `rundll32 powrprof SetSuspendState`（有界等待） | `systemctl suspend`（命令阻塞至唤醒，超时按“已发起”；elogind 回退 loginctl） | `pmset sleepnow` |

说明：
- **注册门**：Windows 全 7 项（shutdown.exe/rundll32.exe 存在）；Linux 全 7 项（`systemctl` 或 `loginctl` 可用且处于图形会话；
  X11 与 Wayland 均按用户裁决注册）；macOS 6 项（osascript 存在；睡眠另需 pmset；锁定屏幕除外）。
- **后端归属更正**：systemd 的电源动词（poweroff/reboot/suspend）自始属于 `systemctl`，`loginctl` 仅有会话类动词（lock-session 等）；初版误用 `loginctl` 执行电源动词导致运行时 “Unknown command verb”，已更正为 systemctl 优先、elogind 系（无 systemctl，其 loginctl 带电源动词）回退；锁屏始终走 `loginctl lock-session`。
- **定时关机族语义**：Linux/macOS 无“非 root 系统级定时关机”，改由插件内倒计时（`ShutdownPlanCenter.cs`），
  到点调用平台“立即关机”原语；宿主正常退出即取消（与 Windows“宿主退出即取消 OS 计划”的用户可见语义一致；
  宿主崩溃场景不再兜底执行——已按用户裁决接受）。Windows 路径保持 OS 级计划不变。
- **失败如实上报**：polkit/TCC 拒绝、命令失败均弹提示并记日志；macOS 首次关机/重启会触发系统“自动化”授权询问
  （拒绝后提示前往系统设置授权）。
- 高级关机对话框（AdvancedShutdownDialog/ExtendShutdownDialog）为跨平台 UI，凡能力门放行的平台均可达。

### 2.2 内存自动清理（GC 三平台、工作集修剪仅 Windows）

「自动清理 ClassIsland 内存」的垃圾回收（GC）与占用测量在三个平台均生效；**工作集修剪仅 Windows** 生效——非 Windows 上修剪环节自动跳过，仅留 `WorkingSetTrimmed=False` 日志，不影响 GC 本身，也不会周期性弹提示（登记 p2-01 §2-A1/§3；06 条目 49）。Windows 修剪经本地接口 `IProcessMemoryMaintenanceService` 的条件适配器（psapi 互操作，条件文件承载）实现。

### 2.3 悬浮窗（R-3/U5 降级口径）

悬浮窗经典外观、层级切换（置顶/置底）、按钮行与规则集、悬浮窗触发器在 Windows 上按原行为交付，经宿主跨平台窗口接口 `IWindowPlatformService` 承载（p2-03 §3）。以下子特性按已批决议降级（登记 p2-03 §2 D1-D15）：

| 子特性 | 行为差异 | 依据 |
| --- | --- | --- |
| 前台/重排 WinEvent 钩子、层级自动重检 | 移除；层级只在明确时点（启动、窗口状态更新、手动切换层级/方案、加载）一次性应用。设置页「层级设置频率」选项保留但运行时不消费（配置兼容，页内附注记） | p2-03 §2-D1；FloatingWindowEditorSettingsPage.axaml 层级组 |
| 低级鼠标钩子识别合成触控 | 移除；触控语义由跨平台指针事件直接承载 | p2-03 §2-D2 |
| 液态玻璃外观 / 自适应背景采样 | 不提供（U5/R-6 决议）；悬浮窗仅经典外观。主题第 4 项「自适应背景」保留选项名，实际效果为**跟随宿主明暗**（设置页该项说明文字已注明） | p2-03 §2-D4/D5；FloatingWindowEditorSettingsPage.axaml 主题项 |
| 手动鼠标拖拽玻璃边缘路径 | 移除；鼠标拖窗恒走跨平台标准拖动路径（与原版经典外观一致），触控拖拽链保留 | p2-03 §2-D6/D7 |
| 玻璃按钮 host/布局/延迟刷新 | 移除；按钮布局恒为经典参数（与原版经典外观一致） | p2-03 §2-D8/D9/D10 |
| 按钮行内拖拽排序 | 不提供；行排序改由「添加行 / 在下方插入一行 / 删除行」按钮承载（设置页描述文字已相应改写，不再提及拖拽） | p3-02 §1-W7（D6 口径） |
| 关机监视（会话结束事件） | 以条件编译承载：Windows 上保留源实现；非 Windows 为 no-op（宿主未提供会话结束事件，见第四节 G2）。宿主主动退出时本地关机计划一律取消 | p2-03 §2-D12；Plugin.cs:217-267 |
| 配置根重启提示事件 | 源版配置根在开关变更时发 `RestartPropertyChanged` 事件提示重启；新版不引入该事件，改为各开关处理器显式落盘并请求重启（用户感知等效，见 5.3） | p2-03 §2-D13；p1-06 §9-7 |

### 2.4 主题同步与遮挡检测辅助行动

「自动切换 ClassIsland 主题」「遮挡文字时隐藏主界面」两项行动的**画面/文字探测后端未随迁**（原探测链依赖已裁剪组件）。新插件中开关可保存，服务启动时经系统通知提示「自动主题同步不可用」/「遮挡检测已降级（未启用）」，随后保持当前状态：主题不自动切换、主界面保持可见（登记 p2-02 §2-AD1/AD2；06 条目 47/48 偏差注记）。

---

## 三、文件复制/移动的元数据差异（AD8）

原插件的文件夹「复制/移动」在 Windows 上调用 `robocopy`，并使用 `/copyall` 一类参数复制 NTFS 权限（ACL）与审核等元数据。新插件改用 .NET 标准库递归复制/移动（全平台统一，`cmd.exe`/`robocopy` 不进入新插件）：

| 项 | 原插件（Windows） | 新插件（三平台） | 依据 |
| --- | --- | --- | --- |
| 复制 | 文件 `File.Copy`；文件夹 `robocopy.exe`（含 ACL/审核元数据复制） | 文件/文件夹均为 .NET 递归复制：复制**内容与基础属性**，**不复制 ACL/审核等安全元数据** | Actions\CopyAction.cs:56/:90-125；p2-02 §1.1/§2-AD5；06 条目 34 |
| 移动 | 文件 `File.Move`；文件夹 `robocopy.exe /move` | 文件 `File.Move`；文件夹同卷 `Directory.Move`、跨卷回退递归复制+删除 | Actions\MoveAction.cs:56/:94-109；06 条目 35 |
| 删除 | 文件夹 `cmd /c rmdir /s /q` | .NET 递归删除（不经 shell 拼接路径） | Actions\DeleteAction.cs:42/:66；06 条目 36 |
| 失败语义 | 按外部工具退出码 | 源/目标不可用、权限不足、跨卷/递归失败：记结构化日志并抛行动错误；移动部分完成时**如实报告「未完整完成」**，不误报成功 | p2-02 §1.1（B2 移动行）；06 条目 34/35 降级行为 |

如需保留 NTFS 权限等完整元数据复制，请在原插件（仅 Windows）中执行该操作。

---

## 四、平台支撑面与构建发布说明（G1-G3）

宿主跨平台抽象在三个平台上的实装程度不同，新插件按以下登记口径处理（宿主契约基线 G1-G3）：

| 编号 | 缺口 | 新插件行为 |
| --- | --- | --- |
| G1 | 宿主 `ISystemEventsService` 无 Linux/macOS 实装（仅 Windows + Stub） | 新插件当前无功能消费该接口，不受影响 |
| G2 | 该接口契约仅含整点报时事件，**无会话结束/关机事件** | 关机监视不假托宿主事件：Windows 走条件编译的监视实现；非 Windows 为 no-op；宿主主动退出路径取消本地计划（见 2.3） |
| G3 | 宿主 `IDesktopService` 无 macOS 实装（开机自启/URL 协议为 Stub 语义） | 新插件当前无功能消费该接口，不受影响 |

（依据：宿主契约基线 §3/§7-G1-G3 登记；p1-05 §5.1-5；p2-03 §2-D12。）

构建与发布面遗留事项（用户可感知为「尚未提供」）：macOS 真机构建重放、标准路径构建复核、`.cipx` 安装包打包核验，按用户裁定口径留待阶段 4 端到端验收（登记：p1-10/p2-10 工部构建门 G1/G3/G4；门下省阶段 2 验收 O-8）。当前 Linux/Windows 构建产物与静态兼容证据已成立。

---

## 五、配置迁移与共存注意事项

### 5.1 不会自动迁移的内容

新插件**不读取、不迁移原插件的任何配置文件**（主配置、悬浮窗方案、AI 会话等均不迁移）。首次运行时全部选项为默认值，需在「SystemTools 设置」分组下重新配置。这是同装隔离的既定决议，避免两插件互写配置造成状态污染。

### 5.2 悬浮窗配置方案的一次性迁移

新插件自身保存的悬浮窗布局成员（横向排布、按钮顺序、按钮行、按钮/行规则集）会在**首次启动时一次性固化**为新插件独立方案存储中的「Default」方案文件（存于新插件配置目录 `FloatingWindowProfiles\Default.json`）；此后该文件已存在即跳过，不会重复迁移。该迁移只整理**新插件自己**的布局成员，不涉及原插件的方案文件，也不迁移主配置中的其他选项（FloatingWindowProfileManager.cs:48-67；Plugin.cs:195；登记 p2-03 §2-D14、p2-06 §3）。

### 5.3 设置页开关与重启提示

- **悬浮窗功能开关（「启用悬浮窗功能」）**：源版通过配置根事件间接触发重启提示；新版在开关变更时显式保存并请求重启，效果与源版一致——变更后需重启宿主生效（登记 p1-06 §9-7 等价口径；p2-03 §2-D13）。
- **AI 服务开关**：开启时保留协议确认对话框；确认/关闭后同样显式保存并请求重启。
- **功能抽屉（「启用功能选项…」）**：调整后点「应用并重启」，由**宿主设置窗口的标准重启机制**执行；源版自行替换 Windows 进程的重启路径不迁（登记 p1-06 §6-8）。
- 抽屉清单仅列出跨平台版已迁移的功能项（行动 29 + 触发器 2 + 组件 6 + 规则 4，随启用门增减）；原插件独有功能项（模拟操作、显示设置、USB、热键、语音等）不出现在该清单中。

### 5.4 共存使用建议

- 两插件功能菜单并存且名称相近（如「电源选项…」），执行时注意区分目标插件；功能 ID 不同，互不冲突。
- 重叠能力（电源/文件夹操作/悬浮窗）建议按需只在其中一个插件中启用，避免同一动作被重复触发。
- 依赖 NTFS 元数据复制（ACL/审核）的场景，用原插件执行（见第三节）。
- 新插件首次使用时请重新配置 AI 服务、虚拟放学时间等个性化选项（见 5.1）。