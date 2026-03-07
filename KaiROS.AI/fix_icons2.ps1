$enc = [System.Text.Encoding]::UTF8
$w   = [System.Text.Encoding]::GetEncoding(1252)
function G([byte[]] $b) { return $w.GetString($b) }
function FixFile([string]$path, [scriptblock]$fb) {
    $c = [System.IO.File]::ReadAllText($path, $enc); $orig = $c
    $c = & $fb $c
    if ($c -ne $orig) { [System.IO.File]::WriteAllText($path, $c, $enc); Write-Host "FIXED: $(Split-Path $path -Leaf)" }
    else { Write-Host "No changes: $(Split-Path $path -Leaf)" }
}
$robot = G(@(0xF0,0x9F,0xA4,0x96)); $circles = G(@(0xE2,0x97,0x8F))*3
$brain = G(@(0xF0,0x9F,0xA7,0xA0)); $globe = G(@(0xF0,0x9F,0x8C,0x90))
$enterK = G(@(0xE2,0x8E,0x8B)); $pagedoc = G(@(0xF0,0x9F,0x93,0x84))
$pic = G(@(0xF0,0x9F,0x96,0xBC)); $sendA = G(@(0xE2,0x9E,0xA4))
$stopB = G(@(0xE2,0x8F,0xB9)); $flash = G(@(0xE2,0x9A,0xA1))
$bar = G(@(0xF0,0x9F,0x93,0x8A)); $floppy = G(@(0xF0,0x9F,0x92,0xBE))
$timer = G(@(0xE2,0x8F,0xB1)); $ctxpin = G(@(0xF0,0x9F,0x93,0x8C))
$game = G(@(0xF0,0x9F,0x8E,0xAE)); $trash = G(@(0xF0,0x9F,0x97,0x91))
$lens = G(@(0xF0,0x9F,0x94,0x8D)); $star = G(@(0xE2,0x98,0x85))
$check = G(@(0xE2,0x9C,0x93)); $bldg = G(@(0xF0,0x9F,0x8F,0xA2))
$dna = G(@(0xF0,0x9F,0xA7,0xAC)); $gear = G(@(0xE2,0x9A,0x99))
$eye = G(@(0xF0,0x9F,0x91,0x81)); $pauseB = G(@(0xE2,0x8F,0xB8))
$playT = G(@(0xE2,0x96,0xB6)); $clipbd = G(@(0xF0,0x9F,0x93,0x8B))
$reload = G(@(0xF0,0x9F,0x94,0x84)); $bubble = G(@(0xF0,0x9F,0x92,0xAC))
$paint = G(@(0xF0,0x9F,0x8E,0xA8)); $plug = G(@(0xF0,0x9F,0x94,0x8C))
$monitor = G(@(0xF0,0x9F,0x96,0xA5)); $folder = G(@(0xF0,0x9F,0x93,0x81))
$openFld = G(@(0xF0,0x9F,0x93,0x82)); $info = G(@(0xE2,0x84,0xB9))
$starbig = G(@(0xE2,0xAD,0x90)); $sun = G(@(0xE2,0x98,0x80))
$moon = G(@(0xF0,0x9F,0x8C,0x99)); $vs16 = [char]0xFE0F; $xmark = G(@(0xE2,0x9C,0x95))
Write-Host "Decoded. Starting..."

