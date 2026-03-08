using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KaiROS.AI.WinUI.Views;

public partial class DocumentView : UserControl
{
    public DocumentView()
    {
        InitializeComponent();
    }

    private void StartService_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag != null &&
            DataContext is ViewModels.DocumentViewModel vm)
            vm.StartServiceCommand.Execute(btn.Tag);
    }

    private void StopService_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag != null &&
            DataContext is ViewModels.DocumentViewModel vm)
            vm.StopServiceCommand.Execute(btn.Tag);
    }

    private void ViewApiService_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag != null &&
            DataContext is ViewModels.DocumentViewModel vm)
            vm.OpenServiceUrlCommand.Execute(btn.Tag);
    }

    private void DeleteService_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag != null &&
            DataContext is ViewModels.DocumentViewModel vm)
            vm.DeleteServiceCommand.Execute(btn.Tag);
    }

    private void AddFileSource_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.DocumentViewModel vm)
            vm.AddFileSourceCommand.Execute(vm.SelectedConfiguration);
    }

    private void AddWebSource_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.DocumentViewModel vm)
            vm.AddWebSourceCommand.Execute(vm.SelectedConfiguration);
    }

    private void RemoveSource_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag != null &&
            DataContext is ViewModels.DocumentViewModel vm)
            vm.RemoveSourceFromServiceCommand.Execute(btn.Tag);
    }
}

