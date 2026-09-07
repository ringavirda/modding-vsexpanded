# Running the game against this checkout: a client to play in, a dedicated server to host, a headless
# boot that proves the mods load, and the staging step all three share.
#
#   exmod client    the game client with the built mods
#   exmod server    a dedicated server with the built mods
#   exmod smoke     boot a server, verify it, stop it
#   exmod stage     copy built mods into a Mods folder
#   exmod logs      the newest client or server log

#region shared

# Where `exmod stage`'s zero-argument rung, and client/server after it, land the mods for one game
# series: the flat bin/Mods the current series has always used, or a series-suffixed sibling for a
# legacy one, so the three series never overwrite each other's staged copy.
function Get-StageDest([string]$Version) {
  if ($Version -eq $CurrentGameVersion) { return Join-Path $RepoRoot 'bin/Mods' }
  return Join-Path $RepoRoot "bin/Mods-$Version"
}

# Built mod output for one game series. Get-BuiltModDirs (exmod.ps1) already does this for the
# current series, whose build keeps a flat bin/$Configuration/Mods/mod path; a legacy series builds
# with its own TargetFramework in the path (bin/$Configuration/$tfm/Mods/mod - see
# mods/Directory.Build.props), which Get-BuiltModDirs knows nothing about, so this covers both
# uniformly and is what every run command's "no mods named" rung uses.
function Get-RunModDirs([string]$Version, [string]$Configuration, [switch]$NoBuild) {
  $tfm = $GameTfms[$Version]
  $isLegacy = $tfm -ne 'net10.0'
  $targets = Get-ExmodBuildTargets
  $sampleIds = @((Get-ExmodSamples).Keys)
  # A sample only ever targets the current series (see samples/*/*.csproj) - a legacy series buys
  # it nothing, so it drops out here the same way `exmod build` skips it.
  $mods = @($targets.Keys | Where-Object { -not ($isLegacy -and $_ -in $sampleIds) })

  $outputDir = {
    param([string]$mod)
    $srcDir = Split-Path $targets[$mod] -Parent
    if ($isLegacy) { return Join-Path $srcDir "bin/$Configuration/$tfm/Mods/mod" }
    return Join-Path $srcDir "bin/$Configuration/Mods/mod"
  }

  $missing = @($mods | Where-Object { -not (Test-Path (Join-Path (& $outputDir $_) 'modinfo.json')) })
  if ($missing) {
    if ($NoBuild) {
      throw "Not built for $Version ($Configuration): $($missing -join ', '). Drop -NoBuild, or run 'exmod build $Version' first."
    }
    Write-Host "Building $Version (not yet built) ..."
    Invoke-Build @($Version, '-Configuration', $Configuration)
  }

  return @($mods | ForEach-Object { & $outputDir $_ })
}

# Copies mod directories into $Dest, one subfolder per mod named for its own modid (read from
# modinfo.json, never guessed from the path - a built mod's output folder is always literally named
# "mod"). Shared by stage's zero-argument rung and by client/server, so all three land on an
# identical layout.
function Publish-ModDirs([string[]]$ModDirs, [string]$Dest) {
  if (Test-Path $Dest) { Remove-Item -Recurse -Force $Dest }
  New-Item -ItemType Directory -Force -Path $Dest | Out-Null
  foreach ($m in $ModDirs) {
    $modinfo = $null
    try {
      $modinfo = Get-Content (Join-Path $m 'modinfo.json') -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    } catch { }
    if (-not $modinfo -or -not $modinfo.modid) { throw "$m/modinfo.json has no modid to stage under." }
    Copy-Item -Recurse -Force -Path $m -Destination (Join-Path $Dest $modinfo.modid)
    Write-Host "Staged '$($modinfo.modid)' from $m"
  }
  Write-Host "Staged $($ModDirs.Count) mod(s) into $Dest"
}

# The build-and-stage step client and server share: builds (unless -NoBuild) and stages every built
# mod, or the explicit -Mods folders instead, into that series' Mods folder. Returns the folder to
# pass as --addModPath.
function Publish-RunMods([string]$Version, [string]$Configuration, [string]$ModsOpt, [bool]$NoBuild) {
  $dest = Get-StageDest $Version
  $modDirs = if ($ModsOpt) { Resolve-ModDirs @($ModsOpt -split ',') }
  else { Get-RunModDirs $Version $Configuration -NoBuild:$NoBuild }
  if (-not $modDirs) { throw "No mods found to stage." }
  Publish-ModDirs $modDirs $dest
  return $dest
}

