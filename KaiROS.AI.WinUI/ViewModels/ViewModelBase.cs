using CommunityToolkit.Mvvm.ComponentModel;

namespace KaiROS.AI.WinUI.ViewModels;

/// <summary>
/// Base class for all ViewModels
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public virtual Task InitializeAsync() => Task.CompletedTask;
}
