using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using KaiROS.AI.WinUI.Controls;
using KaiROS.AI.WinUI.Services;
using WinUIBrush = Microsoft.UI.Xaml.Media.Brush;

namespace KaiROS.AI.WinUI.Converters;

/// <summary>
/// Converts message content to a list of UI elements with markdown formatting.
/// WinUI 3 version: uses Microsoft.UI.Xaml types throughout.
/// </summary>
public class MarkdownContentConverter : IValueConverter
{
    private static readonly Regex HeaderPattern     = new(@"^(#{1,6})\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex BlockquotePattern = new(@"^>\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex LinkPattern       = new(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);
    private static readonly Regex BoldPattern       = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
    private static readonly Regex InlineCodePattern = new(@"`([^`]+)`", RegexOptions.Compiled);
    private static readonly Regex ListItemPattern   = new(@"^[\s]*[-*•]\s+(.+)$", RegexOptions.Compiled);

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string content || string.IsNullOrEmpty(content))
            return new StackPanel();

        var panel = new StackPanel();
        var segments = MarkdownParser.Parse(content);

        foreach (var segment in segments)
        {
            if (segment.Type == SegmentType.CodeBlock)
            {
                var codeBlock = new CodeBlock
                {
                    Code = segment.Content,
                    CodeLanguage = segment.Language,
                    Margin = new Thickness(0, 8, 0, 8)
                };
                panel.Children.Add(codeBlock);
            }
            else
            {
                var lines = segment.Content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        if (panel.Children.Count > 0)
                            panel.Children.Add(new TextBlock { Height = 8 });
                        continue;
                    }

                    var headerMatch = HeaderPattern.Match(line);
                    if (headerMatch.Success)
                    {
                        panel.Children.Add(CreateHeader(headerMatch.Groups[2].Value, headerMatch.Groups[1].Length));
                        continue;
                    }

                    var quoteMatch = BlockquotePattern.Match(line);
                    if (quoteMatch.Success)
                    {
                        panel.Children.Add(CreateBlockquote(quoteMatch.Groups[1].Value));
                        continue;
                    }

                    var listMatch = ListItemPattern.Match(line);
                    if (listMatch.Success)
                    {
                        panel.Children.Add(CreateListItem(listMatch.Groups[1].Value));
                        continue;
                    }

                    panel.Children.Add(CreateFormattedTextBlock(line));
                }
            }
        }

        return panel;
    }

    private static WinUIBrush GetResource(string key) =>
        (WinUIBrush)Microsoft.UI.Xaml.Application.Current.Resources[key];

    private UIElement CreateHeader(string text, int level)
    {
        double fontSize = level switch { 1 => 24, 2 => 20, 3 => 18, _ => 16 };
        return new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = FontWeights.Bold,
            Foreground = GetResource("TextPrimaryBrush"),
            Margin = new Thickness(0, 12, 0, 4),
            TextWrapping = TextWrapping.Wrap
        };
    }

    private UIElement CreateBlockquote(string text)
    {
        var border = new Border
        {
            BorderBrush = GetResource("PrimaryBrush"),
            BorderThickness = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(12, 4, 0, 4),
            Margin = new Thickness(0, 4, 0, 4),
            Background = GetResource("SurfaceLightBrush")
        };

        var content = CreateFormattedTextBlock(text);
        content.FontStyle = Windows.UI.Text.FontStyle.Italic;
        border.Child = content;
        return border;
    }

    private UIElement CreateListItem(string text)
    {
        var itemPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(8, 2, 0, 2)
        };
        itemPanel.Children.Add(new TextBlock
        {
            Text = "•  ",
            Foreground = GetResource("AccentBrush"),
            FontWeight = FontWeights.Bold
        });
        itemPanel.Children.Add(CreateFormattedTextBlock(text));
        return itemPanel;
    }

    private TextBlock CreateFormattedTextBlock(string text)
    {
        var textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 2),
            LineHeight = 22
        };

        var matches = new List<(int Index, int Length, string Text, string Type, string? Url)>();
        foreach (Match m in BoldPattern.Matches(text))
            matches.Add((m.Index, m.Length, m.Groups[1].Value, "bold", null));
        foreach (Match m in InlineCodePattern.Matches(text))
            matches.Add((m.Index, m.Length, m.Groups[1].Value, "code", null));
        foreach (Match m in LinkPattern.Matches(text))
            matches.Add((m.Index, m.Length, m.Groups[1].Value, "link", m.Groups[2].Value));
        matches = matches.OrderBy(m => m.Index).ToList();

        int currentIndex = 0;
        if (matches.Count == 0)
        {
            textBlock.Inlines.Add(new Run { Text = text, Foreground = GetResource("TextPrimaryBrush") });
        }
        else
        {
            foreach (var match in matches)
            {
                if (match.Index > currentIndex)
                    textBlock.Inlines.Add(new Run
                    {
                        Text = text.Substring(currentIndex, match.Index - currentIndex),
                        Foreground = GetResource("TextPrimaryBrush")
                    });

                if (match.Type == "bold")
                {
                    textBlock.Inlines.Add(new Run
                    {
                        Text = match.Text,
                        FontWeight = FontWeights.Bold,
                        Foreground = GetResource("TextPrimaryBrush")
                    });
                }
                else if (match.Type == "code")
                {
                    textBlock.Inlines.Add(new Run
                    {
                        Text = match.Text,
                        FontFamily = new FontFamily("Consolas"),
                        Foreground = GetResource("AccentBrush")
                    });
                }
                else if (match.Type == "link")
                {
                    // WinUI 3: Hyperlink uses Click event (not RequestNavigate)
                    var link = new Hyperlink { Foreground = GetResource("PrimaryLightBrush") };
                    link.Inlines.Add(new Run { Text = match.Text });
                    var uri = match.Url;
                    link.Click += (s, e) =>
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(
                                new System.Diagnostics.ProcessStartInfo(uri ?? "") { UseShellExecute = true });
                        }
                        catch { }
                    };
                    textBlock.Inlines.Add(link);
                }

                currentIndex = match.Index + match.Length;
            }

            if (currentIndex < text.Length)
                textBlock.Inlines.Add(new Run
                {
                    Text = text.Substring(currentIndex),
                    Foreground = GetResource("TextPrimaryBrush")
                });
        }

        return textBlock;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
