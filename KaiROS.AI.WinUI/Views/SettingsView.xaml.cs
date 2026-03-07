using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using KaiROS.AI.WinUI.ViewModels;

namespace KaiROS.AI.WinUI.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }
    
    private void OpenApiUrl_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm && vm.IsApiEnabled)
        {
            var url = $"http://localhost:{vm.ApiPort}/";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }
    
    private void FeedbackHub_Click(object sender, RoutedEventArgs e)
    {
        var feedbackUri = "feedback-hub:?appid=34488AvnishKumar.KaiROSAI_gph07xvrc9pap";
        try
        {
            Process.Start(new ProcessStartInfo(feedbackUri) { UseShellExecute = true });
        }
        catch
        {
            Process.Start(new ProcessStartInfo("mailto:support@kairosai.app?subject=KaiROS AI Feedback") { UseShellExecute = true });
        }
    }
}


