using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Lanceur.Models;
using Lanceur.Services;
using WF = System.Windows.Forms;

namespace Lanceur;

public partial class MainWindow : Window
{
    private const string DragFormat = "Lanceur.Item";

    private AppConfig _config;
    private LaunchGroup? _currentGroup;
    private readonly HotkeyService _hotkey = new();
    private WF.NotifyIcon? _tray;
    private bool _reallyClose;

    private LaunchItem? _pressedItem;
    private Point _pressPoint;

    private bool IsSearching => SearchBox.Text.Trim().Length > 0;

    public MainWindow(AppConfig config)
    {
        _config = config;
        InitializeComponent();

        SourceInitialized += (_, _) => NativeWindow.ApplyFrame(this, ThemeService.IsDark);
        StateChanged += (_, _) => UpdateMaximizedState();
        Loaded += (_, _) => UpdateMaximizedState();
        Closing += OnClosing;
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewTextInput += OnPreviewTextInput;
        _hotkey.Pressed += ToggleVisibility;
    }

    // =====================================================================
    //  Démarrage / affichage
    // =====================================================================

    public void Start(bool startHidden)
    {
        RestorePlacement();
        _hotkey.Attach(this);
        ApplyAll(loadIcons: true);

        if (startHidden && _config.ShowTrayIcon)
            return; // reste dans la zone de notification

        if (startHidden)
            WindowState = WindowState.Minimized;
        Show();
        if (!startHidden) SearchBox.Focus();
    }

    public void ShowAndActivate()
    {
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized)
            WindowState = _config.Window?.Maximized == true ? WindowState.Maximized : WindowState.Normal;
        Activate();
        Topmost = true;                 // force le premier plan…
        Topmost = _config.AlwaysOnTop;  // …puis remet le réglage choisi
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void HideOrMinimize()
    {
        if (_config.ShowTrayIcon) Hide();
        else WindowState = WindowState.Minimized;
    }

    private void ToggleVisibility()
    {
        if (IsVisible && IsActive && WindowState != WindowState.Minimized) HideOrMinimize();
        else ShowAndActivate();
    }

    /// <summary>Applique toute la configuration à l'interface.</summary>
    private void ApplyAll(bool loadIcons)
    {
        ThemeService.Apply(_config);
        NativeWindow.ApplyFrame(this, ThemeService.IsDark);
        Topmost = _config.AlwaysOnTop;
        UpdatePinButton();
        TitleText.Text = string.IsNullOrWhiteSpace(_config.Title) ? "Mon tableau de bord" : _config.Title;
        Title = TitleText.Text;

        if (loadIcons) LoadIcons();

        GroupList.ItemsSource = _config.Groups;
        var idx = Math.Clamp(_config.SelectedGroupIndex, 0, _config.Groups.Count - 1);
        GroupList.SelectedIndex = idx;
        _currentGroup = _config.Groups.ElementAtOrDefault(idx);

        SetupTray();
        RegisterHotkey(showError: false);
        RefreshTiles();
    }

    private void LoadIcons()
    {
        foreach (var item in _config.Groups.SelectMany(g => g.Items))
            item.Icon = IconService.Get(item);
    }

    private void RegisterHotkey(bool showError)
    {
        _hotkey.Unregister();
        if (!_config.HotkeyEnabled) return;
        if (!_hotkey.Register(_config.Hotkey) && showError)
            Dialogs.Info(this, "Raccourci indisponible",
                $"Le raccourci « {_config.Hotkey} » n'a pas pu être enregistré.\n" +
                "Il est probablement déjà utilisé par Windows ou une autre application. Choisissez-en un autre dans les paramètres.");
    }

    // =====================================================================
    //  Affichage des tuiles
    // =====================================================================

