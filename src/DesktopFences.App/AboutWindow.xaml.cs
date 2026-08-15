using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace DesktopFences.App;

public partial class AboutWindow : Window
{
    private static AboutWindow? _instance;

    public AboutWindow()
    {
        InitializeComponent();
        TxtVersion.Text = AppInfo.VersionLabel;
        TxtDescription.Text = AppInfo.Description;
        TxtYear.Text = $"© {AppInfo.Year}";
    }

    public static void ShowOrActivate()
    {
        if (_instance is null)
        {
            _instance = new AboutWindow();
            _instance.Closed += (_, _) => _instance = null;
            _instance.Show();
            return;
        }

        _instance.Show();
        _instance.Activate();
        _instance.WindowState = WindowState.Normal;
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }

    private void Profile_Click(object sender, RoutedEventArgs e) => OpenUrl(AppInfo.ProfileUrl);

    private void Repo_Click(object sender, RoutedEventArgs e) => OpenUrl(AppInfo.RepoUrl);

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
