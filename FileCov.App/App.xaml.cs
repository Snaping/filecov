using System.Windows;
using FileCov.App.Services;
using FileCov.App.ViewModels;

namespace FileCov.App;

public partial class App : Application
{
    public static PluginLoader? PluginLoaderInstance { get; private set; }
    public static ConversionEngine? ConversionEngineInstance { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        PluginLoaderInstance = new PluginLoader();
        PluginLoaderInstance.LoadPlugins();

        ConversionEngineInstance = new ConversionEngine(PluginLoaderInstance);

        var notificationService = new NotificationService();

        var mainWindow = new MainWindow
        {
            DataContext = new MainViewModel(PluginLoaderInstance, ConversionEngineInstance, notificationService)
        };
        mainWindow.Show();
    }
}