# --- ChatView ---
FixFile "c:\Users\User\source\repos\avikeid2007\Kairos.local\KaiROS.AI\Views\ChatView.xaml" {
    param($c)
    # Robot assistant
    $old = '<TextBlock Text="' + $robot + ' Assistant"' + "`r`n" + "                                                       Foreground=""{StaticResource PrimaryLightBrush}""`r`n                                                       FontSize=""12""`r`n                                                       FontWeight=""SemiBold""`r`n                                                       VerticalAlignment=""Center""/>"
    $new = '<StackPanel Orientation="Horizontal" VerticalAlignment="Center"><FontIcon FontFamily="Segoe MDL2 Assets" Glyph="&#xE9CE;" FontSize="13" Foreground="{StaticResource PrimaryLightBrush}" Margin="0,0,6,0" VerticalAlignment="Center"/><TextBlock Text="Assistant" Foreground="{StaticResource PrimaryLightBrush}" FontSize="12" FontWeight="SemiBold" VerticalAlignment="Center"/></StackPanel>'
    $c = $c.Replace($old, $new)
    # Streaming dots -> ProgressRing
    $old2 = '<TextBlock Text="' + $circles + '"'
    $c = $c.Replace($old2, '<ProgressRing Width="16" Height="16" IsActive="True"')
    # Knowledge Base label
    $c = $c.Replace('<TextBlock Text="' + $brain + [char]0xA0 + ' Knowledge Base"', '<TextBlock Text="Knowledge Base"')
    $c = $c.Replace('<TextBlock Text="' + $brain + ' Knowledge Base"', '<TextBlock Text="Knowledge Base"')
    # Web Search label
    $c = $c.Replace('<TextBlock Text="' + $globe + ' Web Search"', '<TextBlock Text="Web Search"')
    # Enter to send
    $c = $c.Replace('<TextBlock Text="' + $enterK + ' Enter to Send"', '<TextBlock Text="Enter to Send"')
    # Doc indicator icon
    $c = $c.Replace('<TextBlock Text="' + $pagedoc + '" Margin="0,0,6,0"/>', '<FontIcon FontFamily="Segoe MDL2 Assets" Glyph="&#xE8A5;" FontSize="14" Margin="0,0,6,0" VerticalAlignment="Center"/>')
    # Image indicator icon
    $c = $c.Replace('<TextBlock Text="' + $pic + '" Margin="0,0,6,0"/>', '<FontIcon FontFamily="Segoe MDL2 Assets" Glyph="&#xEB9F;" FontSize="14" Margin="0,0,6,0" VerticalAlignment="Center"/>')
    # Remove doc button
    $c = $c.Replace("<Button Content=""$xmark""" + "`r`n                                     Command=""{Binding RemoveDocumentCommand}""", "<Button Command=""{Binding RemoveDocumentCommand}""")
    # Remove image button
    $c = $c.Replace("<Button Content=""$xmark""" + "`r`n                                     Command=""{Binding RemoveAttachedImageCommand}""", "<Button Command=""{Binding RemoveAttachedImageCommand}""")
    # Image attach button
    $c = $c.Replace(" Content=""$pic""" + "`r`n                            ToolTipService.ToolTip=""Attach Image", "`r`n                            ToolTipService.ToolTip=""Attach Image")
    # Doc upload button
    $c = $c.Replace(" Content=""$pagedoc""" + "`r`n                            ToolTipService.ToolTip=""Attach text document""", "`r`n                            ToolTipService.ToolTip=""Attach text document""")
    # Send button
    $c = $c.Replace('Content="Send ' + $sendA + '"/>', '><StackPanel Orientation="Horizontal"><TextBlock Text="Send" VerticalAlignment="Center"/><FontIcon FontFamily="Segoe MDL2 Assets" Glyph="&#xE724;" FontSize="14" Margin="6,0,0,0" VerticalAlignment="Center"/></StackPanel></Button')
    # Stop button
    $c = $c.Replace('Content="' + $stopB + ' Stop"/>', '><StackPanel Orientation="Horizontal"><FontIcon FontFamily="Segoe MDL2 Assets" Glyph="&#xE71A;" FontSize="14" VerticalAlignment="Center"/><TextBlock Text=" Stop" VerticalAlignment="Center"/></StackPanel></Button')
    # Stats icons
    $c = $c.Replace('<Run Text="' + $flash + '"/>', '<Run FontFamily="Segoe MDL2 Assets" Text="&#xE945;"/>')
    $c = $c.Replace('<Run Text="' + $bar + '"/>', '<Run FontFamily="Segoe MDL2 Assets" Text="&#xE9F3;"/>')
    $c = $c.Replace('<Run Text="' + $floppy + '"/>', '<Run FontFamily="Segoe MDL2 Assets" Text="&#xE950;"/>')
    $c = $c.Replace('<Run Text="' + $timer + '"/>', '<Run FontFamily="Segoe MDL2 Assets" Text="&#xE916;"/>')
    $c = $c.Replace('<Run Text="' + $ctxpin + '"/>', '<Run FontFamily="Segoe MDL2 Assets" Text="&#xE97D;"/>')
    $c = $c.Replace('<Run Text="' + $game + '"/>', '<Run FontFamily="Segoe MDL2 Assets" Text="&#xECE4;"/>')
    return $c
}

