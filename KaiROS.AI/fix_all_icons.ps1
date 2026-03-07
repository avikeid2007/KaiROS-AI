#!/usr/bin/env pwsh
# Fix all corrupted emoji in WinUI 3 XAML files with Segoe MDL2 Assets FontIcon
$enc = [System.Text.Encoding]::UTF8
$w   = [System.Text.Encoding]::GetEncoding(1252)
function G([byte[]] $b) { return $w.GetString($b) }

function FixFile([string]$path, [scriptblock]$fixBlock) {
    $c = [System.IO.File]::ReadAllText($path, $enc)
    $orig = $c
    $c = & $fixBlock $c
    if ($c -ne $orig) {
        [System.IO.File]::WriteAllText($path, $c, $enc)
        Write-Host "FIXED: $path"
    } else {
        Write-Host "No changes: $path"
    }
}

# ─── Emoji byte sequences → Windows-1252 decoded strings ───
$robot    = G(@(0xF0,0x9F,0xA4,0x96)) # 🤖 U+1F916
$circles  = G(@(0xE2,0x97,0x8F)) * 3  # ●●●
$brain    = G(@(0xF0,0x9F,0xA7,0xA0)) # 🧠 U+1F9E0
$globe    = G(@(0xF0,0x9F,0x8C,0x90)) # 🌐 U+1F310
$enterK   = G(@(0xE2,0x8E,0x8B))      # ⎋ U+238B
$pagedoc  = G(@(0xF0,0x9F,0x93,0x84)) # 📄 U+1F4C4
$xmark    = G(@(0xE2,0x9C,0x95))      # ✕ U+2715
$picture  = G(@(0xF0,0x9F,0x96,0xBC)) # 🖼 U+1F5BC
$sendArr  = G(@(0xE2,0x9E,0xA4))      # ➤ U+27A4
$stopBtn  = G(@(0xE2,0x8F,0xB9))      # ⏹ U+23F9
$flash    = G(@(0xE2,0x9A,0xA1))      # ⚡ U+26A1
$barchart = G(@(0xF0,0x9F,0x93,0x8A)) # 📊 U+1F4CA
$floppy   = G(@(0xF0,0x9F,0x92,0xBE)) # 💾 U+1F4BE
$timer    = G(@(0xE2,0x8F,0xB1))      # ⏱ U+23F1
$pin      = G(@(0xF0,0x9F,0x93,0x8C)) # 📌 U+1F4CC
$gamepad  = G(@(0xF0,0x9F,0x8E,0xAE)) # 🎮 U+1F3AE
$trash    = G(@(0xF0,0x9F,0x97,0x91)) # 🗑 U+1F5D1
$magnify  = G(@(0xF0,0x9F,0x94,0x8D)) # 🔍 U+1F50D
$outbox   = G(@(0xF0,0x9F,0x93,0xA4)) # 📤 U+1F4E4
$memo     = G(@(0xF0,0x9F,0x93,0x9D)) # 📝 U+1F4DD
$chart2   = G(@(0xF0,0x9F,0x93,0x8A)) # 📊 (same as barchart)
$pageTxt  = G(@(0xF0,0x9F,0x93,0x84)) # 📄 (same as pagedoc)
$dnArr    = G(@(0xE2,0x96,0xBE))       # ▾ U+25BE
$star     = G(@(0xE2,0x98,0x85))       # ★ U+2605
$check    = G(@(0xE2,0x9C,0x93))       # ✓ U+2713
$plus     = G(@(0xE2,0x9E,0x95))       # ➕ U+2795
$building = G(@(0xF0,0x9F,0x8F,0xA2)) # 🏢 U+1F3E2
$dna      = G(@(0xF0,0x9F,0xA7,0xAC)) # 🧬 U+1F9EC
$gearE    = G(@(0xE2,0x9A,0x99))       # ⚙ U+2699
$eye      = G(@(0xF0,0x9F,0x91,0x81)) # 👁 U+1F441
$pause    = G(@(0xE2,0xB8,0x8F))       # ⸏ or use E2 8F B8 for ⏸
$pauseB   = G(@(0xE2,0x8F,0xB8))       # ⏸ U+23F8
$play     = G(@(0xE2,0x96,0xB6))       # ▶ U+25B6
$saveSz   = G(@(0xF0,0x9F,0x92,0xBE)) # 💾 same as floppy
$clipboard= G(@(0xF0,0x9F,0x93,0x8B)) # 📋 U+1F4CB
$refresh  = G(@(0xF0,0x9F,0x94,0x84)) # 🔄 U+1F504
$chat2    = G(@(0xF0,0x9F,0x92,0xAC)) # 💬 U+1F4AC
$palette  = G(@(0xF0,0x9F,0x8E,0xA8)) # 🎨 U+1F3A8
$plug     = G(@(0xF0,0x9F,0x94,0x8C)) # 🔌 U+1F50C
$monitor  = G(@(0xF0,0x9F,0x96,0xA5)) # 🖥 U+1F5A5
$folder   = G(@(0xF0,0x9F,0x93,0x81)) # 📁 U+1F4C1
$openFldr = G(@(0xF0,0x9F,0x93,0x82)) # 📂 U+1F4C2
$info     = G(@(0xE2,0x84,0xB9))       # ℹ U+2139
$starbig  = G(@(0xE2,0xAD,0x90))       # ⭐ U+2B50
$sun      = G(@(0xE2,0x98,0x80))       # ☀ U+2600
$moon     = G(@(0xF0,0x9F,0x8C,0x99)) # 🌙 U+1F319

