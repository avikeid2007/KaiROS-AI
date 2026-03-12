using KaiROS.AI.Uno.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Diagnostics;

namespace KaiROS.AI.Uno.Presentation;

public sealed partial class MainPage : Page
{
    public MainViewModel? ViewModel { get; private set; }

    public MainPage()
    {
        this.InitializeComponent();
        Loaded += MainPage_Loaded;
        Debug.WriteLine("MainPage constructed");
    }

    private async void MainPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Debug.WriteLine("MainPage_Loaded fired");
        try
        {
            if (DataContext is MainViewModel viewModel && ViewModel == null)
            {
                ViewModel = viewModel;
                Debug.WriteLine("Starting InitializeAsync...");
                await viewModel.InitializeAsync();
                Debug.WriteLine("InitializeAsync completed");
            }
            else
            {
                Debug.WriteLine($"DataContext type: {DataContext?.GetType().Name ?? "null"}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MainPage_Loaded error: {ex}");
            if (DataContext is MainViewModel vm)
            {
                vm.ErrorMessage = $"Initialization error: {ex.Message}";
            }
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tagStr)
        {
            if (int.TryParse(tagStr, out int tag))
            {
                if (ViewModel != null)
                {
                    ViewModel.SelectedNavigationIndex = tag;
                    UpdateContent(tag);
                }
            }
        }
    }

    private void UpdateContent(int index)
    {
        ContentHost.ContentTemplate = index switch
        {
            0 => Resources["CatalogTemplate"] as Microsoft.UI.Xaml.DataTemplate,
            1 => Resources["ChatTemplate"] as Microsoft.UI.Xaml.DataTemplate,
            2 => Resources["DocumentTemplate"] as Microsoft.UI.Xaml.DataTemplate,
            3 => Resources["SettingsTemplate"] as Microsoft.UI.Xaml.DataTemplate,
            _ => Resources["CatalogTemplate"] as Microsoft.UI.Xaml.DataTemplate
        };
    }
}
