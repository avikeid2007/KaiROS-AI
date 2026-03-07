param([string]$filePath)

$w1252 = [System.Text.Encoding]::GetEncoding(1252)
$utf8   = [System.Text.Encoding]::UTF8

function B([byte[]]$bytes) { return $w1252.GetString($bytes) }

$content = [System.IO.File]::ReadAllText($filePath, $utf8)
$original = $content

# ---- ChatView.xaml replacements ----

# 🤖 U+1F916  = F0 9F A4 96
$robot = B(@(0xF0,0x9F,0xA4,0x96))
$content = $content.Replace(
    "<TextBlock Text=""$robot Assistant""",
    '<StackPanel Orientation="Horizontal" VerticalAlignment="Center"><FontIcon FontFamily="Segoe MDL2 Assets" Glyph="&#xE9CE;" FontSize="13" Foreground="{StaticResource PrimaryLightBrush}" Margin="0,0,6,0" VerticalAlignment="Center"/><TextBlock Text="Assistant" Foreground="{StaticResource PrimaryLightBrush}" FontSize="12" FontWeight="SemiBold" VerticalAlignment="Center"/></StackPanel>'
)

# ●●● streaming U+25CF x3 = E2 97 8F x3
$dot3 = B(@(0xE2,0x97,0x8F)) + B(@(0xE2,0x97,0x8F)) + B(@(0xE2,0x97,0x8F))
$streamOld = @"
                                        <TextBlock Text="$dot3" 
                                                   Foreground="{StaticResource PrimaryBrush}"
                                                   FontSize="14"
                                                   Margin="0,4,0,0"
                                                   Visibility="{Binding IsStreaming, Converter={StaticResource BoolToVis}}"/>
"@
$streamNew = @"
                                        <!-- Streaming Indicator -->
                                        <ProgressRing Width="16" Height="16"
                                                      IsActive="True"
                                                      Foreground="{StaticResource PrimaryBrush}"
                                                      Margin="0,4,0,0"
                                                      Visibility="{Binding IsStreaming, Converter={StaticResource BoolToVis}}"/>
"@
$content = $content.Replace($streamOld, $streamNew)

# 🧠 U+1F9E0 = F0 9F A7 A0
$brain = B(@(0xF0,0x9F,0xA7,0xA0))
$content = $content.Replace(
    "<TextBlock Text=""$brain  Knowledge Base""",
    '<StackPanel Orientation="Horizontal" VerticalAlignment="Center"><FontIcon FontFamily="Segoe MDL2 Assets" Glyph="&#xE8A5;" FontSize="12" Foreground="{StaticResource TextSecondaryBrush}" Margin="0,0,4,0" VerticalAlignment="Center"/><TextBlock Text="Knowledge Base" Foreground="{StaticResource TextSecondaryBrush}" FontSize="12" VerticalAlignment="Center"/></StackPanel>'
)

# 🌐 U+1F310 = F0 9F 8C 90
$globe = B(@(0xF0,0x9F,0x8C,0x90))
$content = $content.Replace(
    "<TextBlock Text=""$globe Web Search""",
    '<StackPanel Orientation="Horizontal" VerticalAlignment="Center"><FontIcon FontFamily="Segoe MDL2 Assets" Glyph="&#xE909;" FontSize="12" Foreground="{StaticResource TextSecondaryBrush}" Margin="0,0,4,0" VerticalAlignment="Center"/><TextBlock Text="Web Search" Foreground="{StaticResource TextSecondaryBrush}" FontSize="12" VerticalAlignment="Center"/></StackPanel>'
)

# ⎋ U+238B = E2 8E 8B
$enter = B(@(0xE2,0x8E,0x8B))
$content = $content.Replace(
    "<TextBlock Text=""$enter Enter to Send""",
    '<StackPanel Orientation="Horizontal" VerticalAlignment="Center"><FontIcon FontFamily="Segoe MDL2 Assets" Glyph="&#xE751;" FontSize="12" Foreground="{StaticResource TextSecondaryBrush}" Margin="0,0,4,0" VerticalAlignment="Center"/><TextBlock Text="Enter to Send" Foreground="{StaticResource TextSecondaryBrush}" FontSize="12" VerticalAlignment="Center"/></StackPanel>'
)

# 📄 U+1F4C4 = F0 9F 93 84 (document in doc indicator)
$doc = B(@(0xF0,0x9F,0x93,0x84))
$content = $content.Replace(
    "<TextBlock Text=""$doc"" Margin=""0,0,6,0""/>",
    '<FontIcon FontFamily="Segoe MDL2 Assets" Glyph="&#xE8A5;" FontSize="14" Margin="0,0,6,0" VerticalAlignment="Center"/>'
)

