$enc = [System.Text.Encoding]::UTF8
function FixFile([string]$path, [scriptblock]$fb) {
    $c = [System.IO.File]::ReadAllText($path, $enc); $orig = $c
    $c = & $fb $c
    if ($c -ne $orig) { [System.IO.File]::WriteAllText($path, $c, $enc); Write-Host "FIXED: $(Split-Path $path -Leaf)" }
    else { Write-Host "No changes: $(Split-Path $path -Leaf)" }
}

# For SettingsView and CodeBlock, the emoji are properly stored as Unicode
# Use direct Unicode string matching
FixFile "c:\Users\User\source\repos\avikeid2007\Kairos.local\KaiROS.AI\Views\SettingsView.xaml" {
    param($c)
    # Section headers - replace emoji with Segoe MDL2 glyph text inline
    $c = $c.Replace([char]0xD83C.ToString() + [char]0xDFA8.ToString() + " Appearance", "&#xE771; Appearance")     # 🎨
    $c = $c.Replace([char]0xD83D.ToString() + [char]0xDD0C.ToString() + " API Server", "&#xE714; API Server")      # 🔌
    $c = $c.Replace([char]0xD83D.ToString() + [char]0xDDA5.ToString() + " Hardware Information", "&#xE7F4; Hardware Information") # 🖥
    $c = $c.Replace([char]0xD83D.ToString() + [char]0xDD04.ToString() + " Refresh Hardware Info", "&#xE72C; Refresh Hardware Info") # 🔄
    $c = $c.Replace([char]0x26A1.ToString() + " Execution Backend", "&#xE945; Execution Backend")                  # ⚡
    $c = $c.Replace([char]0xD83D.ToString() + [char]0xDCC1.ToString() + " Model Storage", "&#xED25; Model Storage") # 📁
    $c = $c.Replace([char]0xD83D.ToString() + [char]0xDCAC.ToString() + " System Prompt", "&#xE8BD; System Prompt") # 💬
    $c = $c.Replace([char]0xD83D.ToString() + [char]0xDCC2.ToString() + " Browse", "&#xED25; Browse")              # 📂
    $c = $c.Replace([char]0xD83D.ToString() + [char]0xDD04.ToString() + " Reset to Default", "&#xE72C; Reset to Default") # 🔄
    $c = $c.Replace([char]0x2139.ToString() + " About KaiROS AI", "&#xE946; About KaiROS AI")                     # ℹ
    $c = $c.Replace([char]0xD83D.ToString() + [char]0xDCAC.ToString() + " Send Feedback", "&#xE8BD; Send Feedback") # 💬
    $c = $c.Replace([char]0xD83C.ToString() + [char]0xDF10.ToString() + " Open", "&#xE8A7; Open")                  # 🌐
    $c = $c.Replace([char]0x2B50.ToString() + " Use Recommended", "&#xE735; Use Recommended")                     # ⭐
    # Sun / moon theme icons
    $c = $c.Replace('Text="' + [char]0x2600 + [char]0xFE0F + '"', 'Text="&#x2600;"')
    $c = $c.Replace('Text="' + [char]0x2600 + '"', 'Text="&#x2600;"')
    $c = $c.Replace('Text="' + [char]0xD83C.ToString() + [char]0xDF19.ToString() + '"', 'Text="&#x263D;"')         # 🌙
    return $c
}

FixFile "c:\Users\User\source\repos\avikeid2007\Kairos.local\KaiROS.AI\Controls\CodeBlock.xaml" {
    param($c)
    # 📋 U+1F4CB = D83D DCBB (surrogate pair)
    $c = $c.Replace('Text="' + [char]0xD83D + [char]0xDCCB + '"', 'Text="&#xE8C8;"')
    return $c
}
Write-Host "Done!"