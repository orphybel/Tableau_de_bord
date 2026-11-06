using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Lanceur.Models;
using Lanceur.Services;
using Microsoft.Win32;

namespace Lanceur;

public partial class SettingsWindow : Window
{
    private readonly AppConfig _s;
    private readonly MainWindow _main;
    private bool _syncingAccent;

    public AppConfig? Result { get; private set; }

    public SettingsWindow(AppConfig settings, MainWindow main)
    {
        InitializeComponent();
        _s = settings;
        _main = main;

        TitleBox.Text = _s.Title;
        (_s.Theme switch { "Light" => ThemeLight, "System" => ThemeSystem, _ => ThemeDark }).IsChecked = true;
        (_s.TileSize switch { "Small" => SizeSmall, "Large" => SizeLarge, _ => SizeMedium }).IsChecked = true;

        foreach (var hex in ThemeService.AccentPresets)
        {
            var rb = new RadioButton
            {
                Style = (Style)FindResource("SwatchRadio"),
                GroupName = "Accent",
                Background = ThemeService.MakeBrush(ThemeService.ParseColor(hex)!.Value),
                ToolTip = hex,
                Tag = hex,
            };
            rb.Checked += (_, _) =>
            {
                if (_syncingAccent) return;
                _syncingAccent = true;
                AccentBox.Text = (string)rb.Tag;
                _syncingAccent = false;
            };
            AccentPanel.Children.Add(rb);
        }
        AccentBox.Text = _s.AccentColor;

        TopmostCheck.IsChecked = _s.AlwaysOnTop;
        MinimizeAfterLaunchCheck.IsChecked = _s.MinimizeAfterLaunch;
        TrayCheck.IsChecked = _s.ShowTrayIcon;
        CloseToTrayCheck.IsChecked = _s.CloseToTray;
        StartupCheck.IsChecked = _s.StartWithWindows;
        StartMinimizedCheck.IsChecked = _s.StartMinimized;
        HotkeyCheck.IsChecked = _s.HotkeyEnabled;
        HotkeyBox.Text = _s.Hotkey;

        ConfigPathText.Text = (ConfigService.IsPortable ? "Mode portable — fichier : " : "Fichier de configuration : ")
                              + ConfigService.ConfigPath;
        UpdateDependencies();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.ButtonState == MouseButtonState.Pressed) try { DragMove(); } catch { }
    }

    private void UpdateDependencies()
    {
        CloseToTrayCheck.IsEnabled = TrayCheck.IsChecked == true;
        StartMinimizedCheck.IsEnabled = StartupCheck.IsChecked == true;
        HotkeyBox.IsEnabled = HotkeyCheck.IsChecked == true;
    }

    private void Tray_Changed(object sender, RoutedEventArgs e) => UpdateDependencies();
    private void Startup_Changed(object sender, RoutedEventArgs e) => UpdateDependencies();
    private void Hotkey_Changed(object sender, RoutedEventArgs e) => UpdateDependencies();

    private void AccentBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var c = ThemeService.ParseColor(AccentBox.Text);
        AccentPreview.Background = c is null ? null : ThemeService.MakeBrush(c.Value);
        if (_syncingAccent) return;
        _syncingAccent = true;
        foreach (var rb in AccentPanel.Children.OfType<RadioButton>())
            rb.IsChecked = string.Equals((string)rb.Tag, AccentBox.Text.Trim(), StringComparison.OrdinalIgnoreCase);
        _syncingAccent = false;
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Tab) { e.Handled = false; return; }
        if (key is Key.Back or Key.Delete) { HotkeyBox.Text = ""; return; }

        var win = Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin);
        var isModifier = key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                         or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;
        HotkeyBox.Text = HotkeyService.Format(Keyboard.Modifiers, win, isModifier ? Key.None : key);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Exporter la configuration",
            FileName = "lanceur.config.json",
            Filter = "Configuration Lanceur (*.json)|*.json",
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            ConfigService.Export(_main.CurrentConfig, dlg.FileName);
            Dialogs.Info(this, "Export terminé", $"Configuration exportée vers :\n{dlg.FileName}");
        }
        catch (Exception ex)
        {
            Dialogs.Info(this, "Export impossible", ex.Message);
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Importer une configuration",
            Filter = "Configuration Lanceur (*.json)|*.json|Tous les fichiers (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) != true) return;
        if (!Dialogs.Confirm(this, "Importer la configuration",
                "Tous vos groupes, boutons et paramètres actuels seront remplacés par ceux du fichier. Continuer ?",
                "Importer", "Annuler", danger: true))
            return;
        if (_main.ImportConfig(dlg.FileName))
            DialogResult = false; // la configuration importée remplace ces réglages
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try { LaunchService.OpenFolder(Path.GetDirectoryName(ConfigService.ConfigPath)!); }
        catch (Exception ex) { Dialogs.Info(this, "Dossier", ex.Message); }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var accent = AccentBox.Text.Trim();
        if (ThemeService.ParseColor(accent) is null)
        {
            Dialogs.Info(this, "Couleur invalide", "La couleur d'accentuation doit être au format #RRGGBB (ex. #6366F1).");
            AccentBox.Focus();
            return;
        }
        if (HotkeyCheck.IsChecked == true && !HotkeyService.TryParse(HotkeyBox.Text, out _, out _))
        {
            Dialogs.Info(this, "Raccourci incomplet",
                "Le raccourci doit combiner au moins une touche de modification (Ctrl, Alt, Shift, Win) et une autre touche.");
            HotkeyBox.Focus();
            return;
        }

        _s.Title = TitleBox.Text.Trim();
        _s.Theme = ThemeLight.IsChecked == true ? "Light" : ThemeSystem.IsChecked == true ? "System" : "Dark";
        _s.TileSize = SizeSmall.IsChecked == true ? "Small" : SizeLarge.IsChecked == true ? "Large" : "Medium";
        _s.AccentColor = accent.StartsWith('#') ? accent.ToUpperInvariant() : "#" + accent.ToUpperInvariant();
        _s.AlwaysOnTop = TopmostCheck.IsChecked == true;
        _s.MinimizeAfterLaunch = MinimizeAfterLaunchCheck.IsChecked == true;
        _s.ShowTrayIcon = TrayCheck.IsChecked == true;
        _s.CloseToTray = CloseToTrayCheck.IsChecked == true;
        _s.StartWithWindows = StartupCheck.IsChecked == true;
        _s.StartMinimized = StartMinimizedCheck.IsChecked == true;
        _s.HotkeyEnabled = HotkeyCheck.IsChecked == true;
        if (_s.HotkeyEnabled) _s.Hotkey = HotkeyBox.Text;

        Result = _s;
        DialogResult = true;
    }
}
