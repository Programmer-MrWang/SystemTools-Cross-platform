using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SystemTools.CrossPlatform.ConfigHandlers;

/// <summary>
/// 悬浮窗配置方案，仅保存悬浮窗按钮布局以及按钮/行规则集。
/// </summary>
public partial class FloatingWindowProfile : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("name")]
    private string _name = "Default";

    [ObservableProperty]
    [JsonPropertyName("floatingWindowHorizontal")]
    private bool _floatingWindowHorizontal;

    [JsonPropertyName("floatingWindowButtonOrder")]
    public List<string> FloatingWindowButtonOrder { get; set; } = new();

    [JsonPropertyName("floatingWindowButtonRows")]
    public List<List<string>> FloatingWindowButtonRows { get; set; } = new();

    [JsonPropertyName("floatingWindowButtonRulesets")]
    public Dictionary<string, ButtonRulesetConfig> FloatingWindowButtonRulesets { get; set; } = new();

    [JsonPropertyName("floatingWindowRowRulesets")]
    public List<RowRulesetConfig> FloatingWindowRowRulesets { get; set; } = new();

    public bool PruneInvalidButtonIds(IEnumerable<string> validButtonIds)
    {
        var validSet = validButtonIds.ToHashSet();
        var changed = false;

        var newOrder = FloatingWindowButtonOrder.Where(id => validSet.Contains(id)).ToList();
        if (newOrder.Count != FloatingWindowButtonOrder.Count)
        {
            FloatingWindowButtonOrder = newOrder;
            changed = true;
        }

        var newRows = FloatingWindowButtonRows
            .Select(row => row.Where(id => validSet.Contains(id)).ToList())
            .ToList();
        if (newRows.Count != FloatingWindowButtonRows.Count ||
            newRows.Zip(FloatingWindowButtonRows, (a, b) => a.SequenceEqual(b)).Any(x => !x))
        {
            FloatingWindowButtonRows = newRows;
            changed = true;
        }

        var invalidButtonConfigs = FloatingWindowButtonRulesets.Keys.Where(id => !validSet.Contains(id)).ToList();
        foreach (var id in invalidButtonConfigs)
        {
            FloatingWindowButtonRulesets.Remove(id);
            changed = true;
        }

        return changed;
    }
}
