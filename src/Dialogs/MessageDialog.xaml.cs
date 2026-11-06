using System.Windows;
using System.Windows.Input;

namespace Lanceur;

public partial class MessageDialog : Window
{
    public MessageDialog()
    {
        InitializeComponent();
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is not System.Windows.Controls.TextBox)
            try { DragMove(); } catch { }
    }
}

/// <summary>Boîtes de dialogue harmonisées avec le thème.</summary>
public static class Dialogs
{
    public static void Info(Window? owner, string title, string message)
    {
        var d = Create(owner, title, message);
        d.CancelButton.Visibility = Visibility.Collapsed;
        d.OkButton.Margin = new Thickness(0);
        d.ShowDialog();
    }

    public static bool Confirm(Window? owner, string title, string message, string okText = "OK",
                               string cancelText = "Annuler", bool danger = false)
    {
        var d = Create(owner, title, message);
        d.OkButton.Content = okText;
        d.CancelButton.Content = cancelText;
        if (danger) d.OkButton.Style = (Style)Application.Current.FindResource("DangerButton");
        return d.ShowDialog() == true;
    }

    public static string? Input(Window? owner, string title, string message, string initial)
    {
        var d = Create(owner, title, message);
        d.InputBox.Visibility = Visibility.Visible;
        d.InputBox.Text = initial;
        d.OkButton.Content = "Valider";
        d.Loaded += (_, _) => { d.InputBox.Focus(); d.InputBox.SelectAll(); };
        return d.ShowDialog() == true ? d.InputBox.Text : null;
    }

    private static MessageDialog Create(Window? owner, string title, string message)
    {
        var d = new MessageDialog();
        if (owner is { IsVisible: true }) d.Owner = owner;
        else d.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        d.TitleText.Text = title;
        d.MessageText.Text = message;
        d.MessageText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
        return d;
    }
}
