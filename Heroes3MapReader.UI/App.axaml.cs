using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Heroes3MapReader.Logic;
using Heroes3MapReader.Logic.Interfaces;
using Heroes3MapReader.Logic.MapSpecificationLogic;
using Heroes3MapReader.Logic.Repositories;
using Heroes3MapReader.UI.Factories;
using Heroes3MapReader.UI.ViewModels;
using Heroes3MapReader.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Heroes3MapReader.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            mainWindow.DataContext = CreateMainWindowViewModel(mainWindow.StorageProvider);
            desktop.MainWindow = mainWindow;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public MainWindowViewModel CreateMainWindowViewModel(IStorageProvider storageProvider)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMapReader, MapReader>();
        services.AddSingleton<IMapReaderFactory, MapReaderFactory>();
        services.AddSingleton<IMapSpecificationRepository, MapSpecificationRepository>();
        services.AddSingleton<IStreamDecompressor, StreamDecompressor>();
        services.AddSingleton(storageProvider);
        services.AddSingleton<ISpellSelectionWindowFactory, SpellSelectionWindowFactory>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<MainWindowViewModel>();

        ServiceProvider serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<MainWindowViewModel>();
    }
}
