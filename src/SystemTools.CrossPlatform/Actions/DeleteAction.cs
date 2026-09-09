using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using SystemTools.CrossPlatform.Settings;

namespace SystemTools.CrossPlatform.Actions;

[ActionInfo("SystemTools.CrossPlatform.Delete", "删除", "\uE61D", false)]
public class DeleteAction(ILogger<DeleteAction> logger) : ActionBase<DeleteSettings>
{
    private readonly ILogger<DeleteAction> _logger = logger;

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("DeleteAction OnInvoke 开始");

        if (Settings == null || string.IsNullOrWhiteSpace(Settings.TargetPath))
        {
            _logger.LogWarning("路径为空");
            return;
        }

        try
        {
            var targetPath = Path.TrimEndingDirectorySeparator(Settings.TargetPath);

            if (Settings.OperationType == "文件")
            {
                if (!File.Exists(targetPath))
                {
                    _logger.LogError("文件不存在: {Path}", targetPath);
                    throw new FileNotFoundException("文件不存在", targetPath);
                }

                try
                {
                    await Task.Run(() => File.Delete(targetPath));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "文件移动失败");
                    throw new Exception($"移动失败: {ex}");
                }

                _logger.LogInformation("文件删除成功: {Path}", targetPath);
            }
            else
            {
                if (!Directory.Exists(targetPath))
                {
                    _logger.LogError("文件夹不存在: {Path}", targetPath);
                    throw new DirectoryNotFoundException($"文件夹不存在: {targetPath}");
                }

                try
                {
                    Directory.Delete(targetPath, true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "删除失败，退出条件非“目标已不存在”: {Path}", targetPath);
                    throw new Exception($"删除失败: {ex}");
                }

                _logger.LogInformation("文件夹删除成功: {Path}", targetPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除失败");
            throw;
        }

        await base.OnInvoke();
        _logger.LogDebug("DeleteAction OnInvoke 完成");
    }
}
