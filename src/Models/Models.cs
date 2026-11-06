using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace Lanceur.Models;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

/// <summary>Un bouton du tableau de bord.</summary>
public class LaunchItem : ObservableObject
{
    private string _name = "";
    private string _path = "";
    private string _arguments = "";
    private string _workingDirectory = "";
    private bool _runAsAdmin;
    private string _iconPath = "";
    private string _color = "";
    private ImageSource? _icon;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name
    {
        get => _name;
        set { if (Set(ref _name, value ?? "")) { OnPropertyChanged(nameof(Monogram)); OnPropertyChanged(nameof(Tooltip)); } }
    }

    /// <summary>Chemin du programme (.exe, .lnk, .bat, dossier, URL…). Les variables %ENV% sont acceptées.</summary>
    public string Path
    {
        get => _path;
        set { if (Set(ref _path, value ?? "")) OnPropertyChanged(nameof(Tooltip)); }
    }

    public string Arguments
    {
        get => _arguments;
        set { if (Set(ref _arguments, value ?? "")) OnPropertyChanged(nameof(Tooltip)); }
    }

    public string WorkingDirectory
    {
        get => _workingDirectory;
        set => Set(ref _workingDirectory, value ?? "");
    }

    public bool RunAsAdmin
    {
        get => _runAsAdmin;
        set => Set(ref _runAsAdmin, value);
    }

    /// <summary>Icône personnalisée (.ico, .png, .jpg, ou un .exe/.dll). Vide = icône du programme.</summary>
    public string IconPath
    {
        get => _iconPath;
        set => Set(ref _iconPath, value ?? "");
    }

    /// <summary>Couleur de la tuile au format #RRGGBB. Vide = aucune.</summary>
    public string Color
    {
        get => _color;
        set { if (Set(ref _color, value ?? "")) { OnPropertyChanged(nameof(TintBrush)); OnPropertyChanged(nameof(ColorBrush)); } }
    }

    [JsonIgnore]
    public ImageSource? Icon
    {
        get => _icon;
        set => Set(ref _icon, value);
    }

    [JsonIgnore]
    public string Monogram => string.IsNullOrWhiteSpace(Name) ? "?" : Name.Trim()[..1].ToUpperInvariant();

    [JsonIgnore]
    public string Tooltip
    {
        get
        {
            var target = string.IsNullOrWhiteSpace(Arguments) ? Path : $"{Path} {Arguments}";
            return $"{Name}\n{target}";
        }
    }

    [JsonIgnore]
    public Brush TintBrush
    {
        get
        {
            var c = Services.ThemeService.ParseColor(Color);
            if (c is null) return Brushes.Transparent;
            var b = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x38, c.Value.R, c.Value.G, c.Value.B));
            b.Freeze();
            return b;
        }
    }

    [JsonIgnore]
    public Brush? ColorBrush
    {
        get
        {
            var c = Services.ThemeService.ParseColor(Color);
            if (c is null) return null;
            var b = new SolidColorBrush(c.Value);
            b.Freeze();
            return b;
        }
    }

    public LaunchItem Clone() => new()
    {
        Name = Name,
        Path = Path,
        Arguments = Arguments,
        WorkingDirectory = WorkingDirectory,
        RunAsAdmin = RunAsAdmin,
        IconPath = IconPath,
        Color = Color,
        Icon = Icon,
    };

    public void CopyFrom(LaunchItem o)
    {
        Name = o.Name;
        Path = o.Path;
        Arguments = o.Arguments;
        WorkingDirectory = o.WorkingDirectory;
        RunAsAdmin = o.RunAsAdmin;
        IconPath = o.IconPath;
        Color = o.Color;
    }
}

/// <summary>Un groupe (onglet) de boutons.</summary>
public class LaunchGroup : ObservableObject
{
    private string _name = "";

    public string Name
    {
        get => _name;
        set => Set(ref _name, value ?? "");
    }

    public ObservableCollection<LaunchItem> Items { get; set; } = new();
}

public class WindowPlacement
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Maximized { get; set; }
}

/// <summary>Toute la configuration, sérialisée en JSON.</summary>
public class AppConfig
{
    public int Version { get; set; } = 1;

    // Apparence
    public string Title { get; set; } = "Mon tableau de bord";
    public string Theme { get; set; } = "Dark";          // Dark | Light | System
    public string AccentColor { get; set; } = "#6366F1";
    public string TileSize { get; set; } = "Medium";     // Small | Medium | Large

    // Comportement
    public bool AlwaysOnTop { get; set; }
    public bool MinimizeAfterLaunch { get; set; }
    public bool ShowTrayIcon { get; set; } = true;
    public bool CloseToTray { get; set; }
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public bool HotkeyEnabled { get; set; } = true;
    public string Hotkey { get; set; } = "Ctrl+Alt+Space";

    public WindowPlacement? Window { get; set; }
    public int SelectedGroupIndex { get; set; }

    public ObservableCollection<LaunchGroup> Groups { get; set; } = new();

    public AppConfig CloneSettings() => new()
    {
        Title = Title,
        Theme = Theme,
        AccentColor = AccentColor,
        TileSize = TileSize,
        AlwaysOnTop = AlwaysOnTop,
        MinimizeAfterLaunch = MinimizeAfterLaunch,
        ShowTrayIcon = ShowTrayIcon,
        CloseToTray = CloseToTray,
        StartWithWindows = StartWithWindows,
        StartMinimized = StartMinimized,
        HotkeyEnabled = HotkeyEnabled,
        Hotkey = Hotkey,
    };

    public void ApplySettings(AppConfig s)
    {
        Title = s.Title;
        Theme = s.Theme;
        AccentColor = s.AccentColor;
        TileSize = s.TileSize;
        AlwaysOnTop = s.AlwaysOnTop;
        MinimizeAfterLaunch = s.MinimizeAfterLaunch;
        ShowTrayIcon = s.ShowTrayIcon;
        CloseToTray = s.CloseToTray;
        StartWithWindows = s.StartWithWindows;
        StartMinimized = s.StartMinimized;
        HotkeyEnabled = s.HotkeyEnabled;
        Hotkey = s.Hotkey;
    }
}
