using KaiROS.AI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KaiROS.AI.Helpers;

/// <summary>
/// Maps ViewModel types to their corresponding View DataTemplates.
/// Replaces the WPF {x:Type}-based implicit DataTemplate routing that is
/// not supported in WinUI 3.
/// </summary>
public class ViewModelTemplateSelector : DataTemplateSelector
{
    public DataTemplate? CatalogTemplate { get; set; }
    public DataTemplate? ChatTemplate { get; set; }
    public DataTemplate? DocumentTemplate { get; set; }
    public DataTemplate? SettingsTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        return item switch
        {
            ModelCatalogViewModel => CatalogTemplate,
            ChatViewModel => ChatTemplate,
            DocumentViewModel => DocumentTemplate,
            SettingsViewModel => SettingsTemplate,
            _ => null
        };
    }
}