# The series a run command was given, as positional [latest|1.22|1.21|1.20] rather than smoke's
# -Version option - 'all' makes no sense for something that launches one game.
function Resolve-RunVersion([string]$Spec) {
  if ($Spec -eq 'latest') { return $CurrentGameVersion }
  if ($GameTfms.Contains($Spec)) { return $Spec }
  throw "Unknown version '$Spec'. Use latest or one of: $($GameTfms.Keys -join ', ')."
}

#endregion
#region client

# A usable install already at .game for $Version/$Kind, without provisioning one - the same test
# Resolve-GameInstall (exmod.ps1) applies, duplicated here because `client` must never let that
# helper's own auto-provisioning reach the network before -Provision says so.
function Find-UsableGameInstall([string]$Version, [string]$Kind) {
  $slug = ($Version -split '\.')[0..1] -join '.'
  $entry = if ($Kind -eq 'server') { 'VintagestoryServer.dll' } else { 'Vintagestory.dll' }
  foreach ($c in @(".game/$slug-$Kind", ".game/$slug")) {
    $full = Join-Path $RepoRoot $c
    if (-not (Test-Path (Join-Path $full $entry))) { continue }
    if ($OnWindows -or (Test-Path (Join-Path $full 'Lib/libe_sqlite3.so'))) { return $full }
  }
  return $null
}

# Launches the real client in the foreground with this checkout's mods loaded, passing its exit
# code straight through. Builds and stages first unless -NoBuild. Never provisions a client on its
# own - the archive is about a gigabyte - unless -Provision is given; otherwise it prints the exact
# command to fetch one and exits 1.
function Invoke-Client([string[]]$Argv) {
  $positional = @(Get-Positional $Argv @('-Configuration', '-Mods', '-DataPath') @('-NoBuild', '-Provision'))
  $versionArg = if ($positional.Count -gt 0) { $positional[0] } else { 'latest' }
  $version = Resolve-RunVersion $versionArg

  $configuration = Get-Opt $Argv '-Configuration' 'Debug'
  $modsOpt = Get-Opt $Argv '-Mods' $null
  $noBuild = Get-Flag $Argv '-NoBuild'
  $dataPath = Get-Opt $Argv '-DataPath' (Join-Path $RepoRoot '.gamedata')
  $provision = Get-Flag $Argv '-Provision'

  $install = Find-UsableGameInstall $version 'client'
  if (-not $install) {
    $provisionCmd = "exmod provision game -Version $version -Kind client"
    if (-not $provision) {
      throw "No usable client install for $version on this platform.`nProvision one with: $provisionCmd`n(or pass -Provision to fetch it here now - it is about a gigabyte)."
    }
    Write-Host "Provisioning a client install for $version ..."
    Invoke-ProvisionGame @('-Version', $version, '-Kind', 'client')
    $install = Find-UsableGameInstall $version 'client'
    if (-not $install) { throw "Provisioning completed but no usable client install was found for $version." }
  }

  $modsDest = Publish-RunMods $version $configuration $modsOpt $noBuild

  # The system dotnet muxer ignores DOTNET_ROOT; the game only sees .dotnet's runtimes when both the
  # host and this variable point there, same as .vscode/launch.json sets it for the debugger.
  $dotnet = Resolve-DotnetHost @($version)
  if ($dotnet -eq (Join-Path $RepoRoot ".dotnet/dotnet$ExeSuffix")) {
    $env:DOTNET_ROOT = Join-Path $RepoRoot '.dotnet'
  }

  Write-Step "Launching the client ($version)"
  & $dotnet (Join-Path $install 'Vintagestory.dll') --tracelog --dataPath $dataPath --addModPath $modsDest
  exit $LASTEXITCODE
}


Add-ExmodCommand -Group run -Name client -Summary 'build, stage and launch the client' -Action {
  param([string[]]$Argv) Invoke-Client $Argv
} -Detail @'
exmod client [latest|1.22|1.21|1.20] [-Configuration Debug] [-Mods <dir>[,...]] [-NoBuild]
             [-DataPath <path>] [-Provision]

