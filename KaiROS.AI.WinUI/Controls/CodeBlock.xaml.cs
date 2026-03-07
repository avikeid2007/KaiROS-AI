using System.Text.RegularExpressions;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace KaiROS.AI.WinUI.Controls;

public partial class CodeBlock : UserControl
{
    private string _code = string.Empty;
    private string _language = "code";
    
    public CodeBlock()
    {
        InitializeComponent();
    }
    
    public string Code
    {
        get => _code;
        set
        {
            _code = value;
            ApplySyntaxHighlighting();
        }
    }
    
    public string CodeLanguage
    {
        get => _language;
        set
        {
            _language = value;
            LanguageLabel.Text = value.ToLower();
        }
    }
    
    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // WinUI 3: Windows.ApplicationModel.DataTransfer.Clipboard
            var pkg = new DataPackage();
            pkg.SetText(_code);
            Clipboard.SetContent(pkg);

            CopyText.Text = "Copied!";
            CopyIcon.Text = "✓";
            
            // Reset after 2 seconds using Microsoft.UI.Xaml.DispatcherTimer
            var timer = new Microsoft.UI.Xaml.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            timer.Tick += (s, args) =>
            {
                CopyText.Text = "Copy";
                CopyIcon.Text = "📋";
                timer.Stop();
            };
            timer.Start();
        }
        catch
        {
            // Ignore clipboard errors
        }
    }
    
    private void ApplySyntaxHighlighting()
    {
        CodeContent.Inlines.Clear();
        
        if (string.IsNullOrEmpty(_code))
            return;
        
        // Apply syntax highlighting based on language
        var highlightedRuns = GetHighlightedCode(_code, _language);
        foreach (var run in highlightedRuns)
        {
            CodeContent.Inlines.Add(run);
        }
    }
    
    private static List<Run> GetHighlightedCode(string code, string language)
    {
        var runs = new List<Run>();
        
        // Color palette (Catppuccin Mocha inspired)
        // WinUI 3: Windows.UI.Color.FromArgb replaces System.Windows.Media.Color.FromRgb
        var keywordColor = new SolidColorBrush(Color.FromArgb(255, 203, 166, 247)); // Mauve - keywords
        var stringColor  = new SolidColorBrush(Color.FromArgb(255, 166, 227, 161)); // Green - strings
        var commentColor = new SolidColorBrush(Color.FromArgb(255, 108, 112, 134)); // Overlay0 - comments
        var numberColor  = new SolidColorBrush(Color.FromArgb(255, 250, 179, 135)); // Peach - numbers
        var typeColor    = new SolidColorBrush(Color.FromArgb(255, 137, 180, 250)); // Blue - types
        var functionColor= new SolidColorBrush(Color.FromArgb(255, 249, 226, 175)); // Yellow - functions
        var defaultColor = new SolidColorBrush(Color.FromArgb(255, 205, 214, 244)); // Text - default
        
        // Language-specific keywords
        var keywords = GetKeywords(language);
        var types = GetTypes(language);
        
        // Simple tokenizer
        var lines = code.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            
            // Check for comment
            if (IsComment(line, language))
            {
                runs.Add(new Run { Text = line, Foreground = commentColor });
            }
            else
            {
                var tokens = Tokenize(line);
                foreach (var token in tokens)
                {
                    if (keywords.Contains(token.ToLower()) || keywords.Contains(token))
                    {
                        runs.Add(new Run { Text = token, Foreground = keywordColor, FontWeight = FontWeights.SemiBold });
                    }
                    else if (types.Contains(token))
                    {
                        runs.Add(new Run { Text = token, Foreground = typeColor });
                    }
                    else if (IsString(token))
                    {
                        runs.Add(new Run { Text = token, Foreground = stringColor });
                    }
                    else if (IsNumber(token))
                    {
                        runs.Add(new Run { Text = token, Foreground = numberColor });
                    }
                    else if (token.EndsWith("("))
                    {
                        runs.Add(new Run { Text = token.TrimEnd('('), Foreground = functionColor });
                        runs.Add(new Run { Text = "(", Foreground = defaultColor });
                    }
                    else
                    {
                        runs.Add(new Run { Text = token, Foreground = defaultColor });
                    }
                }
            }
            
            if (i < lines.Length - 1)
            {
                runs.Add(new Run { Text = "\n", Foreground = defaultColor });
            }
        }
        
        return runs;
    }
    
    private static HashSet<string> GetKeywords(string language)
    {
        return language.ToLower() switch
        {
            "csharp" or "c#" or "cs" => new HashSet<string>
            {
                "abstract", "as", "async", "await", "base", "bool", "break", "byte", "case", "catch",
                "char", "checked", "class", "const", "continue", "decimal", "default", "delegate", "do",
                "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed",
                "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal",
                "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
                "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
                "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw",
                "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "var",
                "virtual", "void", "volatile", "while", "yield", "record", "init", "required", "get", "set"
            },
            "javascript" or "js" or "typescript" or "ts" => new HashSet<string>
            {
                "async", "await", "break", "case", "catch", "class", "const", "continue", "debugger",
                "default", "delete", "do", "else", "export", "extends", "false", "finally", "for",
                "function", "if", "import", "in", "instanceof", "let", "new", "null", "return", "static",
                "super", "switch", "this", "throw", "true", "try", "typeof", "undefined", "var", "void",
                "while", "with", "yield", "interface", "type", "enum", "implements", "public", "private"
            },
            "python" or "py" => new HashSet<string>
            {
                "and", "as", "assert", "async", "await", "break", "class", "continue", "def", "del",
                "elif", "else", "except", "False", "finally", "for", "from", "global", "if", "import",
                "in", "is", "lambda", "None", "nonlocal", "not", "or", "pass", "raise", "return",
                "True", "try", "while", "with", "yield", "self", "print"
            },
            "sql" => new HashSet<string>
            {
                "select", "from", "where", "and", "or", "not", "insert", "update", "delete", "create",
                "table", "drop", "alter", "index", "join", "left", "right", "inner", "outer", "on",
                "group", "by", "order", "having", "limit", "offset", "as", "distinct", "count", "sum",
                "avg", "max", "min", "null", "is", "in", "between", "like", "exists", "case", "when",
                "then", "else", "end", "primary", "key", "foreign", "references", "unique", "constraint"
            },
            "html" or "xml" => new HashSet<string>
            {
                "html", "head", "body", "div", "span", "p", "a", "img", "table", "tr", "td", "th",
                "form", "input", "button", "select", "option", "ul", "ol", "li", "h1", "h2", "h3",
                "h4", "h5", "h6", "header", "footer", "nav", "section", "article", "aside", "main"
            },
            "css" => new HashSet<string>
            {
                "color", "background", "margin", "padding", "border", "width", "height", "display",
                "position", "top", "right", "bottom", "left", "font", "text", "flex", "grid", "align",
                "justify", "overflow", "opacity", "transform", "transition", "animation", "z-index"
            },
            _ => new HashSet<string>()
        };
    }
    
    private static HashSet<string> GetTypes(string language)
    {
        return language.ToLower() switch
        {
            "csharp" or "c#" or "cs" => new HashSet<string>
            {
                "String", "Int32", "Int64", "Boolean", "Double", "Float", "Decimal", "DateTime",
                "List", "Dictionary", "Task", "Action", "Func", "IEnumerable", "Array", "Object",
                "StringBuilder", "Exception", "Console", "Math", "File", "Directory", "Path"
            },
            "javascript" or "js" or "typescript" or "ts" => new HashSet<string>
            {
                "Array", "Object", "String", "Number", "Boolean", "Date", "Math", "JSON", "Promise",
                "Map", "Set", "RegExp", "Error", "console", "window", "document"
            },
            _ => new HashSet<string>()
        };
    }
    
    private static bool IsComment(string line, string language)
    {
        var trimmed = line.TrimStart();
        return language.ToLower() switch
        {
            "csharp" or "c#" or "cs" or "javascript" or "js" or "typescript" or "ts" 
                => trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*"),
            "python" or "py" => trimmed.StartsWith("#"),
            "sql" => trimmed.StartsWith("--"),
            "html" or "xml" => trimmed.StartsWith("<!--"),
            _ => trimmed.StartsWith("//") || trimmed.StartsWith("#")
        };
    }
    
    private static bool IsString(string token)
    {
        return (token.StartsWith("\"") && token.EndsWith("\"")) ||
               (token.StartsWith("'") && token.EndsWith("'")) ||
               (token.StartsWith("`") && token.EndsWith("`"));
    }
    
    private static bool IsNumber(string token)
    {
        return double.TryParse(token, out _) || 
               (token.StartsWith("0x") && token.Length > 2);
    }
    
    private static List<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        var pattern = @"(""[^""]*""|'[^']*'|`[^`]*`|//.*|#.*|[a-zA-Z_][a-zA-Z0-9_]*\(|[a-zA-Z_][a-zA-Z0-9_]*|[0-9]+\.?[0-9]*|\s+|.)";
        var matches = Regex.Matches(line, pattern);
        foreach (Match match in matches)
        {
            tokens.Add(match.Value);
        }
        return tokens;
    }
}
