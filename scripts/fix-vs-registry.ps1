param(
    # The real Vintage Story install to repoint the uninstall entry at. Defaults to $VINTAGE_STORY.
    [string]$InstallDir = $env:VINTAGE_STORY
)

$ErrorActionPreference = 'Stop'

# Repairs the "Vintage Story" Add/Remove-Programs (uninstall) registry entry so it points at a real
# install. All Vintage Story installers share one Inno Setup AppId, so silent-installing the client
# into .game/<ver> (see provision-game.ps1 -Kind client) rewrites that single shared entry to point
# at the repo copy - leaving a machine-wide install's entry broken. Run this to point it back.
#
#   scripts/fix-vs-registry.ps1 -InstallDir "D:\Path\To\Vintagestory"
#
# provision-game.ps1 already snapshots+restores this entry around its own client installs, so this is
# only needed to recover an entry that was already clobbered (or to (re)register an install).

if (-not $InstallDir) {
    throw "No -InstallDir given and `$env:VINTAGE_STORY is not set. Pass the path to your real Vintage Story install."
}
$InstallDir = (Resolve-Path $InstallDir).Path.TrimEnd('\')
foreach ($f in 'Vintagestory.exe', 'unins000.exe') {
    if (-not (Test-Path (Join-Path $InstallDir $f))) {
        throw "'$InstallDir' does not look like a Vintage Story install (missing $f)."
    }
}

# The VS installer registers under this fixed AppId; per-user installs land in HKCU, admin in HKLM.
$appKey = '{70364653-036D-49B3-8B80-AF39665F29C1}_is1'
$roots = @(
    'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall',
    'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall',
    'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
)
$key = $null
foreach ($r in $roots) { $p = Join-Path $r $appKey; if (Test-Path $p) { $key = $p; break } }
if (-not $key) {
    # No entry yet (e.g. the user only ever ran the repo's provisioner): create one under HKCU.
    $key = Join-Path $roots[0] $appKey
    New-Item -Path $key -Force | Out-Null
}

$exe = Join-Path $InstallDir 'Vintagestory.exe'
$unins = Join-Path $InstallDir 'unins000.exe'
$ver = (Get-Item $exe).VersionInfo.ProductVersion
$parts = $ver -split '\.'

$strs = @{
    'Inno Setup: App Path' = $InstallDir
    'InstallLocation'      = "$InstallDir\"
    'DisplayName'          = "Vintage Story version $ver"
    'DisplayIcon'          = $exe
    'DisplayVersion'       = $ver
    'UninstallString'      = "`"$unins`""
    'QuietUninstallString' = "`"$unins`" /SILENT"
    'Publisher'            = 'Anego Systems'
}
foreach ($n in $strs.Keys) { New-ItemProperty -Path $key -Name $n -Value $strs[$n] -PropertyType String -Force | Out-Null }

$dwords = @{ MajorVersion = [int]$parts[0]; VersionMajor = [int]$parts[0]; MinorVersion = [int]$parts[1]; VersionMinor = [int]$parts[1] }
foreach ($n in $dwords.Keys) { New-ItemProperty -Path $key -Name $n -Value $dwords[$n] -PropertyType DWord -Force | Out-Null }

Write-Host "Repointed the Vintage Story uninstall entry to '$InstallDir' (version $ver)."
