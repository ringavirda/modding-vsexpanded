param(
    # Which game version(s) to make runnable: latest (1.22) / a series / all.
    [ValidateSet('latest', 'all', '1.22', '1.21', '1.20')]
    [string]$Version = 'latest',
    # Re-install even if a matching runtime is already present in .dotnet.
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

# Builds a SELF-CONTAINED .NET install under the repo's .dotnet folder so a fresh clone can run the
# tests and launch the game without the modder hand-installing .NET 7/8/10. Each Vintage Story version
# is a framework-dependent app pinned to one major (net10=1.22, net8=1.21, net7=1.20) that won't roll
# forward across majors, and the legacy test hosts need those same majors' base runtimes.
#
# IMPORTANT: the GLOBAL `dotnet` muxer ignores DOTNET_ROOT (verified), so extra runtimes are only used
# when invoked through THIS install's own muxer (.dotnet/dotnet). Hence we install a full SDK here too,
# and the test/launch entry points call .dotnet/dotnet when a needed runtime is missing system-wide.
# Installs via Microsoft's official dotnet-install script (no admin - lands in .dotnet, not the machine).

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$dotnetDir = Join-Path $repoRoot '.dotnet'
$onWindows = [System.OperatingSystem]::IsWindows()

# game version -> .NET release channel.
$channels = [ordered]@{ '1.22' = '10.0'; '1.21' = '8.0'; '1.20' = '7.0' }
$sdkChannel = '10.0'  # one current SDK builds every supported TFM
$wanted = switch ($Version) {
    'latest' { @('1.22') }
    'all' { @($channels.Keys) }
    default { @($Version) }
}

function Test-Framework([string]$framework, [string]$major) {
    $p = Join-Path $dotnetDir "shared/$framework"
    if (-not (Test-Path $p)) { return $false }
    return @(Get-ChildItem $p -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "$major.*" }).Count -gt 0
}
function Test-Sdk([string]$major) {
    $p = Join-Path $dotnetDir 'sdk'
    if (-not (Test-Path $p)) { return $false }
    return @(Get-ChildItem $p -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "$major.*" }).Count -gt 0
}

$cache = Join-Path $dotnetDir '.cache'
New-Item -ItemType Directory -Force -Path $cache | Out-Null
$installer = Join-Path $cache 'dotnet-install.ps1'
if (-not (Test-Path $installer)) {
    Write-Host "Fetching the official dotnet-install script"
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -UseBasicParsing
}
# The SDK (also provides the current major's base runtime).
if ($Force -or -not (Test-Sdk $sdkChannel.Split('.')[0])) {
    Write-Host "Installing the .NET $sdkChannel SDK into .dotnet ..."
    & $installer -Channel $sdkChannel -InstallDir $dotnetDir -NoPath
}

foreach ($v in $wanted) {
    $chan = $channels[$v]
    $major = $chan.Split('.')[0]
    # Base runtime (test hosts + the cross-platform game core). The SDK already supplies the current one.
    if ($Force -or -not (Test-Framework 'Microsoft.NETCore.App' $major)) {
        Write-Host "Installing the .NET $chan runtime for Vintage Story $v ..."
        & $installer -Runtime dotnet -Channel $chan -InstallDir $dotnetDir -NoPath
    }
    # Windows client also needs the Desktop runtime (Microsoft.WindowsDesktop.App).
    if ($onWindows -and ($Force -or -not (Test-Framework 'Microsoft.WindowsDesktop.App' $major))) {
        Write-Host "Installing the .NET $chan Desktop runtime for Vintage Story $v ..."
        & $installer -Runtime windowsdesktop -Channel $chan -InstallDir $dotnetDir -NoPath
    }
}

Write-Host "Self-contained .NET ready in .dotnet for version(s): $($wanted -join ', ')"
