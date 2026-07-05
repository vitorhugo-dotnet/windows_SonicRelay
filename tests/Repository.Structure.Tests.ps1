$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$requiredPaths = @(
    'SonicRelay.Windows.slnx'
    'global.json'
    'Directory.Build.props'
    '.editorconfig'
    '.gitignore'
    'src/SonicRelay.Windows.App/SonicRelay.Windows.App.csproj'
    'src/SonicRelay.Windows.Core/SonicRelay.Windows.Core.csproj'
    'src/SonicRelay.Windows.ApiClient/SonicRelay.Windows.ApiClient.csproj'
    'src/SonicRelay.Windows.Signaling/SonicRelay.Windows.Signaling.csproj'
    'src/SonicRelay.Windows.Audio/SonicRelay.Windows.Audio.csproj'
    'src/SonicRelay.Windows.WebRtc/SonicRelay.Windows.WebRtc.csproj'
    'tests/SonicRelay.Windows.Core.Tests/SonicRelay.Windows.Core.Tests.csproj'
    'tests/SonicRelay.Windows.ApiClient.Tests/SonicRelay.Windows.ApiClient.Tests.csproj'
    'docs/windows-publisher.md'
    'docs/architecture.md'
)

$missingPaths = $requiredPaths | Where-Object {
    -not (Test-Path -LiteralPath (Join-Path $root $_))
}

if ($missingPaths.Count -gt 0) {
    Write-Error "Missing required repository paths:`n$($missingPaths -join "`n")"
}

Write-Host "Repository structure verified: $($requiredPaths.Count) required paths found."