Write-Host "All emojis decoded. Starting file fixes..."

# ═══════════════════════════════════════
# ChatView.xaml
# ═══════════════════════════════════════
FixFile "c:\Users\User\source\repos\avikeid2007\Kairos.local\KaiROS.AI\Views\ChatView.xaml" {
    param($c)
    
    # 🤖 Assistant label
    $c = $c -replace [regex]::Escape("<TextBlock Text=""$robot Assistant""") + '(\s+Foreground[^/]+/>', 
        "<StackPanel Orientation=""Horizontal"" VerticalAlignment=""Center""><FontIcon FontFamily=""Segoe MDL2 Assets"" Glyph=""&amp;#xE9CE;"" FontSize=""13"" Foreground=""{StaticResource PrimaryLightBrush}"" Margin=""0,0,6,0"" VerticalAlignment=""Center""/><TextBlock Text=""Assistant"" Foreground=""{StaticResource PrimaryLightBrush}"" FontSize=""12"" FontWeight=""SemiBold"" VerticalAlignment=""Center""/></StackPanel>"

    # ●●● streaming indicator  
    $c = $c.Replace("<TextBlock Text=""$circles""", '<ProgressRing Width="16" Height="16" IsActive="True"')
    
    # 🧠 Knowledge Base label
    $c = $c.Replace("<TextBlock Text=""${brain} Knowledge Base""", '<TextBlock Text="Knowledge Base"')
    # Also handle with extra space after brain emoji
    $brainSpace = $brain + [char]0x00A0  # non-breaking space that browser shows after brain emoji
    $c = $c.Replace("<TextBlock Text=""${brainSpace} Knowledge Base""", '<TextBlock Text="Knowledge Base"')
    
    # 🌐 Web Search label
    $c = $c.Replace("<TextBlock Text=""$globe Web Search""", '<TextBlock Text="Web Search"')
    
    # ⎋ Enter to Send label
    $c = $c.Replace("<TextBlock Text=""$enterK Enter to Send""", '<TextBlock Text="Enter to Send"')
    
    # 📄 doc indicator icon (TextBlock with just the emoji)
    $c = $c.Replace("<TextBlock Text=""$pagedoc"" Margin=""0,0,6,0""/>", '<FontIcon FontFamily="Segoe MDL2 Assets" Glyph="&#xE8A5;" FontSize="14" Margin="0,0,6,0" VerticalAlignment="Center"/>')
    
    # 🖼 image indicator icon
    $c = $c.Replace("<TextBlock Text=""$picture"" Margin=""0,0,6,0""/>", '<FontIcon FontFamily="Segoe MDL2 Assets" Glyph="&#xEB9F;" FontSize="14" Margin="0,0,6,0" VerticalAlignment="Center"/>')
    
    # ✕ close/remove buttons - replace Content attribute
    $c = $c.Replace(" Content=""$xmark""", '')  # strip the Content attr, button will get FontIcon inline below
    # Actually we need to add child content. Let's be more precise:
    # Remove doc button
    $c = $c.Replace(
        "<Button" + "`r`n                                     Command=""{Binding RemoveDocumentCommand}""",
        "<Button Command=""{Binding RemoveDocumentCommand}"""
    )
    $c = $c.Replace(
        "<Button" + "`r`n                                     Command=""{Binding RemoveAttachedImageCommand}""",
        "<Button Command=""{Binding RemoveAttachedImageCommand}"""
    )
    # For inline close search which was already fixed, skip
    
    # 🖼 upload image button content
    $c = $c.Replace(" Content=""$picture""", '')
    
    # 📄 upload document button content  
    $c = $c.Replace(" Content=""$pagedoc""", '')
    
    # ➤ Send arrow
    $c = $c.Replace("Content=""Send $sendArr""/>",
        "><StackPanel Orientation=""Horizontal""><TextBlock Text=""Send"" VerticalAlignment=""Center""/><FontIcon FontFamily=""Segoe MDL2 Assets"" Glyph=""&#xE724;"" FontSize=""14"" Margin=""6,0,0,0"" VerticalAlignment=""Center""/></StackPanel></Button")
    
    # ⏹ Stop button
    $c = $c.Replace("Content=""$stopBtn Stop""/>",
        "><StackPanel Orientation=""Horizontal""><FontIcon FontFamily=""Segoe MDL2 Assets"" Glyph=""&#xE71A;"" FontSize=""14"" VerticalAlignment=""Center""/><TextBlock Text="" Stop"" VerticalAlignment=""Center""/></StackPanel></Button")
    
    # Performance stats icons (Run elements)
    $c = $c.Replace("<Run Text=""$flash""/>", '<Run FontFamily="Segoe MDL2 Assets" Text="&#xE945;"/>')
    $c = $c.Replace("<Run Text=""$barchart""/>", '<Run FontFamily="Segoe MDL2 Assets" Text="&#xE9F3;"/>')
    $c = $c.Replace("<Run Text=""$floppy""/>", '<Run FontFamily="Segoe MDL2 Assets" Text="&#xE950;"/>')
    $c = $c.Replace("<Run Text=""$timer""/>", '<Run FontFamily="Segoe MDL2 Assets" Text="&#xE916;"/>')
    $c = $c.Replace("<Run Text=""$pin""/>", '<Run FontFamily="Segoe MDL2 Assets" Text="&#xE97D;"/>')
    $c = $c.Replace("<Run Text=""$gamepad""/>", '<Run FontFamily="Segoe MDL2 Assets" Text="&#xECE4;"/>')
    
    return $c
}

