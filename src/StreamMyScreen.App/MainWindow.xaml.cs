using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using StreamMyScreen.App.Services;

namespace StreamMyScreen.App;

public partial class MainWindow : Window
{
    private readonly WebServerService _webServer = new();
    private readonly AirPlayService _airPlay = new("StreamMyScreen - PC");

    public MainWindow()
    {
        InitializeComponent();
        SetWindowIcon();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void SetWindowIcon()
    {
        try
        {
            var iconFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.png");
            if (File.Exists(iconFile))
            {
                Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconFile));
            }
        }
        catch { }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _webServer.StartAsync();
            _airPlay.Start();

            var envOptions = new CoreWebView2EnvironmentOptions("--ignore-certificate-errors");
            var userDataFolder = Path.Combine(Path.GetTempPath(), "StreamMyScreen_WebView2");
            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, envOptions);

            await MainWebView.EnsureCoreWebView2Async(env);
            MainWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            MainWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

            var targetUrl = $"https://127.0.0.1:{_webServer.HttpsPort}/receiver/index.html";
            MainWebView.Source = new Uri(targetUrl);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Application startup failed: {ex.Message}", "Stream My Screen", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _airPlay.Stop();
        await _webServer.StopAsync();
    }
}
