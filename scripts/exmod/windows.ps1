# Windows-only repairs to machine state outside the checkout.

#region fix-registry

# Repoints the Vintage Story Add/Remove-Programs entry at a real install. Every VS installer shares
# one Inno AppId, so silent-installing the client into .game rewrites that single shared entry and
# leaves a machine-wide install's entry broken. `provision game -Kind client` already snapshots and
# restores it, so this is only needed to recover an entry that was already clobbered.
function Invoke-FixRegistry([string[]]$Argv) {
  Assert-Windows 'fix-registry'
  $installDir = Get-Opt $Argv '-InstallDir' $env:VINTAGE_STORY
  if (-not $installDir) {
    throw "No -InstallDir given and `$env:VINTAGE_STORY is not set. Pass the path to your Vintage Story install."
  }
  $installDir = (Resolve-Path $installDir).Path.TrimEnd('\')
  foreach ($f in 'Vintagestory.exe', 'unins000.exe') {
    if (-not (Test-Path (Join-Path $installDir $f))) {
      throw "'$installDir' does not look like a Vintage Story install (missing $f)."
    }
  }

  $appKey = '{70364653-036D-49B3-8B80-AF39665F29C1}_is1'
  $roots = @(
    'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall',
    'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall',
    'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
  )
  $key = $null
  foreach ($r in $roots) { $p = Join-Path $r $appKey; if (Test-Path $p) { $key = $p; break } }
  if (-not $key) {
    # No entry yet, e.g. only the repo provisioner has ever run. Create one under HKCU.
    $key = Join-Path $roots[0] $appKey
    New-Item -Path $key -Force | Out-Null
  }

  $exe = Join-Path $installDir 'Vintagestory.exe'
  $unins = Join-Path $installDir 'unins000.exe'
  $ver = (Get-Item $exe).VersionInfo.ProductVersion
  $parts = $ver -split '\.'

  $strs = @{
    'Inno Setup: App Path' = $installDir
    'InstallLocation'      = "$installDir\"
    'DisplayName'          = "Vintage Story version $ver"
    'DisplayIcon'          = $exe
    'DisplayVersion'       = $ver
    'UninstallString'      = "`"$unins`""
    'QuietUninstallString' = "`"$unins`" /SILENT"
    'Publisher'            = 'Anego Systems'
  }
  foreach ($n in $strs.Keys) {
    New-ItemProperty -Path $key -Name $n -Value $strs[$n] -PropertyType String -Force | Out-Null
  }
  $dwords = @{
    MajorVersion = [int]$parts[0]; VersionMajor = [int]$parts[0]
    MinorVersion = [int]$parts[1]; VersionMinor = [int]$parts[1]
  }
  foreach ($n in $dwords.Keys) {
    New-ItemProperty -Path $key -Name $n -Value $dwords[$n] -PropertyType DWord -Force | Out-Null
  }

  Write-Host "Repointed the Vintage Story uninstall entry to '$installDir' (version $ver)."
}


Add-ExmodCommand -Group machine -Name fix-registry -Summary "repoint Windows' Vintage Story file association" -Action {
  param([string[]]$Argv) Invoke-FixRegistry $Argv
} -Detail @'
exmod fix-registry [-InstallDir <path>]

Windows only. Every Vintage Story installer shares one Inno Setup AppId, so installing into .game/
rewrites the machine's single uninstall entry and the file association follows it. This points both
back at a real install: -InstallDir, or the default install location.
'@

#endregion