# --- ModelCatalogView ---
FixFile "c:\Users\User\source\repos\avikeid2007\Kairos.local\KaiROS.AI\Views\ModelCatalogView.xaml" {
    param($c)
    $c = $c.Replace('<TextBlock Text="' + $bldg + ' Org:"', '<TextBlock Text="Org:"')
    $c = $c.Replace('<TextBlock Text="' + $dna + ' Family:"', '<TextBlock Text="Family:"')
    $c = $c.Replace('<TextBlock Text="' + $gear + $vs16 + ' Variant:"', '<TextBlock Text="Variant:"')
    $c = $c.Replace('<TextBlock Text="' + $gear + ' Variant:"', '<TextBlock Text="Variant:"')
    $c = $c.Replace('<TextBlock Text="' + $eye + $vs16 + ' Vision:"', '<TextBlock Text="Vision:"')
    $c = $c.Replace('<TextBlock Text="' + $eye + ' Vision:"', '<TextBlock Text="Vision:"')
    $c = $c.Replace('Content="' + $star + ' Recommended"', 'Content="&#x2605; Recommended"')
    $c = $c.Replace('<TextBlock Text="' + $lens + '" VerticalAlignment="Center" Foreground="{StaticResource TextSecondaryBrush}" Margin="0,0,6,0"/>', '<FontIcon FontFamily="Segoe MDL2 Assets" Glyph="&#xE721;" FontSize="14" VerticalAlignment="Center" Foreground="{StaticResource TextSecondaryBrush}" Margin="0,0,6,0"/>')
    $c = $c.Replace('<TextBlock Text="' + $check + ' Downloaded"', '<TextBlock Text="&#x2713; Downloaded"')
    $c = $c.Replace('<Run Text="' + $floppy + '"/>', '<Run Text=" "/>')
    $c = $c.Replace('<Run Text="' + $brain + '"/>', '<Run Text=" "/>')
    $c = $c.Replace(' Content="' + $trash + '" ToolTipService.ToolTip="Delete model"/>', '><FontIcon FontFamily="Segoe MDL2 Assets" Glyph="&#xE74D;" FontSize="14" ToolTipService.ToolTip="Delete model"/></Button')
    $c = $c.Replace(' Content="' + $pauseB + ' Pause"', ' Content="&#x23F8; Pause"')
    $c = $c.Replace(' Content="' + $playT + ' Resume"', ' Content="&#x25B6; Resume"')
    return $c
}

# --- SettingsView ---
FixFile "c:\Users\User\source\repos\avikeid2007\Kairos.local\KaiROS.AI\Views\SettingsView.xaml" {
    param($c)
    $c = $c.Replace('Text="' + $paint + ' Appearance"',   'Text="&#xE771; Appearance"')
    $c = $c.Replace('Text="' + $plug + ' API Server"',    'Text="&#xE714; API Server"')
    $c = $c.Replace('Text="' + $monitor + ' Hardware Information"', 'Text="&#xE7F4; Hardware Information"')
    $c = $c.Replace('Text="' + $monitor + $vs16 + ' Hardware Information"', 'Text="&#xE7F4; Hardware Information"')
    $c = $c.Replace('Text="' + $flash + ' Execution Backend"', 'Text="&#xE945; Execution Backend"')
    $c = $c.Replace('Text="' + $folder + ' Model Storage"', 'Text="&#xED25; Model Storage"')
    $c = $c.Replace('Text="' + $bubble + ' System Prompt"', 'Text="&#xE8BD; System Prompt"')
    $c = $c.Replace('Text="' + $info + ' About KaiROS AI"', 'Text="&#xE946; About KaiROS AI"')
    $c = $c.Replace('Text="' + $info + $vs16 + ' About KaiROS AI"', 'Text="&#xE946; About KaiROS AI"')
    $c = $c.Replace('Content="' + $globe + ' Open"',             'Content="&#xE8A7; Open"')
    $c = $c.Replace('Content="' + $reload + ' Refresh Hardware Info"', 'Content="&#xE72C; Refresh Hardware Info"')
    $c = $c.Replace('Content="' + $reload + ' Reset to Default"', 'Content="&#xE72C; Reset to Default"')
    $c = $c.Replace('Content="' + $openFld + ' Browse"',         'Content="&#xED25; Browse"')
    $c = $c.Replace('Content="' + $bubble + ' Send Feedback"',   'Content="&#xE8BD; Send Feedback"')
    $c = $c.Replace('Content="' + $starbig + ' Use Recommended"', 'Content="&#xE735; Use Recommended"')
    $c = $c.Replace('Text="' + $sun + '"', 'Text="&#x2600;"')
    $c = $c.Replace('Text="' + $sun + $vs16 + '"', 'Text="&#x2600;"')
    $c = $c.Replace('Text="' + $moon + '"', 'Text="&#x263D;"')
    return $c
}

# --- CodeBlock ---
FixFile "c:\Users\User\source\repos\avikeid2007\Kairos.local\KaiROS.AI\Controls\CodeBlock.xaml" {
    param($c)
    $c = $c.Replace('Text="' + $clipbd + '"', 'Text="&#xE8C8;"')
    return $c
}
Write-Host "All done!"