# ✕ U+2715 = E2 9C 95  (all occurrences of remove buttons)
$xmark = B(@(0xE2,0x9C,0x95))
$content = $content.Replace(
    "<Button Content=""$xmark""",
    '<Button'
).Replace(
    "<Button Content=""$xmark""",
    '<Button'
)
# More precise: replace the remove document ✕ button
$content = $content -replace (
    [regex]::Escape("<Button Content=""$xmark""") + '(\s+)(Command="\{Binding RemoveDocumentCommand\}")',
    '<Button $2'
)

# Simpler approach for the ✕ buttons - replace content attribute with FontIcon child
$content = $content.Replace(
    "Content=""$xmark""" + "`r`n                                     Command=""{Binding RemoveDocumentCommand}""",
    "Command=""{Binding RemoveDocumentCommand}"""
).Replace(
    "Content=""$xmark""" + "`r`n                                     Command=""{Binding RemoveAttachedImageCommand}""",
    "Command=""{Binding RemoveAttachedImageCommand}"""
)

# 🖼 U+1F5BC = F0 9F 96 BC
$picture = B(@(0xF0,0x9F,0x96,0xBC))
$content = $content.Replace(
    "<TextBlock Text=""$picture"" Margin=""0,0,6,0""/>",
    '<FontIcon FontFamily="Segoe MDL2 Assets" Glyph="&#xEB9F;" FontSize="14" Margin="0,0,6,0" VerticalAlignment="Center"/>'
)

# Image button content 
$content = $content.Replace(
    "Content=""$picture""",
    'ToolTipService2="img_fix"'
)

# 📄 document upload button content
$content = $content.Replace(
    "Content=""$doc""" + "`r`n                            ToolTipService.ToolTip=""Attach text document""",
    'ToolTipService.ToolTip="Attach text document"'
)

# ➤ U+27A4 = E2 9E A4
$arr = B(@(0xE2,0x9E,0xA4))
$content = $content.Replace(
    "Content=""Send $arr""/>",
    '><StackPanel Orientation="Horizontal"><TextBlock Text="Send" VerticalAlignment="Center"/><FontIcon FontFamily="Segoe MDL2 Assets" Glyph="&#xE724;" FontSize="14" Margin="6,0,0,0" VerticalAlignment="Center"/></StackPanel></Button'
)

# ⏹ U+23F9 = E2 8F B9
$stop = B(@(0xE2,0x8F,0xB9))
$content = $content.Replace(
    "Content=""$stop Stop""/>",
    '><StackPanel Orientation="Horizontal"><FontIcon FontFamily="Segoe MDL2 Assets" Glyph="&#xE71A;" FontSize="14" VerticalAlignment="Center"/><TextBlock Text=" Stop" VerticalAlignment="Center"/></StackPanel></Button'
)

# ⚡ U+26A1 = E2 9A A1
$flash = B(@(0xE2,0x9A,0xA1))
$content = $content.Replace("<Run Text=""$flash""/>", '<Run FontFamily="Segoe MDL2 Assets" Text="&#xE945;"/>')

# 📊 U+1F4CA = F0 9F 93 8A
$chart = B(@(0xF0,0x9F,0x93,0x8A))
$content = $content.Replace("<Run Text=""$chart""/>", '<Run FontFamily="Segoe MDL2 Assets" Text="&#xE9F3;"/>')

# 💾 U+1F4BE = F0 9F 92 BE
$save = B(@(0xF0,0x9F,0x92,0xBE))
$content = $content.Replace("<Run Text=""$save""/>", '<Run FontFamily="Segoe MDL2 Assets" Text="&#xE950;"/>')

# ⏱ U+23F1 = E2 8F B1
$timer = B(@(0xE2,0x8F,0xB1))
$content = $content.Replace("<Run Text=""$timer""/>", '<Run FontFamily="Segoe MDL2 Assets" Text="&#xE916;"/>')

# 📌 U+1F4CC = F0 9F 93 8C (context window icon)
$pin = B(@(0xF0,0x9F,0x93,0x8C))
$content = $content.Replace("<Run Text=""$pin""/>", '<Run FontFamily="Segoe MDL2 Assets" Text="&#xE97D;"/>')

# 🎮 U+1F3AE = F0 9F 8E AE
$game = B(@(0xF0,0x9F,0x8E,0xAE))
$content = $content.Replace("<Run Text=""$game""/>", '<Run FontFamily="Segoe MDL2 Assets" Text="&#xECE4;"/>')

$changed = ($content -ne $original)
Write-Host "Content changed: $changed"

if ($changed) {
    [System.IO.File]::WriteAllText($filePath, $content, $utf8)
    Write-Host "File written successfully"
}
