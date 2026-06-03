using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Heroes3MapReader.UI.ViewModels;

namespace Heroes3MapReader.UI.Views;

public partial class MainView : UserControl
{
    private bool _dataContextInitialized;

    public MainView()
    {
        InitializeComponent();

        var dataGrid = this.FindControl<DataGrid>("MapsDataGrid");
        if (dataGrid != null)
        {
            dataGrid.SelectionChanged += (s, e) =>
            {
                if (DataContext is MainWindowViewModel viewModel)
                {
                    viewModel.SetSelectedMaps(dataGrid.SelectedItems.OfType<MapItemViewModel>());
                }

                if (dataGrid.SelectedItem != null)
                {
                    dataGrid.ScrollIntoView(dataGrid.SelectedItem, null);
                }
            };
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (!_dataContextInitialized && DataContext == null && Application.Current is App app)
        {
            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            IStorageProvider? storageProvider = topLevel?.StorageProvider;
            if (storageProvider != null)
            {
                DataContext = app.CreateMainWindowViewModel(storageProvider);
                _dataContextInitialized = true;
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
    }
}
