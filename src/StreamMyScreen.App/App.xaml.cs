using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace StreamMyScreen.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show(args.Exception.Message, "Error");
            args.Handled = true;
        };
    }
}
