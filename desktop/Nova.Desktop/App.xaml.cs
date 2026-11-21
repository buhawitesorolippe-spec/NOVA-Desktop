using System.Windows;

namespace Nova.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        EnvironmentConfiguration.Load();
        base.OnStartup(e);
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
