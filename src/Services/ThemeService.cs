using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Lanceur.Models;
using Microsoft.Win32;

namespace Lanceur.Services;

public static class ThemeService
{
    public const string DefaultAccent = "#6366F1";

    public static readonly string[] AccentPresets =
        { "#6366F1", "#3B82F6", "#0EA5E9", "#14B8A6", "#22C55E", "#EAB308", "#F97316", "#EF4444", "#EC4899", "#A855F7" };

    public static readonly string[] TilePresets =
        { "", "#EF4444", "#F97316", "#EAB308", "#22C55E", "#14B8A6", "#3B82F6", "#6366F1", "#A855F7", "#EC4899", "#64748B" };

    public static bool IsDark { get; private set; } = true;

    private static readonly Dictionary<string, string> Dark = new()
    {
        ["WindowBg"] = "#1B1B1F",
        ["SidebarBg"] = "#141417",
        ["SurfaceBg"] = "#26262C",
        ["SurfaceHoverBg"] = "#303038",
        ["SurfacePressedBg"] = "#2A2A31",
        ["InputBg"] = "#1F1F24",
        ["Stroke"] = "#34343C",
        ["TextPrimary"] = "#F3F3F6",
        ["TextSecondary"] = "#9C9CA8",
        ["Danger"] = "#F05252",
        ["DangerHover"] = "#F87171",
        ["Overlay"] = "#99000000",
    };

    private static readonly Dictionary<string, string> Light = new()
    {
        ["WindowBg"] = "#F6F7F9",
        ["SidebarBg"] = "#ECEEF2",
        ["SurfaceBg"] = "#FFFFFF",
        ["SurfaceHoverBg"] = "#F1F3F7",
        ["SurfacePressedBg"] = "#E7EAF0",
        ["InputBg"] = "#FFFFFF",
        ["Stroke"] = "#DCE0E7",
        ["TextPrimary"] = "#1B1D22",
        ["TextSecondary"] = "#667085",
        ["Danger"] = "#DC2626",
        ["DangerHover"] = "#EF4444",
        ["Overlay"] = "#55000000",
    };

    public static void Apply(AppConfig cfg)
    {
        var res = Application.Current.Resources;
        IsDark = cfg.Theme switch
        {
            "Light" => false,
            "System" => SystemPrefersDark(),
            _ => true,
        };

        foreach (var (key, hex) in IsDark ? Dark : Light)
            res[key] = MakeBrush(ParseColor(hex)!.Value);

        var accent = ParseColor(cfg.AccentColor) ?? ParseColor(DefaultAccent)!.Value;
        res["Accent"] = MakeBrush(accent);
        res["AccentHover"] = MakeBrush(Shift(accent, IsDark ? 0.14 : -0.10));
        res["AccentSoft"] = MakeBrush(Color.FromArgb(IsDark ? (byte)0x38 : (byte)0x26, accent.R, accent.G, accent.B));
        res["OnAccent"] = MakeBrush(Luminance(accent) > 0.6 ? Colors.Black : Colors.White);

        var (w, h, icon, font) = cfg.TileSize switch
        {
            "Small" => (100.0, 96.0, 34.0, 12.0),
            "Large" => (152.0, 144.0, 60.0, 14.0),
            _ => (124.0, 116.0, 46.0, 13.0),
        };
        res["TileWidth"] = w;
        res["TileHeight"] = h;
        res["TileIconSize"] = icon;
        res["TileFontSize"] = font;
    }

    public static bool SystemPrefersDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return true; }
    }

    public static Color? ParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        var s = hex.Trim().TrimStart('#');
        if (s.Length == 3) s = $"{s[0]}{s[0]}{s[1]}{s[1]}{s[2]}{s[2]}";
        if (s.Length == 6) s = "FF" + s;
        if (s.Length != 8 || !uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            return null;
        return Color.FromArgb((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    public static SolidColorBrush MakeBrush(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static Color Shift(Color c, double amount)
    {
        byte F(byte x) => (byte)Math.Clamp(amount >= 0 ? x + (255 - x) * amount : x * (1 + amount), 0, 255);
        return Color.FromRgb(F(c.R), F(c.G), F(c.B));
    }

    private static double Luminance(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
}
