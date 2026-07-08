using Avalonia;
using Avalonia.iOS;
using Foundation;
using Heroes3MapReader.UI;

namespace Heroes3MapReader.iOS;

[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
