using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using SystemTools.CrossPlatform.Settings;

namespace SystemTools.CrossPlatform.Actions;

[ActionInfo("SystemTools.CrossPlatform.Move", "移动", "\uE6E7", false)]
public class MoveAction(ILogger<MoveAction> logger) : ActionBase<MoveSettings>
{
    private readonly ILogger<MoveAction> _logger = logger;

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("MoveAction OnInvoke 开始");

        if (Settings == null || string.IsNullOrWhiteSpace(Settings.SourcePath) ||
            string.IsNullOrWhiteSpace(Settings.DestinationPath))
        {
            _logger.LogWarning("路径为空");
            return;
        }

        try
        {
            var sourcePath = Path.TrimEndingDirectorySeparator(Settings.SourcePath);
            var destPath = Path.TrimEndingDirectorySeparator(Settings.DestinationPath);

            if (Settings.OperationType == "文件")
            {
                if (!File.Exists(sourcePath))
                {
                    _logger.LogError("源文件不存在: {Path}", sourcePath);
                    throw new FileNotFoundException("源文件不存在", sourcePath);
                }

                if (Directory.Exists(destPath))
                {
                    var fileName = Path.GetFileName(sourcePath);
                    destPath = Path.Combine(destPath, fileName);
                }

                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                try
                {
                    await Task.Run(() => File.Move(sourcePath, destPath));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "文件移动失败");
                    throw new Exception($"移动失败: {ex}");
                }

                _logger.LogInformation("文件移动成功: {Source} -> {Destination}", sourcePath, destPath);
            }
            else
            {
                if (!Directory.Exists(sourcePath))
                {
                    _logger.LogError("源文件夹不存在: {Path}", sourcePath);
                    throw new DirectoryNotFoundException($"源文件夹不存在: {sourcePath}");
                }

                if (!Directory.Exists(destPath))
                {
                    Directory.CreateDirectory(destPath);
                }

                var sourceDirName = new DirectoryInfo(sourcePath).Name;
                var finalDestPath = Path.Combine(destPath, sourceDirName);

                if (Directory.Exists(finalDestPath))
                {
                    Directory.Delete(finalDestPath, true);
                }

                try
                {
                    await Task.Run(() => Directory.Move(sourcePath, finalDestPath));
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "同卷移动不可用，回退为递归复制后删除源目录: {Source} -> {Destination}",
                        sourcePath, finalDestPath);
                    await Task.Run(() => CopyDirectoryRecursive(sourcePath, finalDestPath));
                    try
                    {
                        Directory.Delete(sourcePath, true);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogError(deleteEx, "目标已复制完成，但源目录删除失败，移动未完整完成: {Source}", sourcePath);
                        throw new Exception($"移动失败: {deleteEx}");
                    }
                }

                _logger.LogInformation("文件夹移动成功: {Source} -> {Destination}", sourcePath, finalDestPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "移动失败");
            throw;
        }

        await base.OnInvoke();
        _logger.LogDebug("MoveAction OnInvoke 完成");
    }

    private void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
        }

        foreach (var dir in Directory.EnumerateDirectories(sourceDir))
        {
            var target = Path.Combine(destDir, Path.GetFileName(dir));
            if (IsSamePath(dir, target))
            {
                continue;
            }

            CopyDirectoryRecursive(dir, target);
        }
    }

    private static bool IsSamePath(string a, string b)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), comparison);
    }
}