    private void RefreshTiles()
    {
        var query = SearchBox.Text.Trim();
        ClearSearchButton.Visibility = query.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        if (query.Length > 0)
        {
            var q = Normalize(query);
            var results = _config.Groups.SelectMany(g => g.Items)
                .Where(i => Normalize(i.Name).Contains(q) || Normalize(i.Path).Contains(q))
                .ToList();
            TilesList.ItemsSource = results;
            SubtitleText.Text = results.Count switch
            {
                0 => $"Aucun résultat pour « {query} »",
                1 => $"1 résultat pour « {query} » — Entrée pour lancer",
                _ => $"{results.Count} résultats pour « {query} » — Entrée lance le premier",
            };
            SetEmptyState(results.Count == 0, "", "Aucun résultat", "Essayez un autre nom ou une partie du chemin du programme.");
        }
        else
        {
            TilesList.ItemsSource = _currentGroup?.Items;
            var n = _currentGroup?.Items.Count ?? 0;
            SubtitleText.Text = $"{_currentGroup?.Name} · {n} application{(n > 1 ? "s" : "")}";
            SetEmptyState(n == 0, "", "Ce groupe est vide",
                "Glissez-déposez un programme (.exe, raccourci, dossier) ici, ou cliquez sur « Ajouter ».");
        }
    }

    private void SetEmptyState(bool visible, string glyph, string title, string text)
    {
        EmptyState.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        EmptyGlyph.Text = glyph;
        EmptyTitle.Text = title;
        EmptyText.Text = text;
    }

