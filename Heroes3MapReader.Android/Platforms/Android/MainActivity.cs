using Android.App;
using Android.Content.PM;
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
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .LogToTrace();
    }
}
