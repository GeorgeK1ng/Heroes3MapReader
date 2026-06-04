using Avalonia;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Heroes3MapReader.UI;

internal class Program
{
    private const string StartupErrorFileName = "startup-error.log";
    private const uint MessageBoxIconError = 0x00000010;

    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            {
                if (eventArgs.ExceptionObject is Exception exception)
                {
                    LogStartupException(exception);
                }
            };

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception)
        {
            string? logPath = LogStartupException(exception);
            ShowStartupError(exception, logPath);
            throw;
        }
    }

    private static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

#if NET6_0_WINDOWS
        // Windows 7 machines frequently lack modern GPU/DirectX components used by the default backend.
        // Force software rendering for the Windows 7-compatible build so startup does not fail silently
        // before the first window is shown.
        builder.With(new Win32PlatformOptions
        {
            RenderingMode = new[] { Win32RenderingMode.Software },
        });
#endif

#if DEBUG
        builder.AfterSetup(_ =>
        {
            if (builder.Instance is App app)
            {
                app.AttachDevTools();
            }
        });
#endif

        return builder;
    }

    private static string? LogStartupException(Exception exception)
    {
        try
        {
            string logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Heroes3MapReader");
            Directory.CreateDirectory(logDirectory);

            string logPath = Path.Combine(logDirectory, StartupErrorFileName);
            File.WriteAllText(logPath, exception.ToString());
            return logPath;
        }
        catch
        {
            return null;
        }
    }

    private static void ShowStartupError(Exception exception, string? logPath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        string message = "Heroes3MapReader failed to start.";
        if (!string.IsNullOrWhiteSpace(logPath))
        {
            message += $"\n\nDetails were saved to:\n{logPath}";
        }

        message += $"\n\n{exception.Message}";
        MessageBox(IntPtr.Zero, message, "Heroes3MapReader", MessageBoxIconError);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