    private static string Normalize(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder();
        foreach (var c in s.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    private void ShowToast(string message)
    {
        ToastText.Text = message;
        var anim = new DoubleAnimationUsingKeyFrames();
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1900))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2300))));
        Toast.BeginAnimation(OpacityProperty, anim);
    }

    private void Save()
    {
        try
        {
            ConfigService.Save(_config);
        }
        catch (Exception ex)
        {
            Dialogs.Info(this, "Enregistrement impossible", $"La configuration n'a pas pu être enregistrée :\n{ex.Message}");
        }
        BuildTrayMenu();
    }

    // =====================================================================
    //  Lancement
    // =====================================================================

    private void Launch(LaunchItem item, bool asAdmin = false)
    {
        var error = LaunchService.Launch(item, asAdmin);
        if (error is null)
        {
            ShowToast($"{item.Name} lancé");
            if (_config.MinimizeAfterLaunch) HideOrMinimize();
        }
        else if (error.Length > 0)
        {
            if (Dialogs.Confirm(this, "Impossible de lancer", error, "Modifier le bouton", "Fermer"))
                EditItem(item);
        }
    }

    // =====================================================================
    //  Tuiles : clic, glisser-déposer
    // =====================================================================

    private void Tile_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressedItem = (sender as FrameworkElement)?.DataContext as LaunchItem;
        _pressPoint = e.GetPosition(this);
    }

    private void Tile_MouseMove(object sender, MouseEventArgs e)
    {
        if (_pressedItem is null || e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(this);
        if (Math.Abs(p.X - _pressPoint.X) < SystemParameters.MinimumHorizontalDragDistance * 2 &&
            Math.Abs(p.Y - _pressPoint.Y) < SystemParameters.MinimumVerticalDragDistance * 2)
            return;

        var item = _pressedItem;
        _pressedItem = null;
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(DragFormat, item), DragDropEffects.Move);
    }

    private void Tile_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var item = (sender as FrameworkElement)?.DataContext as LaunchItem;
        if (item != null && ReferenceEquals(item, _pressedItem))
            Launch(item);
        _pressedItem = null;
    }

    private void TileEdit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is LaunchItem item) EditItem(item);
    }

    private void Area_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DragFormat) ? DragDropEffects.Move
                  : e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy
                  : DragDropEffects.None;
        e.Handled = true;
    }

    private void Tile_DragEnter(object sender, DragEventArgs e)
    {
        if (sender is Border b) b.BorderBrush = (Brush)FindResource("Accent");
    }

    private void Tile_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border b) b.ClearValue(Border.BorderBrushProperty);
    }

    private void Tile_Drop(object sender, DragEventArgs e)
    {
        Tile_DragLeave(sender, e);
        e.Handled = true;
        var target = (sender as FrameworkElement)?.DataContext as LaunchItem;

        if (e.Data.GetData(DragFormat) is LaunchItem dragged)
        {
            if (target is null || ReferenceEquals(dragged, target)) return;
            var group = FindGroup(dragged);
            if (group is null || !ReferenceEquals(group, FindGroup(target))) return;
            group.Items.Move(group.Items.IndexOf(dragged), group.Items.IndexOf(target));
            Save();
        }
        else if (e.Data.GetData(DataFormats.FileDrop) is string[] files && _currentGroup != null)
        {
            var index = target != null ? _currentGroup.Items.IndexOf(target) : -1;
            AddFiles(files, _currentGroup, index);
        }
    }

    private void Area_Drop(object sender, DragEventArgs e)
    {
        if (_currentGroup is null) return;
        if (e.Data.GetData(DragFormat) is LaunchItem dragged)
        {
            if (IsSearching || !ReferenceEquals(FindGroup(dragged), _currentGroup)) return;
            _currentGroup.Items.Move(_currentGroup.Items.IndexOf(dragged), _currentGroup.Items.Count - 1);
            Save();
        }
        else if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            AddFiles(files, _currentGroup, -1);
        }
    }

    private void AddFiles(string[] files, LaunchGroup group, int index)
    {
        var added = 0;
        foreach (var path in files)
        {
            var item = new LaunchItem { Name = PathHelper.FriendlyName(path), Path = path };
            item.Icon = IconService.Get(item);
            if (index >= 0 && index <= group.Items.Count) group.Items.Insert(index++, item);
            else group.Items.Add(item);
            added++;
        }
        if (added == 0) return;
        if (IsSearching) SearchBox.Clear();
        SelectGroup(group);
        Save();
        ShowToast(added == 1 ? "1 bouton ajouté" : $"{added} boutons ajoutés");
    }

    private LaunchGroup? FindGroup(LaunchItem item) => _config.Groups.FirstOrDefault(g => g.Items.Contains(item));

    // =====================================================================
    //  Menu contextuel des tuiles
    // =====================================================================

    /// <summary>Retrouve l'élément ciblé par un menu contextuel (via la cible du menu, fiable même si le menu est partagé).</summary>
    private static T? MenuTarget<T>(object sender) where T : class
    {
        if (sender is MenuItem mi)
        {
            DependencyObject? node = mi;
            while (node != null && node is not System.Windows.Controls.ContextMenu) node = LogicalTreeHelper.GetParent(node) ?? VisualTreeHelper.GetParent(node);
            if (node is System.Windows.Controls.ContextMenu cm && cm.PlacementTarget is FrameworkElement fe && fe.DataContext is T t) return t;
            return mi.DataContext as T;
        }
        return null;
    }

    private void MenuLaunch_Click(object sender, RoutedEventArgs e)
    {
        if (MenuTarget<LaunchItem>(sender) is { } item) Launch(item);
    }

    private void MenuLaunchAdmin_Click(object sender, RoutedEventArgs e)
    {
        if (MenuTarget<LaunchItem>(sender) is { } item) Launch(item, asAdmin: true);
    }

    private void MenuOpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (MenuTarget<LaunchItem>(sender) is not { } item) return;
        if (PathHelper.FindExisting(item.Path) is null)
            Dialogs.Info(this, "Emplacement introuvable", $"Le fichier n'existe pas :\n{PathHelper.Expand(item.Path)}");
        else
            LaunchService.OpenLocation(item);
    }

    private void MenuEdit_Click(object sender, RoutedEventArgs e)
    {
        if (MenuTarget<LaunchItem>(sender) is { } item) EditItem(item);
    }

    private void MenuDuplicate_Click(object sender, RoutedEventArgs e)
    {
        if (MenuTarget<LaunchItem>(sender) is not { } item || FindGroup(item) is not { } group) return;
        var copy = item.Clone();
        copy.Name = item.Name + " (copie)";
        group.Items.Insert(group.Items.IndexOf(item) + 1, copy);
        Save();
        RefreshTiles();
    }

    private void MenuDelete_Click(object sender, RoutedEventArgs e)
    {
        if (MenuTarget<LaunchItem>(sender) is not { } item || FindGroup(item) is not { } group) return;
        if (!Dialogs.Confirm(this, "Supprimer le bouton", $"Supprimer « {item.Name} » du tableau de bord ?\nLe programme lui-même n'est pas touché.",
                "Supprimer", "Annuler", danger: true))
            return;
        group.Items.Remove(item);
        Save();
        RefreshTiles();
    }

    // =====================================================================
    //  Ajout / modification
    // =====================================================================

    private void AddItem_Click(object sender, RoutedEventArgs e) => AddItem();

    private void AddItem()
    {
        var dlg = new ItemEditorWindow(null, _config.Groups, _currentGroup ?? _config.Groups[0]) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Result is null || dlg.TargetGroup is null) return;
        var item = dlg.Result;
        item.Icon = IconService.Get(item);
        dlg.TargetGroup.Items.Add(item);
        if (IsSearching) SearchBox.Clear();
        SelectGroup(dlg.TargetGroup);
        Save();
        RefreshTiles();
        ShowToast($"« {item.Name} » ajouté");
    }

    private void EditItem(LaunchItem item)
    {
        var group = FindGroup(item);
        if (group is null) return;
        var dlg = new ItemEditorWindow(item, _config.Groups, group) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Result is null || dlg.TargetGroup is null) return;

        item.CopyFrom(dlg.Result);
        IconService.Clear();
        item.Icon = IconService.Get(item);
        if (!ReferenceEquals(group, dlg.TargetGroup))
        {
            group.Items.Remove(item);
            dlg.TargetGroup.Items.Add(item);
        }
        Save();
        RefreshTiles();
    }

    // =====================================================================
    //  Groupes
    // =====================================================================

    private void SelectGroup(LaunchGroup group)
    {
        GroupList.SelectedItem = group;
        _currentGroup = group;
    }

    private void GroupList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GroupList.SelectedItem is not LaunchGroup g) return;
        _currentGroup = g;
        _config.SelectedGroupIndex = GroupList.SelectedIndex;
        if (IsSearching) SearchBox.Clear(); // déclenche RefreshTiles
        else RefreshTiles();
    }

    private void AddGroup_Click(object sender, RoutedEventArgs e)
    {
        var name = Dialogs.Input(this, "Nouveau groupe", "Nom du groupe", "");
        if (string.IsNullOrWhiteSpace(name)) return;
        var g = new LaunchGroup { Name = name.Trim() };
        _config.Groups.Add(g);
        SelectGroup(g);
        Save();
    }

    private void RenameGroup(LaunchGroup g)
    {
        var name = Dialogs.Input(this, "Renommer le groupe", "Nom du groupe", g.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        g.Name = name.Trim();
        Save();
        RefreshTiles();
    }

    private void GroupItem_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is LaunchGroup g) RenameGroup(g);
    }

    private void GroupRename_Click(object sender, RoutedEventArgs e)
    {
        if (MenuTarget<LaunchGroup>(sender) is { } g) RenameGroup(g);
    }

    private void GroupUp_Click(object sender, RoutedEventArgs e) => MoveGroup(MenuTarget<LaunchGroup>(sender), -1);

    private void GroupDown_Click(object sender, RoutedEventArgs e) => MoveGroup(MenuTarget<LaunchGroup>(sender), +1);

    private void MoveGroup(LaunchGroup? g, int delta)
    {
        if (g is null) return;
        var i = _config.Groups.IndexOf(g);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= _config.Groups.Count) return;
        _config.Groups.Move(i, j);
        SelectGroup(g);
        Save();
    }

    private void GroupDelete_Click(object sender, RoutedEventArgs e)
    {
        if (MenuTarget<LaunchGroup>(sender) is not { } g) return;
        if (_config.Groups.Count <= 1)
        {
            Dialogs.Info(this, "Suppression impossible", "Il faut garder au moins un groupe. Vous pouvez le renommer.");
            return;
        }
        var msg = g.Items.Count == 0
            ? $"Supprimer le groupe « {g.Name} » ?"
            : $"Supprimer le groupe « {g.Name} » et ses {g.Items.Count} bouton(s) ?\nLes programmes eux-mêmes ne sont pas touchés.";
        if (!Dialogs.Confirm(this, "Supprimer le groupe", msg, "Supprimer", "Annuler", danger: true)) return;

        var idx = _config.Groups.IndexOf(g);
        _config.Groups.Remove(g);
        SelectGroup(_config.Groups[Math.Min(idx, _config.Groups.Count - 1)]);
        Save();
        RefreshTiles();
    }

    private void GroupItem_DragEnter(object sender, DragEventArgs e)
    {
        if (sender is ListBoxItem li) li.Opacity = 0.6;
    }

    private void GroupItem_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is ListBoxItem li) li.ClearValue(OpacityProperty);
    }

    private void GroupItem_Drop(object sender, DragEventArgs e)
    {
        GroupItem_DragLeave(sender, e);
        e.Handled = true;
        if ((sender as FrameworkElement)?.DataContext is not LaunchGroup target) return;

        if (e.Data.GetData(DragFormat) is LaunchItem dragged)
        {
            var source = FindGroup(dragged);
            if (source is null || ReferenceEquals(source, target)) return;
            source.Items.Remove(dragged);
            target.Items.Add(dragged);
            Save();
            RefreshTiles();
            ShowToast($"« {dragged.Name} » déplacé vers {target.Name}");
        }
        else if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            AddFiles(files, target, -1);
        }
    }

    // =====================================================================
    //  Recherche & clavier
    // =====================================================================

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshTiles();

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        SearchBox.Focus();
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && IsSearching && TilesList.Items.Count > 0 && TilesList.Items[0] is LaunchItem first)
        {
            Launch(first, asAdmin: Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers == ModifierKeys.Control;
        if (ctrl && e.Key == Key.F) { SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; }
        else if (ctrl && e.Key == Key.N) { AddItem(); e.Handled = true; }
        else if (ctrl && e.Key == Key.OemComma) { OpenSettings(); e.Handled = true; }
        else if (ctrl && e.Key >= Key.D1 && e.Key <= Key.D9)
        {
            var i = e.Key - Key.D1;
            if (i < _config.Groups.Count) SelectGroup(_config.Groups[i]);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && IsSearching) { SearchBox.Clear(); e.Handled = true; }
    }

    /// <summary>Taper n'importe où commence une recherche.</summary>
    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox || string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0])) return;
        SearchBox.Focus();
        SearchBox.Text += e.Text;
        SearchBox.CaretIndex = SearchBox.Text.Length;
        e.Handled = true;
    }

    // =====================================================================
    //  Paramètres
    // =====================================================================

    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private void OpenSettings()
    {
        var dlg = new SettingsWindow(_config.CloneSettings(), this) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Result is null) return;

        var startupChanged = dlg.Result.StartWithWindows != _config.StartWithWindows;
        var hotkeyChanged = dlg.Result.Hotkey != _config.Hotkey || dlg.Result.HotkeyEnabled != _config.HotkeyEnabled;
        _config.ApplySettings(dlg.Result);

        if (startupChanged)
        {
            try { StartupService.Apply(_config.StartWithWindows); }
            catch (Exception ex) { Dialogs.Info(this, "Démarrage automatique", $"Impossible de modifier le démarrage automatique :\n{ex.Message}"); }
        }

        ApplyAll(loadIcons: false);
        if (hotkeyChanged) RegisterHotkey(showError: true);
        Save();
        ShowToast("Paramètres enregistrés");
    }

    /// <summary>Remplace toute la configuration par celle d'un fichier (appelé depuis les paramètres).</summary>
    public bool ImportConfig(string path)
    {
        AppConfig imported;
        try { imported = ConfigService.Import(path); }
        catch (Exception ex)
        {
            Dialogs.Info(this, "Import impossible", $"Ce fichier n'est pas une configuration valide :\n{ex.Message}");
            return false;
        }
        imported.Window = _config.Window;
        _config = imported;
        try { StartupService.Apply(_config.StartWithWindows); } catch { }
        IconService.Clear();
        SearchBox.Clear();
        ApplyAll(loadIcons: true);
        Save();
        ShowToast("Configuration importée");
        return true;
    }

    public AppConfig CurrentConfig => _config;

    // =====================================================================
    //  Zone de notification
    // =====================================================================

    private void SetupTray()
    {
        if (!_config.ShowTrayIcon)
        {
            _tray?.Dispose();
            _tray = null;
            return;
        }

        if (_tray is null)
        {
            _tray = new WF.NotifyIcon { Icon = LoadTrayIcon(), Visible = true };
            _tray.MouseClick += (_, e) => { if (e.Button == WF.MouseButtons.Left) ToggleVisibility(); };
        }
        var text = string.IsNullOrWhiteSpace(_config.Title) ? "Lanceur" : _config.Title;
        _tray.Text = text.Length > 63 ? text[..63] : text;
        BuildTrayMenu();
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        var res = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
        using var stream = res!.Stream;
        return new System.Drawing.Icon(stream, WF.SystemInformation.SmallIconSize);
    }

    private void BuildTrayMenu()
    {
        if (_tray is null) return;
        var menu = new WF.ContextMenuStrip { ShowImageMargin = false };
        menu.Items.Add("Ouvrir le tableau de bord", null, (_, _) => ShowAndActivate());
        menu.Items.Add(new WF.ToolStripSeparator());

        foreach (var group in _config.Groups)
        {
            var sub = new WF.ToolStripMenuItem(group.Name);
            ((WF.ToolStripDropDownMenu)sub.DropDown).ShowImageMargin = false;
            foreach (var item in group.Items)
            {
                var it = item;
                sub.DropDownItems.Add(it.Name.Replace("&", "&&"), null, (_, _) => Launch(it));
            }
            if (group.Items.Count == 0) sub.DropDownItems.Add(new WF.ToolStripMenuItem("(vide)") { Enabled = false });
            menu.Items.Add(sub);
        }

        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add("Paramètres…", null, (_, _) => { ShowAndActivate(); OpenSettings(); });
        menu.Items.Add("Quitter", null, (_, _) => QuitApp());

        var old = _tray.ContextMenuStrip;
        _tray.ContextMenuStrip = menu;
        old?.Dispose();
    }

    private void QuitApp()
    {
        _reallyClose = true;
        Close();
    }

    // =====================================================================
    //  Fenêtre
    // =====================================================================

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        _config.AlwaysOnTop = !_config.AlwaysOnTop;
        Topmost = _config.AlwaysOnTop;
        UpdatePinButton();
        Save();
        ShowToast(_config.AlwaysOnTop ? "Toujours au premier plan : activé" : "Toujours au premier plan : désactivé");
    }

    private void UpdatePinButton()
    {
        PinButton.Content = _config.AlwaysOnTop ? "" : "";
        PinButton.Foreground = _config.AlwaysOnTop ? (Brush)FindResource("Accent") : (Brush)FindResource("TextPrimary");
        PinButton.ToolTip = _config.AlwaysOnTop ? "Ne plus garder au premier plan" : "Toujours au premier plan";
    }

    private void UpdateMaximizedState()
    {
        var max = WindowState == WindowState.Maximized;
        RootBorder.Margin = max ? NativeWindow.MaximizedOverflow(this) : new Thickness(0);
        MaxButton.Content = max ? "" : "";
        MaxButton.ToolTip = max ? "Restaurer" : "Agrandir";
    }

    private void RestorePlacement()
    {
        var p = _config.Window;
        if (p is null || p.Width < MinWidth || p.Height < MinHeight)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                              SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var rect = new Rect(p.Left, p.Top, p.Width, p.Height);
        if (!screen.IntersectsWith(rect) || Rect.Intersect(screen, rect).Width < 100)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Width = p.Width;
            Height = p.Height;
            return;
        }
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = p.Left;
        Top = p.Top;
        Width = p.Width;
        Height = p.Height;
        if (p.Maximized) WindowState = WindowState.Maximized;
    }

    private void SavePlacement()
    {
        var r = RestoreBounds;
        if (r.IsEmpty || r.Width <= 0) r = new Rect(Left, Top, Width, Height);
        _config.Window = new WindowPlacement
        {
            Left = r.Left,
            Top = r.Top,
            Width = r.Width,
            Height = r.Height,
            Maximized = WindowState == WindowState.Maximized,
        };
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_reallyClose && _config.CloseToTray && _config.ShowTrayIcon)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        if (IsLoaded) SavePlacement();
        try { ConfigService.Save(_config); } catch { /* on quitte quand même */ }
        _hotkey.Dispose();
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }
        Application.Current.Shutdown();
    }
}