Builds this checkout's mods for the series (unless -NoBuild), stages them the way `exmod stage`
would, and runs the real client in the foreground, passing its exit code through. Never downloads a
client on its own - the archive is about a gigabyte - unless -Provision is given; otherwise it
prints the exact `exmod provision game` command to run and exits 1.

  -Mods       mod folder(s), or folder(s) of mod folders, instead of every built mod in the checkout
  -NoBuild    skip the build step; the mods must already be built
  -DataPath   client data path (default: .gamedata)
  -Provision  fetch a client install for this series here, if none is usable yet
'@

#endregion
#region server

# Boots a real dedicated server in the foreground with this checkout's mods loaded, and keeps it
# running until Ctrl+C stops it. Its data path is persistent by default rather than a scratch one,
# so a world survives between runs, and unlike smoke it never sends /stop or kills the process
# itself: it just runs in the foreground and lets Ctrl+C reach it the ordinary way, which is how
# the dedicated server saves and shuts down cleanly on its own.
function Invoke-Server([string[]]$Argv) {
  $positional = @(Get-Positional $Argv @('-Port', '-Configuration', '-Mods', '-DataPath') @('-NoBuild'))
  $versionArg = if ($positional.Count -gt 0) { $positional[0] } else { 'latest' }
  $version = Resolve-RunVersion $versionArg

  $port = [int](Get-Opt $Argv '-Port' 42420)
  $configuration = Get-Opt $Argv '-Configuration' 'Debug'
  $modsOpt = Get-Opt $Argv '-Mods' $null
  $noBuild = Get-Flag $Argv '-NoBuild'
  $dataPath = Get-Opt $Argv '-DataPath' (Join-Path $RepoRoot '.gamedata/server')

  $serverDir = Resolve-SmokeServer $version
  $modsDest = Publish-RunMods $version $configuration $modsOpt $noBuild

  Write-Step "Running the dedicated server ($version) on port $port"
  & dotnet (Join-Path $serverDir 'VintagestoryServer.dll') --dataPath $dataPath --addModPath $modsDest --port $port
  exit $LASTEXITCODE
}


Add-ExmodCommand -Group run -Name server -Summary 'build, stage and run a dedicated server' -Action {
  param([string[]]$Argv) Invoke-Server $Argv
} -Detail @'
exmod server [latest|1.22|1.21|1.20] [-Port <n>] [-Configuration Debug] [-Mods <dir>[,...]]
             [-NoBuild] [-DataPath <path>]

