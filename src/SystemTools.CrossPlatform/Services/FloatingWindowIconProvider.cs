// 来源：SystemTools（主插件）Services\FloatingWindowIconProvider.cs（LGPL 同许可）。

using System;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ClassIsland.Core.Controls;
using ClassIsland.Core.Controls.IconSources;
using ClassIsland.Core.Helpers.UI;
using FluentAvalonia.UI.Controls;

namespace SystemTools.CrossPlatform.Services;

/// <summary>
/// 悬浮窗图标统一入口。完全采用 ClassIsland 的图标表达式协议：
/// 配置只保存字符串表达式（如 fluent("…")、lucide("…")、img("C:\…")），
/// 渲染时解析为 <see cref="FAIconSource"/>，并兼容旧版 /uXXXX 与单个字形字符。
/// </summary>
public static class FloatingWindowIconProvider
{
    /// <summary>
    /// 默认 Fluent 字形字符（与旧版悬浮窗触发器默认图标一致）。
    /// </summary>
    public const string DefaultFluentGlyph = "\uea37";

    private static bool _imgHandlerEnsured;
    private static readonly object ImgHandlerLock = new();

    /// <summary>
    /// 新触发器使用的默认图标表达式。
    /// </summary>
    public static string DefaultIconExpression => FormatExpression("fluent", DefaultFluentGlyph);

