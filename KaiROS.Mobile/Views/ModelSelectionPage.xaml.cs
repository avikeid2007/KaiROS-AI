using KaiROS.Mobile.ViewModels;

namespace KaiROS.Mobile.Views;

public partial class ModelSelectionPage : ContentPage
{
    private readonly ModelSelectionViewModel _viewModel;

    public ModelSelectionPage(ModelSelectionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _viewModel.LoadModelsCommand.ExecuteAsync(null);
    }
}
