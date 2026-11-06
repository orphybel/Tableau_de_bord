using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lanceur.Models;
using Lanceur.Services;
using Microsoft.Win32;

namespace Lanceur;

public partial class ItemEditorWindow : Window
{
    private readonly IList<LaunchGroup> _groups;
    private string _color = "";
    private bool _nameTouched;

    public LaunchItem? Result { get; private set; }
    public LaunchGroup? TargetGroup { get; private set; }

    public ItemEditorWindow(LaunchItem? item, IList<LaunchGroup> groups, LaunchGroup group)
    {
        InitializeComponent();
        _groups = groups;
        TargetGroup = group;

        if (item != null)
        {
            HeaderText.Text = "Modifier le bouton";
            NameBox.Text = item.Name;
            PathBox.Text = item.Path;
            ArgsBox.Text = item.Arguments;
            WorkDirBox.Text = item.WorkingDirectory;
            IconBox.Text = item.IconPath;
            AdminCheck.IsChecked = item.RunAsAdmin;
            _color = item.Color;
            _nameTouched = true;
        }

        BuildColorSwatches();
        BuildGroupChips();
        UpdatePreview();
        NameBox.TextChanged += (_, _) => { if (NameBox.IsKeyboardFocused) _nameTouched = true; };
        Loaded += (_, _) => (item is null ? PathBox : NameBox).Focus();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.ButtonState == MouseButtonState.Pressed) try { DragMove(); } catch { }
    }

    private void BuildColorSwatches()
    {
        foreach (var hex in ThemeService.TilePresets)
        {
            var color = ThemeService.ParseColor(hex);
            var rb = new RadioButton
            {
                Style = (Style)FindResource("SwatchRadio"),
                GroupName = "TileColor",
                Background = color is null ? Brushes.Transparent : ThemeService.MakeBrush(color.Value),
                Content = color is null ? "" : null,
                ToolTip = color is null ? "Aucune couleur" : hex,
                IsChecked = string.Equals(hex, _color, System.StringComparison.OrdinalIgnoreCase),
                Tag = hex,
            };
            rb.Checked += (_, _) => _color = (string)rb.Tag;
            ColorPanel.Children.Add(rb);
        }
        if (!ColorPanel.Children.OfType<RadioButton>().Any(r => r.IsChecked == true))
        {
            // couleur personnalisée saisie à la main dans le JSON : on la conserve
            var custom = ThemeService.ParseColor(_color);
            if (custom != null)
            {
                var rb = new RadioButton
                {
                    Style = (Style)FindResource("SwatchRadio"),
                    GroupName = "TileColor",
                    Background = ThemeService.MakeBrush(custom.Value),
                    ToolTip = _color,
                    Tag = _color,
                    IsChecked = true,
                };
                rb.Checked += (_, _) => _color = (string)rb.Tag;
                ColorPanel.Children.Add(rb);
            }
            else ((RadioButton)ColorPanel.Children[0]).IsChecked = true;
        }
    }

    private void BuildGroupChips()
    {
        foreach (var g in _groups)
        {
            var rb = new RadioButton
            {
                Style = (Style)FindResource("PillRadio"),
                GroupName = "Group",
                Content = g.Name,
                IsChecked = ReferenceEquals(g, TargetGroup),
            };
            var group = g;
            rb.Checked += (_, _) => TargetGroup = group;
            GroupPanel.Children.Add(rb);
        }
    }

    private void Fields_Changed(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded) UpdatePreview();
    }

    private void UpdatePreview()
    {
        var probe = new LaunchItem { Name = NameBox.Text, Path = PathBox.Text, IconPath = IconBox.Text };
        var icon = IconService.Get(probe);
        PreviewImage.Source = icon;
        PreviewMonogram.Text = icon is null ? probe.Monogram : "";
    }

    private void BrowseTarget_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Choisir le programme à lancer",
            Filter = "Programmes et raccourcis (*.exe;*.lnk;*.bat;*.cmd;*.ps1;*.url)|*.exe;*.lnk;*.bat;*.cmd;*.ps1;*.url|Tous les fichiers (*.*)|*.*",
            DereferenceLinks = false,
        };
        var current = PathHelper.FindExisting(PathBox.Text);
        if (current != null && File.Exists(current)) dlg.InitialDirectory = Path.GetDirectoryName(current);
        if (dlg.ShowDialog(this) != true) return;

        PathBox.Text = dlg.FileName;
        if (!_nameTouched || string.IsNullOrWhiteSpace(NameBox.Text))
            NameBox.Text = PathHelper.FriendlyName(dlg.FileName);
    }

    private void BrowseWorkDir_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Dossier de démarrage" };
        var current = PathHelper.Expand(WorkDirBox.Text);
        if (Directory.Exists(current)) dlg.InitialDirectory = current;
        if (dlg.ShowDialog(this) == true) WorkDirBox.Text = dlg.FolderName;
    }

    private void BrowseIcon_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Choisir une icône",
            Filter = "Images et icônes (*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.exe;*.dll)|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.exe;*.dll|Tous les fichiers (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) == true) IconBox.Text = dlg.FileName;
    }

    private void ResetIcon_Click(object sender, RoutedEventArgs e) => IconBox.Text = "";

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        var path = PathBox.Text.Trim();
        if (name.Length == 0 || path.Length == 0)
        {
            ShowError(name.Length == 0 ? "Donnez un nom au bouton." : "Indiquez le programme à lancer.");
            (name.Length == 0 ? NameBox : PathBox).Focus();
            return;
        }

        var expanded = PathHelper.Expand(path);
        if (!PathHelper.IsUrl(expanded) && Path.IsPathRooted(expanded) && !File.Exists(expanded) && !Directory.Exists(expanded))
        {
            if (!Dialogs.Confirm(this, "Fichier introuvable",
                    $"Ce chemin n'existe pas actuellement :\n{expanded}\n\nEnregistrer quand même ? (utile pour un disque externe ou réseau)",
                    "Enregistrer quand même", "Corriger"))
                return;
        }

        Result = new LaunchItem
        {
            Name = name,
            Path = path,
            Arguments = ArgsBox.Text.Trim(),
            WorkingDirectory = WorkDirBox.Text.Trim(),
            IconPath = IconBox.Text.Trim(),
            RunAsAdmin = AdminCheck.IsChecked == true,
            Color = _color,
        };
        DialogResult = true;
    }

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.Visibility = Visibility.Visible;
    }
}