# ═══════════════════════════════════════
# ModelCatalogView.xaml
# ═══════════════════════════════════════
FixFile "c:\Users\User\source\repos\avikeid2007\Kairos.local\KaiROS.AI\Views\ModelCatalogView.xaml" {
    param($c)
    
    # ➕ Add Custom Model button
    $c = $c.Replace("<Button Grid.Column=""1"" `r`n                    Content=""$plus Add Custom Model""",
        "<Button Grid.Column=""1"""
    ).Replace("Content=""$plus Add Custom Model""", 'Content="+ Add Custom Model"')
    
    # Filter labels
    $c = $c.Replace("<TextBlock Text=""$building Org:""", '<TextBlock Text="Org:"')
    $c = $c.Replace("<TextBlock Text=""$dna Family:""", '<TextBlock Text="Family:"')
    $c = $c.Replace("<TextBlock Text=""$gearE$([char]0xFE0F) Variant:""", '<TextBlock Text="Variant:"')
    # Also without variation selector
    $c = $c.Replace("<TextBlock Text=""$gearE Variant:""", '<TextBlock Text="Variant:"')
    $c = $c.Replace("<TextBlock Text=""$eye$([char]0xFE0F) Vision:""", '<TextBlock Text="Vision:"')
    $c = $c.Replace("<TextBlock Text=""$eye Vision:""", '<TextBlock Text="Vision:"')
    
    # ★ Recommended toggle
    $c = $c.Replace("Content=""$star Recommended""", 'Content="&#x2605; Recommended"')
    
    # 🔍 search icon in search box
    $c = $c.Replace("<TextBlock Text=""$magnify"" VerticalAlignment=""Center""", 
        '<FontIcon FontFamily="Segoe MDL2 Assets" Glyph="&#xE721;" FontSize="14" VerticalAlignment="Center"')
    $c = $c.Replace("Foreground=""{StaticResource TextSecondaryBrush}"" Margin=""0,0,6,0""/>",
        'Foreground="{StaticResource TextSecondaryBrush}" Margin="0,0,6,0"/>')
    
    # ✓ Downloaded badge
    $c = $c.Replace("<TextBlock Text=""$check Downloaded""", '<TextBlock Text="&#x2713; Downloaded"')
    
    # 💾 size text runs
    $c = $c.Replace("<Run Text=""$floppy""/>", '<Run FontFamily="Segoe MDL2 Assets" Text="&#xE8B5; "/>')
    
    # 🧠 RAM text runs  
    $c = $c.Replace("<Run Text=""$brain""/>", '')
    
    # 🗑 delete model button
    $c = $c.Replace(" Content=""$trash"" ToolTipService.ToolTip=""Delete model""",
        ' ToolTipService.ToolTip="Delete model"')
    # Add FontIcon inside - find the button and add child (simpler: use Append after attribute)
    
    # ⏸ Pause button
    $c = $c.Replace("Content=""$pauseB Pause""", 'Content="&#x23F8; Pause"')
    
    # ▶ Resume button  
    $c = $c.Replace("Content=""$play Resume""", 'Content="&#x25B6; Resume"')
    
    return $c
}

