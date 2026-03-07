using CommunityToolkit.Mvvm.ComponentModel;

namespace KaiROS.AI.WinUI.ViewModels;

/// <summary>
/// Base class for all ViewModels
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isLoading;
    
    [ObservableProperty]
    private string? _errorMessage;
    
    public virtual Task InitializeAsync() => Task.CompletedTask;
}