    /// <summary>
    /// 将参数格式化为图标表达式（转义规则与 ClassIsland 一致）。
    /// </summary>
    public static string FormatExpression(string function, string argument) =>
        $"{function}(\"{argument.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t")}\")";

    /// <summary>
    /// 是否为旧版 /uXXXX 或 \uXXXX 形式的图标标记。
    /// </summary>
    public static bool IsLegacyToken(string? raw) =>
        TryTokenToGlyph(raw) != null;

    /// <summary>
    /// 将旧版 /uXXXX / \uXXXX 标记转换为字形字符；非旧版格式返回 null。
    /// </summary>
    public static string? TryTokenToGlyph(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        if (value.Length <= 2)
        {
            return null;
        }

        if (!value.StartsWith("/u", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("\\u", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return int.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code)
            ? char.ConvertFromUtf32(code)
            : null;
    }

    /// <summary>
    /// 将任意历史格式规范化为图标表达式。旧版 /uXXXX 会转换为 fluent("…") 表达式。
    /// </summary>
    public static string NormalizeForStorage(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var glyph = TryTokenToGlyph(raw);
        return glyph != null ? FormatExpression("fluent", glyph) : raw.Trim();
    }

    /// <summary>
    /// 解析图标表达式为 <see cref="FAIconSource"/>。支持表达式、单个字形字符和旧版 /uXXXX 标记。
    /// 解析失败或为空时返回 null。
    /// </summary>
    public static FAIconSource? TryResolveIconSource(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        var glyph = TryTokenToGlyph(value);
        if (glyph != null)
        {
            value = glyph;
        }

        if (value.StartsWith("img(", StringComparison.OrdinalIgnoreCase))
        {
            EnsureImageExpressionHandler();
        }

        return IconExpressionHelper.TryParseOrNull(value);
    }

    /// <summary>
    /// 解析 func("arg") 形式的表达式并返回函数名与参数。解析失败返回 false。
    /// </summary>
    public static bool TryParseFunctionExpression(string? raw, out string function, out string argument)
    {
        function = string.Empty;
        argument = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var value = raw.Trim();
        var open = value.IndexOf('(');
        if (open <= 0 || !value.EndsWith(')') || value.Length < open + 3)
        {
            return false;
        }

        var name = value[..open].Trim();
        if (name.Length == 0)
        {
            return false;
        }

        foreach (var c in name)
        {
            if (!char.IsLetterOrDigit(c) && c is not '_' and not '.')
            {
                return false;
            }
        }

        function = name;
        argument = UnescapeArgument(value[(open + 1)..^1]);
        return true;
    }

    /// <summary>
    /// 根据图标表达式创建用于显示的图标控件。支持 Fluent / Lucide / 图片及其余已注册的图标源，
    /// 同时兼容旧版 /uXXXX 与单个字形字符；无效或为空时回退到默认图标。
    /// </summary>
    /// <param name="iconExpression">图标表达式</param>
    /// <param name="size">图标尺寸（字体图标为字号，图片为宽高）</param>
    /// <param name="foreground">前景色（仅字体图标有效）</param>
    public static Control CreateIconControl(string? iconExpression, double size, IBrush? foreground = null)
    {
        var value = string.IsNullOrWhiteSpace(iconExpression) ? null : iconExpression.Trim();
        var tokenGlyph = TryTokenToGlyph(value);
        var glyph = tokenGlyph ?? DefaultFluentGlyph;
        string? kind = tokenGlyph != null ? "fluent" : null;

        if (tokenGlyph == null && value is { Length: > 0 } && value.Length <= 2)
        {
            // 单个字形字符（或代理对），按 Fluent 处理
            kind = "fluent";
        }
        else if (TryParseFunctionExpression(value, out var function, out var argument)
                 && !string.IsNullOrWhiteSpace(argument))
        {
            kind = function.ToLowerInvariant();
            if (kind is not ("img" or "fluent" or "lucide" or "sfsymbols"))
            {
                kind = null;
            }
            else if (kind != "img")
            {
                glyph = argument;
            }
        }

        switch (kind)
        {
            case "img":
            {
                EnsureImageExpressionHandler();
                return new FAIconSourceElement
                {
                    IconSource = TryResolveIconSource(value),
                    Width = size,
                    Height = size,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            case "lucide":
                return CreateFontIcon(new LucideIcon(), glyph, size, foreground);
            default:
                return CreateFontIcon(new FluentIcon(), glyph, size, foreground);
        }
    }

    private static Control CreateFontIcon(Control icon, string glyph, double size, IBrush? foreground)
    {
        if (icon is FAFontIcon fontIcon)
        {
            fontIcon.Glyph = glyph;
            fontIcon.FontSize = size;
            if (foreground != null)
            {
                fontIcon.Foreground = foreground;
            }
        }

        icon.HorizontalAlignment = HorizontalAlignment.Center;
        icon.VerticalAlignment = VerticalAlignment.Center;
        return icon;
    }

    private static string UnescapeArgument(string raw)
    {
        var value = raw.Trim();
        if (value.Length < 2)
        {
            return value;
        }

        var quote = value[0];
        if (quote is not ('"' or '\'') || value[^1] != quote)
        {
            return value;
        }

        var sb = new StringBuilder(value.Length - 2);
        for (var i = 1; i < value.Length - 1; i++)
        {
            var c = value[i];
            if (c == '\\' && i + 1 < value.Length - 1)
            {
                i++;
                switch (value[i])
                {
                    case '\\': sb.Append('\\'); break;
                    case '"': sb.Append('"'); break;
                    case '\'': sb.Append('\''); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    default: sb.Append(value[i]); break;
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 确保注册 img 图标表达式处理器。新版本主程序已自带时（重复注册会抛异常）自动跳过；
    /// 旧版本主程序由本插件补注册，保证图片图标在所有受支持版本上可用。
    /// </summary>
    private static void EnsureImageExpressionHandler()
    {
        if (_imgHandlerEnsured)
        {
            return;
        }

        lock (ImgHandlerLock)
        {
            if (_imgHandlerEnsured)
            {
                return;
            }

            _imgHandlerEnsured = true;
            try
            {
                if (IconExpressionHelper.IconExpressionHandlers.ContainsKey("img"))
                {
                    return;
                }

                IconExpressionHelper.RegisterHandler("img", args =>
                    args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
                        ? new AdvancedImageIconSource { Uri = args[0] }
                        : null);
            }
            catch (ArgumentException)
            {
                // 主程序或其他插件已经注册过 img 处理器，跳过即可。
            }
        }
    }
}