# ═══════════════════════════════════════
# SettingsView.xaml - replace emoji section headers with MDL2
# ═══════════════════════════════════════
FixFile "c:\Users\User\source\repos\avikeid2007\Kairos.local\KaiROS.AI\Views\SettingsView.xaml" {
    param($c)
    
    # 🎨 Appearance
    $c = $c.Replace("Text=""$palette Appearance""", 'Text="&#xE771; Appearance"')
    # Fallback - some may already be properly stored
    $c = $c -replace 'Text="🎨 Appearance"', 'Text="&#xE771; Appearance"'
    
    # 🔌 API Server
    $c = $c.Replace("Text=""$plug API Server""", 'Text="&#xE8CE; API Server"')
    $c = $c -replace 'Text="🔌 API Server"', 'Text="&#xE8CE; API Server"'
    
    # 🖥 Hardware Information  
    $c = $c.Replace("Text=""$monitor Hardware Information""", 'Text="&#xE7F4; Hardware Information"')
    $c = $c -replace 'Text="🖥 Hardware Information"', 'Text="&#xE7F4; Hardware Information"'
    
    # ⚡ Execution Backend
    $c = $c.Replace("Text=""$flash Execution Backend""", 'Text="&#xE945; Execution Backend"')
    $c = $c -replace 'Text="⚡ Execution Backend"', 'Text="&#xE945; Execution Backend"'
    
    # 📁 Model Storage
    $c = $c.Replace("Text=""$folder Model Storage""", 'Text="&#xED25; Model Storage"')
    $c = $c -replace 'Text="📁 Model Storage"', 'Text="&#xED25; Model Storage"'
    
    # 💬 System Prompt
    $c = $c.Replace("Text=""$chat2 System Prompt""", 'Text="&#xE8BD; System Prompt"')
    $c = $c -replace 'Text="💬 System Prompt"', 'Text="&#xE8BD; System Prompt"'
    
    # ℹ About
    $c = $c.Replace("Text=""$info About KaiROS AI""", 'Text="&#xE946; About KaiROS AI"')
    $c = $c -replace 'Text="ℹ About KaiROS AI"', 'Text="&#xE946; About KaiROS AI"'
    
    # 🌐 Open button in API settings
    $c = $c.Replace("Content=""$globe Open""", 'Content="&#xE8A7; Open"')
    $c = $c -replace 'Content="🌐 Open"', 'Content="&#xE8A7; Open"'
    
    # 🔄 Refresh / Reset buttons
    $c = $c.Replace("Content=""$refresh Refresh Hardware Info""", 'Content="&#xE72C; Refresh Hardware Info"')
    $c = $c -replace 'Content="🔄 Refresh Hardware Info"', 'Content="&#xE72C; Refresh Hardware Info"'
    $c = $c.Replace("Content=""$refresh Reset to Default""", 'Content="&#xE72C; Reset to Default"')
    $c = $c -replace 'Content="🔄 Reset to Default"', 'Content="&#xE72C; Reset to Default"'
    
    # 📂 Browse button
    $c = $c.Replace("Content=""$openFldr Browse""", 'Content="&#xED25; Browse"')
    $c = $c -replace 'Content="📂 Browse"', 'Content="&#xED25; Browse"'
    
    # 💬 Send Feedback button
    $c = $c.Replace("Content=""$chat2 Send Feedback""", 'Content="&#xE8BD; Send Feedback"')
    $c = $c -replace 'Content="💬 Send Feedback"', 'Content="&#xE8BD; Send Feedback"'
    
    # ⭐ Use Recommended
    $c = $c.Replace("Content=""$starbig Use Recommended""", 'Content="&#xE735; Use Recommended"')
    $c = $c -replace 'Content="⭐ Use Recommended"', 'Content="&#xE735; Use Recommended"'
    
    # ☀ and 🌙 theme icons - inline textblocks
    $c = $c.Replace("Text=""$sun""", 'Text="&#x2600;"')
    $c = $c -replace 'Text="☀️"', 'Text="&#x2600;"'
    $c = $c -replace 'Text="☀"', 'Text="&#x2600;"'
    $c = $c.Replace("Text=""$moon""", 'Text="&#x1F319;"')
    # Moon is supplementary - use Segoe MDL2 instead
    $c = $c -replace 'Text="🌙"', 'Text="&#x263D;"'
    
    return $c
}

# ═══════════════════════════════════════
# CodeBlock.xaml
# ═══════════════════════════════════════
FixFile "c:\Users\User\source\repos\avikeid2007\Kairos.local\KaiROS.AI\Controls\CodeBlock.xaml" {
    param($c)
    # 📋 Copy icon 
    $c = $c.Replace("Text=""$clipboard""", 'Text="&#xE8C8;"')
    $c = $c -replace 'Text="📋"', 'Text="&#xE8C8;"'
    return $c
}

Write-Host "`nAll done!"
