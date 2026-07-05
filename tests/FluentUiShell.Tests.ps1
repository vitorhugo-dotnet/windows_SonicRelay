$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$app = Join-Path $root 'src/SonicRelay.Windows.App'
$requiredFiles = @(
    'Styles/DesignTokens.xaml'
    'Pages/DashboardPage.xaml'
    'Pages/ConnectionPage.xaml'
    'Pages/SessionPage.xaml'
    'Pages/AudioPage.xaml'
    'Pages/DiagnosticsPage.xaml'
    'Pages/SettingsPage.xaml'
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $app $file))) {
        throw "Missing Fluent shell file: $file"
    }
}

$window = Get-Content -Raw (Join-Path $app 'MainWindow.xaml')
$codeBehind = Get-Content -Raw (Join-Path $app 'MainWindow.xaml.cs')
$dashboard = Get-Content -Raw (Join-Path $app 'Pages/DashboardPage.xaml')
$tokens = Get-Content -Raw (Join-Path $app 'Styles/DesignTokens.xaml')

@('Dashboard','Connection','Session','Audio','Diagnostics','Settings','NavigationView','TopStatusStrip','LogPanel') | ForEach-Object {
    if ($window -notmatch $_) { throw "MainWindow is missing: $_" }
}

@('Authentication','Backend','Current device','Session code','Viewer count','Audio capture','Signaling','Mock state') | ForEach-Object {
    if ($dashboard -notmatch $_) { throw "Dashboard is missing placeholder: $_" }
}

@('SpacingMedium','CornerRadiusMedium','StatusNeutralBrush','TitleFontSize','ThemeDictionaries') | ForEach-Object {
    if ($tokens -notmatch $_) { throw "Design tokens are missing: $_" }
}

if ($codeBehind -notmatch 'MicaBackdrop' -or $codeBehind -notmatch 'SolidFallbackBrush') {
    throw 'MainWindow must configure Mica with a solid fallback.'
}

Write-Host 'Fluent UI shell contract verified.'
