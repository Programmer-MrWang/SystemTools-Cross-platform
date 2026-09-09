using System;

namespace SystemTools.CrossPlatform.Services;

/// <summary>
/// 插件本地进程内存维护抽象（06 条目 49 替换目标 / p2-05 §3 方案 B，尚书省裁定 3）。
/// <para>
/// <b>插件本地抽象，非宿主 API</b>：宿主 SDK 双分支（U3 本地检出 + NuGet 2.1.1.1 包二进制）对
/// IProcessMemoryMaintenanceService 均 ABSENT（p2-05 §4 检索记录），按 p1-05 §5.3-3 禁止以
/// ClassIsland.* 命名空间发明宿主接口，故落插件命名空间并于此注记偏差。
/// </para>
/// <para>语义：修剪指定进程的工作集（物理驻留统计）。仅 Windows 具备等价系统调用；
/// 不支持的平台实现应返回 false（no-op 降级，06 条目 49：Linux/macOS 默认不执行等价强制工作集清理）。</para>
/// </summary>
public interface IProcessMemoryMaintenanceService
{
    bool TryTrimWorkingSet(IntPtr processHandle);
}
