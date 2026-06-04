using System;
using System.IO;
using System.Threading.Tasks;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Avalonia;
using Avalonia.Android;
using Heroes3MapReader.UI;

namespace Heroes3MapReader.Android.Platforms.Android;

[Activity(
    Label = "Heroes 3 Map Reader",
    Theme = "@style/MyTheme.NoActionBar",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.UiMode
        | ConfigChanges.KeyboardHidden)]
public class MainActivity : AvaloniaMainActivity<App>
{
    private const string LogTag = "Heroes3MapReader";
    private const string StartupErrorFileName = "startup-error.log";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        RegisterStartupDiagnostics();
        base.OnCreate(savedInstanceState);
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .With(new AndroidPlatformOptions
            {
                RenderingMode = new[] { AndroidRenderingMode.Software },
            })
            .LogToTrace();
    }

    private void RegisterStartupDiagnostics()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                LogStartupException(exception);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            LogStartupException(eventArgs.Exception);
        };
    }

    private void LogStartupException(Exception exception)
    {
        try
        {
            Log.Error(LogTag, exception.ToString());

            string? filesPath = FilesDir?.AbsolutePath;
            if (string.IsNullOrWhiteSpace(filesPath))
            {
                return;
            }

            string logPath = Path.Combine(filesPath, StartupErrorFileName);
            File.WriteAllText(logPath, exception.ToString());
        }
        catch
        {
            // Avoid throwing while handling an unhandled exception.
        }
    }
}