Builds this checkout's mods for the series (unless -NoBuild), stages them the way `exmod stage`
would, and runs a real dedicated server in the foreground until Ctrl+C stops it. Unlike `smoke`,
its data path is persistent by default, so a world survives between runs, and it binds the game's
own default port instead of smoke's deliberately odd one - this is the server you actually play on,
not a throwaway boot.

  -Port       port to listen on (default: 42420, the game's own default)
  -Mods       mod folder(s), or folder(s) of mod folders, instead of every built mod in the checkout
  -NoBuild    skip the build step; the mods must already be built
  -DataPath   persistent server data path (default: .gamedata/server)
'@

#endregion
#region smoke

# An unusual, fixed port for every smoke boot: a game the owner is actually playing on this machine
# binds the default 42420 (or whatever their own serverconfig.json says), and this must never collide
# with it.
$SmokePort = 42499

# Finds a working dedicated-server install for $version's slug (".game/<slug>-server" first, since
# that is where Invoke-ProvisionGame redirects a server request away from an existing client that
# cannot serve it - see its header comment - then the plain ".game/<slug>"), provisioning one if
# neither is present. Returns the full path to the install directory.
function Resolve-SmokeServer([string]$version) {
  $slug = ($version -split '\.')[0..1] -join '.'
  $candidates = @(".game/$slug-server", ".game/$slug")
  foreach ($c in $candidates) {
    $full = Join-Path $RepoRoot $c
    if (Test-Path (Join-Path $full 'VintagestoryServer.dll')) { return $full }
  }
  Write-Host "No dedicated-server install found for $version - provisioning one..."
  Invoke-ProvisionGame @('-Version', $version, '-Kind', 'server')
  foreach ($c in $candidates) {
    $full = Join-Path $RepoRoot $c
    if (Test-Path (Join-Path $full 'VintagestoryServer.dll')) { return $full }
  }
  throw "Provisioning completed but no VintagestoryServer.dll was found under $($candidates -join ' or ')."
}

# Boots the real dedicated server with the given mods, waits for it to come up, runs /exmod verify and
# stops it - the zero-effort rung: no test code, just "does the server load this without erroring".
#
#   -Version <x.y>     game series to smoke against (default 1.22).
#   -Mods <dir>[,...]  mod folder(s) or folder(s)-of-mod-folders to load instead of every built mod.
#   -Timeout N         seconds to wait for "Dedicated Server now running" (default 180).
#   -KeepData          don't delete the scratch dataPath/mods afterwards (for inspecting the failure).
function Invoke-Smoke([string[]]$Argv) {
  $version = Get-Opt $Argv '-Version' '1.22'
  $modsOpt = Get-Opt $Argv '-Mods' $null
  $timeout = [int](Get-Opt $Argv '-Timeout' 180)
  $keepData = Get-Flag $Argv '-KeepData'

  $serverDir = Resolve-SmokeServer $version

  $modDirs = if ($modsOpt) { Resolve-ModDirs @($modsOpt -split ',') } else { Get-BuiltModDirs }
  if (-not $modDirs) { throw "No mods found to smoke-test (nothing under mods/*/src/bin/Debug/Mods/mod, and -Mods was not given)." }

  $scratch = Join-Path $RepoRoot ".game/.smoke-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
  $scratchData = Join-Path $scratch 'data'
  $scratchMods = Join-Path $scratch 'mods'
  New-Item -ItemType Directory -Force -Path $scratchData, $scratchMods | Out-Null

  foreach ($m in $modDirs) {
    # A modinfo.json broken enough that this can't even read it is exactly the kind of mistake the
    # smoke lane exists to catch, so it is copied in under its folder name regardless and left for the
    # real mod loader to report - not failed here, which would only ever say "some folder's modinfo.json
    # doesn't parse" instead of naming the mod and the game's own error.
    $modid = (Split-Path $m -Leaf)
    try {
      $modinfo = Get-Content (Join-Path $m 'modinfo.json') -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
      if ($modinfo.modid) { $modid = $modinfo.modid }
    } catch {
      Write-Host "Warning: $m/modinfo.json did not parse ($($_.Exception.Message)) - copying it in under '$modid' anyway so the server reports it." -ForegroundColor Yellow
    }
    Copy-Item -Recurse -Force -Path $m -Destination (Join-Path $scratchMods $modid)
  }
  Write-Host "Smoke-testing $($modDirs.Count) mod(s) against Vintage Story $version at $serverDir"

  $logPath = Join-Path $scratchData 'Logs/server-main.log'
  $proc = $null
  try {
    # GLIBC_TUNABLES and MSBUILDDISABLENODEREUSE are already in $env: from the top of this script and
    # are inherited by the child unchanged; nothing extra to set here.
    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = 'dotnet'
    foreach ($a in @(
        (Join-Path $serverDir 'VintagestoryServer.dll'), '--dataPath', $scratchData,
        '--addModPath', $scratchMods, '--port', $SmokePort
      )) { $psi.ArgumentList.Add($a) }
    $psi.RedirectStandardInput = $true
    $psi.UseShellExecute = $false
    $psi.WorkingDirectory = $serverDir
    $proc = [System.Diagnostics.Process]::Start($psi)
    $proc.StandardInput.AutoFlush = $true

    Write-Host "Waiting up to ${timeout}s for the server to come up..."
    $deadline = [DateTime]::UtcNow.AddSeconds($timeout)
    $ready = $false
    while ([DateTime]::UtcNow -lt $deadline) {
      if ((Test-Path $logPath) -and (Select-String -Path $logPath -Pattern 'Dedicated Server now running' -Quiet)) {
        $ready = $true
        break
      }
      if ($proc.HasExited) { break }
      Start-Sleep -Seconds 1
    }
    if (-not $ready) {
      $tail = if (Test-Path $logPath) { Get-Content $logPath -Tail 60 } else { '(no log written)' }
      throw "Server did not report ready within ${timeout}s (exited: $($proc.HasExited)).`n$($tail -join "`n")"
    }
    Write-Host "Server is up; running /exmod verify..."

    $proc.StandardInput.WriteLine('/exmod verify')
    $verifyDeadline = [DateTime]::UtcNow.AddSeconds(10)
    while ([DateTime]::UtcNow -lt $verifyDeadline) {
      if (Select-String -Path $logPath -Pattern 'check\(s\) run, \d+ error\(s\) found\.' -Quiet) { break }
      Start-Sleep -Milliseconds 500
    }

    $proc.StandardInput.WriteLine('/stop')
    if (-not $proc.WaitForExit(30000)) {
      Write-Host "Server did not exit after /stop within 30s - killing it."
      $proc.Kill($true)
      $proc.WaitForExit(10000) | Out-Null
    }

    $log = Get-Content $logPath -Raw
    Write-Host ""
    Write-Host "===== [exlib] notification lines ====="
    Select-String -Path $logPath -Pattern '\[exlib\]' | ForEach-Object { Write-Host $_.Line }
    Write-Host ""
    Write-Host "===== Error/Fatal lines ====="
    Select-String -Path $logPath -Pattern '\[Error\]|\[Fatal\]' | ForEach-Object { Write-Host $_.Line }

    $hasErrors = [bool](Select-String -Path $logPath -Pattern '\[Error\]|\[Fatal\]' -Quiet)
    $verifyMatch = [regex]::Match($log, '(\d+) check\(s\) run, (\d+) error\(s\) found\.')
    $verifyErrors = if ($verifyMatch.Success) { [int]$verifyMatch.Groups[2].Value } else { 0 }

    Write-Host ""
    if ($hasErrors) { throw "Smoke failed: the server log holds [Error] or [Fatal] line(s)." }
    if ($verifyErrors -gt 0) { throw "Smoke failed: /exmod verify reported $verifyErrors error(s)." }
    Write-Host "Smoke passed: server booted, verified clean and stopped."
  }
  finally {
    if ($proc -and -not $proc.HasExited) {
      try { $proc.Kill($true) } catch { }
    }
    if ($proc) { $proc.Dispose() }
    if (-not $keepData) {
      Remove-Item -Recurse -Force $scratch -ErrorAction SilentlyContinue
    }
    else {
      Write-Host "Kept scratch data at $scratch"
    }
  }
}


Add-ExmodCommand -Group run -Name smoke -Summary 'boot a server, verify it, stop it' -Action {
  param([string[]]$Argv) Invoke-Smoke $Argv
} -Detail @'
exmod smoke [-Version <x.y>] [-Mods <dir>[,<dir>...]] [-Timeout <seconds>] [-KeepData]

Boots a real dedicated server with the mods loaded, waits for it to come up, runs /exmod verify and
stops it. No test code is involved: the question is only whether a server loads this without
erroring, which is the one check that covers assets, patches and registration at once. It binds an
unusual port so it can never collide with a game you are actually playing on this machine.

  -Mods      mod folders, or folders of mod folders, instead of every built mod in the checkout
  -Timeout   seconds to wait for the server to report itself running (default 180)
  -KeepData  keep the scratch data path and the staged mods, to inspect a failure
'@

#endregion
#region stage

# Copies built mods into a Mods folder for a manual playtest, or for `client`/`server` to launch
# against. With no <name>=<src> pairs, every built mod and sample in the checkout is staged (built
# first, if needed) via Get-RunModDirs; with no -Dest, the destination is the flat bin/Mods the
# current series has always used, or a series-suffixed bin/Mods-<x.y> for a legacy one. Either can
# still be given explicitly, in which case the behaviour is exactly what it always was: copy each
# named <name>=<src> pair as given, no build, no defaulting.
function Invoke-Stage([string[]]$Argv) {
  $version = Get-Opt $Argv '-Version' $CurrentGameVersion
  if (-not $GameTfms.Contains($version)) {
    throw "Unknown version '$version'. Use one of: $($GameTfms.Keys -join ', ')."
  }
  $configuration = Get-Opt $Argv '-Configuration' 'Debug'
  $dest = Get-Opt $Argv '-Dest'
  if (-not $dest) { $dest = Get-StageDest $version }
  $mods = @(Get-Positional $Argv @('-Dest', '-Version', '-Configuration') @())

  if (-not $mods) {
    Publish-ModDirs (Get-RunModDirs $version $configuration) $dest
    return
  }

  if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
  New-Item -ItemType Directory -Force -Path $dest | Out-Null

  foreach ($entry in $mods) {
    $name, $src = $entry -split '=', 2
    if (-not $src) { throw "Bad stage entry '$entry' - expected <name>=<src>." }
    if (-not (Test-Path $src)) { throw "Mod source not found: $src" }
    Copy-Item -Recurse -Force -Path $src -Destination (Join-Path $dest $name)
    Write-Host "Staged '$name' from $src"
  }
  Write-Host "Staged $($mods.Count) mod(s) into $dest"
}


Add-ExmodCommand -Group run -Name stage -Summary 'copy built mods into a Mods folder' -Action {
  param([string[]]$Argv) Invoke-Stage $Argv
} -Detail @'
exmod stage [-Dest <path>] [-Version <x.y>] [-Configuration Debug] [<name>=<src> ...]

Copies built mod output into one Mods folder, a subfolder per mod. This is what `exmod client` and
`exmod server` use to lay out the mods they launch, and what the VS Code launch-prep tasks call.

With no <name>=<src> pairs, every built mod and sample in the checkout is staged - built first if
it isn't yet - under its own modid. With no -Dest, the destination is bin/Mods for the current
series, or bin/Mods-<x.y> for a legacy one named with -Version.

Either can still be given explicitly: -Dest <path> <name>=<src> [<name>=<src> ...] copies exactly
those sources under those names, unbuilt, into exactly that folder - the same as it always has.
'@

#endregion
#region logs

# Prints the tail of one client or server log. Fails with the list of logs that do exist when the
# one asked for is not among them - more useful than "file not found" when, say, a fresh server
# that has never crashed is asked for its crash log.
function Invoke-Logs([string[]]$Argv) {
  $positional = @(Get-Positional $Argv @('-Kind', '-Lines', '-DataPath') @('-Follow'))
  $target = if ($positional.Count -gt 0) { $positional[0] } else { 'client' }
  if ($target -notin @('client', 'server')) { throw "logs needs 'client' or 'server', got '$target'." }

  $kind = Get-Opt $Argv '-Kind' 'main'
  $lines = [int](Get-Opt $Argv '-Lines' 200)
  $follow = Get-Flag $Argv '-Follow'
  $defaultDataPath = if ($target -eq 'client') { Join-Path $RepoRoot '.gamedata' } else { Join-Path $RepoRoot '.gamedata/server' }
  $dataPath = Get-Opt $Argv '-DataPath' $defaultDataPath

  $logsDir = Join-Path $dataPath 'Logs'
  $logPath = Join-Path $logsDir "$target-$kind.log"

  if (-not (Test-Path $logPath)) {
    $existing = if (Test-Path $logsDir) {
      @(Get-ChildItem $logsDir -Filter "$target-*.log" -File | ForEach-Object { $_.Name })
    } else { @() }
    $have = if ($existing) { $existing -join ', ' } else { '(none - the data path has no Logs folder yet)' }
    throw "No $target-$kind.log under $logsDir. Logs that do exist: $have"
  }

  Write-Host "Reading $logPath"
  Get-Content -Path $logPath -Tail $lines -Wait:$follow
}


Add-ExmodCommand -Group run -Name logs -Summary 'tail a client or server log' -Action {
  param([string[]]$Argv) Invoke-Logs $Argv
} -Detail @'
exmod logs [client|server] [-Kind main|debug|audit|chat|crash|build] [-Lines <n>] [-Follow]
           [-DataPath <path>]

Prints the tail of one log. Client logs live at <dataPath>/Logs/client-<kind>.log (default data
path: .gamedata); server logs at <dataPath>/Logs/server-<kind>.log (default: .gamedata/server, the
same default `exmod server` uses). Fails with the list of logs that do exist when the one asked for
is not among them.

  -Kind      main (the default), debug, audit, chat, crash, or build
  -Lines     lines to print from the end (default: 200)
  -Follow    keep tailing it live, like tail -f
  -DataPath  data path to read from instead of the default for client/server
'@

#endregion
