#!/usr/bin/env pwsh
# Single entry point for every repo task. Runs on Windows, Linux and macOS under pwsh 7: the platform
# differences live in $OnWindows branches below rather than in a second copy of each script that has
# to be kept in step. exmod.sh is a launcher for POSIX shells, not a second implementation - it finds
# pwsh (bootstrapping it into .dotnet/tools if absent) and forwards here.
#
#   exmod test [latest|all|1.22|1.21|1.20] [-Throttle N] [-Coverage]
#   exmod format [-Check]
#   exmod provision game -Version <x.y[.z]> [-Dest <path>] [-Kind server|client] [-Force]
#   exmod provision dotnet [-Version latest|all|1.22|1.21|1.20] [-Force]
#   exmod stage -Dest <path> <name>=<src> [<name>=<src> ...]
#   exmod fix-registry [-InstallDir <path>]     (Windows only)

[CmdletBinding()]
param(
  [Parameter(Position = 0)][string]$Command,
  [Parameter(Position = 1, ValueFromRemainingArguments = $true)][string[]]$Arguments = @()
)

$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$OnWindows = [System.OperatingSystem]::IsWindows()
$ExeSuffix = if ($OnWindows) { '.exe' } else { '' }

#region Argument helpers

# -Name <value>; returns $Default when absent.
function Get-Opt([string[]]$Argv, [string]$Name, $Default = $null) {
  for ($i = 0; $i -lt $Argv.Count; $i++) {
    if ($Argv[$i] -ieq $Name) {
      if ($i + 1 -ge $Argv.Count) { throw "$Name needs a value." }
      return $Argv[$i + 1]
    }
  }
  return $Default
}

# -Name used as a switch.
function Get-Flag([string[]]$Argv, [string]$Name) {
  foreach ($a in $Argv) { if ($a -ieq $Name) { return $true } }
  return $false
}

# Arguments that are neither an option name nor an option value.
function Get-Positional([string[]]$Argv, [string[]]$ValueOpts, [string[]]$FlagOpts) {
  $out = @()
  for ($i = 0; $i -lt $Argv.Count; $i++) {
    $a = $Argv[$i]
    if ($ValueOpts -contains $a) { $i++; continue }
    if ($FlagOpts -contains $a) { continue }
    $out += $a
  }
  # comma keeps a single-element result an array rather than letting PowerShell unroll it
  return , $out
}

function Assert-Windows([string]$What) {
  if (-not $OnWindows) { throw "$What is Windows-only." }
}

#endregion

#region provision dotnet

# Builds a self-contained .NET under .dotnet so a fresh clone can run the tests without the modder
# hand-installing .NET 7/8/10. Each Vintage Story version pins one major (net10=1.22, net8=1.21,
# net7=1.20) and will not roll forward across majors.
#
# The global dotnet muxer ignores DOTNET_ROOT, so extra runtimes are only visible when invoked through
# this install's own muxer (.dotnet/dotnet). That is why a full SDK is installed here too.
function Invoke-ProvisionDotnet([string[]]$Argv) {
  $version = Get-Opt $Argv '-Version' 'latest'
  $force = Get-Flag $Argv '-Force'

  $dotnetDir = Join-Path $RepoRoot '.dotnet'
  $channels = [ordered]@{ '1.22' = '10.0'; '1.21' = '8.0'; '1.20' = '7.0' }
  $sdkChannel = '10.0'
  $wanted = switch ($version) {
    'latest' { @('1.22') }
    'all' { @($channels.Keys) }
    default {
      if (-not $channels.Contains($version)) { throw "Unknown version '$version'." }
      @($version)
    }
  }

  function Test-Framework([string]$Framework, [string]$Major) {
    $p = Join-Path $dotnetDir "shared/$Framework"
    if (-not (Test-Path $p)) { return $false }
    @(Get-ChildItem $p -Directory -ErrorAction SilentlyContinue |
      Where-Object { $_.Name -like "$Major.*" }).Count -gt 0
  }
  function Test-Sdk([string]$Major) {
    $p = Join-Path $dotnetDir 'sdk'
    if (-not (Test-Path $p)) { return $false }
    @(Get-ChildItem $p -Directory -ErrorAction SilentlyContinue |
      Where-Object { $_.Name -like "$Major.*" }).Count -gt 0
  }

  $cache = Join-Path $dotnetDir '.cache'
  New-Item -ItemType Directory -Force -Path $cache | Out-Null

  # Microsoft ships a .ps1 installer for Windows and a .sh for everything else.
  $installer = Join-Path $cache ($OnWindows ? 'dotnet-install.ps1' : 'dotnet-install.sh')
  if (-not (Test-Path $installer)) {
    $url = $OnWindows ? 'https://dot.net/v1/dotnet-install.ps1' : 'https://dot.net/v1/dotnet-install.sh'
    Write-Host "Fetching the official dotnet-install script"
    Invoke-WebRequest -Uri $url -OutFile $installer -UseBasicParsing
    if (-not $OnWindows) { & chmod +x $installer }
  }

  function Install-Dotnet([string[]]$InstallArgs) {
    if ($OnWindows) {
      & $installer @InstallArgs
    } else {
      # The shell installer takes POSIX-style flags rather than PowerShell parameter names.
      $sh = @()
      for ($i = 0; $i -lt $InstallArgs.Count; $i += 2) {
        $sh += ('--' + $InstallArgs[$i].TrimStart('-').ToLower())
        $sh += $InstallArgs[$i + 1]
      }
      & bash $installer @sh --no-path
    }
    if ($LASTEXITCODE -ne 0) { throw "dotnet-install failed ($LASTEXITCODE)." }
  }

  if ($force -or -not (Test-Sdk $sdkChannel.Split('.')[0])) {
    Write-Host "Installing the .NET $sdkChannel SDK into .dotnet ..."
    Install-Dotnet @('-Channel', $sdkChannel, '-InstallDir', $dotnetDir)
  }

  foreach ($v in $wanted) {
    $chan = $channels[$v]
    $major = $chan.Split('.')[0]
    if ($force -or -not (Test-Framework 'Microsoft.NETCore.App' $major)) {
      Write-Host "Installing the .NET $chan runtime for Vintage Story $v ..."
      Install-Dotnet @('-Runtime', 'dotnet', '-Channel', $chan, '-InstallDir', $dotnetDir)
    }
    # Only the Windows client needs the Desktop runtime.
    if ($OnWindows -and ($force -or -not (Test-Framework 'Microsoft.WindowsDesktop.App' $major))) {
      Write-Host "Installing the .NET $chan Desktop runtime for Vintage Story $v ..."
      Install-Dotnet @('-Runtime', 'windowsdesktop', '-Channel', $chan, '-InstallDir', $dotnetDir)
    }
  }

  Write-Host "Self-contained .NET ready in .dotnet for version(s): $($wanted -join ', ')"
}

#endregion

#region provision game

# Publicizes interface members Vintage Story ships as `internal abstract`. Such a member is a vtable
# slot every implementer must fill, but no other assembly is allowed to fill it, so the declaring
# interface cannot be implemented or mocked at all. Applied to the provisioned copy only, which is a
# regenerable build artifact; the shipped mods still target whatever API the player has installed.
# Idempotent, and a no-op on versions that lack the member, so it disappears once upstream fixes it.
function Publicize-GameApi([string]$ApiDll) {
  $patcher = Join-Path $PSScriptRoot 'tools/patch-api.cs'
  if (-not (Test-Path $patcher) -or -not (Test-Path $ApiDll)) { return }
  & dotnet run $patcher -- $ApiDll
  if ($LASTEXITCODE -ne 0) {
    Write-Host "patch-api failed ($LASTEXITCODE); IPlayer cannot be mocked on this install." -ForegroundColor Yellow
  }
}

# Provisions a Vintage Story install into .game/<slug> from the public CDN. No machine-wide install,
# no admin. Idempotent.
#
#   -Kind server (default)  the dedicated-server archive, carrying every assembly the build and the
#                           headless tests need. What CI and the day-to-day loop use.
#   -Kind client            the full playable client, needed only to launch the game. On Windows it
#                           ships solely as an Inno Setup installer, so this silent-installs; on
#                           Linux and macOS it is a plain tarball. A client install is a superset of
#                           the server.
#
# Each platform only ever fetches its own archive, so a Linux checkout never pulls Windows binaries.
function Invoke-ProvisionGame([string[]]$Argv) {
  $version = Get-Opt $Argv '-Version'
  $dest = Get-Opt $Argv '-Dest'
  $kind = Get-Opt $Argv '-Kind' 'server'
  $force = Get-Flag $Argv '-Force'

  if (-not $version) { throw "provision game needs -Version <x.y[.z]>." }
  if ($kind -notin @('server', 'client')) { throw "-Kind must be 'server' or 'client'." }

  # A major.minor series resolves to its newest stable patch; a full patch passes through. This lets
  # launch track the latest patch while the build's compatibility floor stays pinned at the series .0.
  if ($version -match '^\d+\.\d+$') {
    Write-Host "Resolving newest stable patch for series $version"
    $json = Invoke-RestMethod -Uri 'https://api.vintagestory.at/stable.json' -UseBasicParsing
    $cands = @($json.PSObject.Properties.Name | Where-Object { $_ -like "$version.*" })
    if (-not $cands) { throw "No stable release found for series $version." }
    $version = ($cands | Sort-Object { [version]$_ } -Descending | Select-Object -First 1)
  }

  $slug = ($version -split '\.')[0..1] -join '.'
  if (-not $dest) { $dest = ".game/$slug" }
  $destFull = Join-Path $RepoRoot $dest
  $cacheDir = Join-Path $RepoRoot '.game/.cache'
  New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null

  # Serialize concurrent provisions of the same slug, e.g. parallel MSBuild nodes auto-provisioning on
  # a fresh build. An AbandonedMutexException means a prior holder exited without releasing; the lock
  # is still acquired and the markers below are re-checked, so it is safe to ignore.
  $mutexName = ($OnWindows ? 'Local\' : '') + "vs-provision-$($slug -replace '[^\w]', '_')"
  $lock = [System.Threading.Mutex]::new($false, $mutexName)
  try { [void]$lock.WaitOne() } catch [System.Threading.AbandonedMutexException] { }

  try {
    # VintagestoryAPI.dll is in every archive; a client additionally carries the client entry assembly.
    # The version stamp records the exact patch, so resolving a newer patch re-provisions rather than
    # being skipped by a slug folder that already exists.
    $apiMarker = Join-Path $destFull 'VintagestoryAPI.dll'
    $clientMarker = Join-Path $destFull 'Vintagestory.dll'
    $stamp = Join-Path $destFull '.vsversion'
    $installed = if (Test-Path $stamp) { (Get-Content $stamp -Raw).Trim() } else { '' }
    $clientPresent = Test-Path $clientMarker

    if (-not $force) {
      # A server request must never downgrade an existing client: the client already satisfies the
      # build and tests, and this is what stops an auto-provisioning build clobbering it.
      if ($kind -eq 'server' -and $clientPresent) {
        Write-Host "Vintage Story client already at $dest - keeping it (it satisfies the server binaries)."
        Publicize-GameApi $apiMarker
        return
      }
      $haveKind = (Test-Path $apiMarker) -and ($kind -eq 'server' -or $clientPresent)
      if ($haveKind -and $installed -eq $version) {
        Write-Host "Vintage Story $version ($kind) already provisioned at $dest"
        Publicize-GameApi $apiMarker
        return
      }
    }

    # Download unless cached; atomic via a .part temp file.
    function Get-Cached([string]$Url, [string]$OutFile, [string]$Label) {
      if (Test-Path $OutFile) { Write-Host "Using cached $Label"; return }
      Write-Host "Downloading $Url"
      $tmp = "$OutFile.part"
      try {
        Invoke-WebRequest -Uri $Url -OutFile $tmp -UseBasicParsing
        Move-Item -Force $tmp $OutFile
      } catch {
        if (Test-Path $tmp) { Remove-Item -Force $tmp }
        throw "Failed to download $Url - $($_.Exception.Message)"
      }
    }

    $cdn = 'https://cdn.vintagestory.at/gamefiles/stable'

    if (-not $OnWindows) {
      # Both kinds are plain tarballs off Windows.
      $name = "vs_${kind}_linux-x64_$version.tar.gz"
      $tarball = Join-Path $cacheDir $name
      Get-Cached "$cdn/$name" $tarball $name
      Write-Host "Extracting $name to $dest"
      if (Test-Path $destFull) { Remove-Item -Recurse -Force $destFull }
      New-Item -ItemType Directory -Force -Path $destFull | Out-Null
      & tar -xzf $tarball -C $destFull
      if ($LASTEXITCODE -ne 0) { throw "tar failed extracting $name." }
    }
    elseif ($kind -eq 'server') {
      $name = "vs_server_win-x64_$version.zip"
      $zip = Join-Path $cacheDir $name
      Get-Cached "$cdn/$name" $zip $name
      Write-Host "Extracting $name to $dest"
      if (Test-Path $destFull) { Remove-Item -Recurse -Force $destFull }
      New-Item -ItemType Directory -Force -Path $destFull | Out-Null
      Expand-Archive -Path $zip -DestinationPath $destFull -Force
    }
    else {
      $name = "vs_install_win-x64_$version.exe"
      $exe = Join-Path $cacheDir $name
      Get-Cached "$cdn/$name" $exe $name

      # Every VS installer shares one Inno AppId, so installing into .game rewrites the single shared
      # uninstall entry. Snapshot it and restore it verbatim afterwards; the .game client stays
      # unregistered, which is fine because launching never needs an Add/Remove-Programs entry.
      $appKey = '{70364653-036D-49B3-8B80-AF39665F29C1}_is1'
      $regKey = $null
      foreach ($r in @(
          'HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall',
          'HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall',
          'HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall')) {
        $p = "$r\$appKey"
        & reg query $p *> $null
        if ($LASTEXITCODE -eq 0) { $regKey = $p; break }
      }
      $backup = $null
      if ($regKey) {
        $b = Join-Path $cacheDir "vs-uninstall-backup-$PID.reg"
        & reg export $regKey $b /y *> $null
        if ($LASTEXITCODE -eq 0 -and (Test-Path $b)) { $backup = $b }
      }

      Write-Host "Silent-installing the client to $dest"
      if (Test-Path $destFull) { Remove-Item -Recurse -Force $destFull }
      New-Item -ItemType Directory -Force -Path $destFull | Out-Null
      try {
        $innoArgs = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOICONS', "/DIR=$destFull")
        $proc = Start-Process -FilePath $exe -ArgumentList $innoArgs -Wait -PassThru
        if ($proc.ExitCode -ne 0) {
          throw "Client install exited with code $($proc.ExitCode). If a UAC prompt appeared, run from an elevated shell."
        }
      } finally {
        if ($backup -and (Test-Path $backup)) {
          & reg delete $regKey /f *> $null
          & reg import $backup *> $null
          Remove-Item -Force $backup -ErrorAction SilentlyContinue
          Write-Host "Restored the existing Vintage Story uninstall registry entry."
        }
      }
    }

    # Some archives nest everything under one top-level directory; lift it to the root.
    if (-not (Test-Path $apiMarker)) {
      $inner = Get-ChildItem $destFull -Recurse -Depth 1 -Filter VintagestoryAPI.dll -ErrorAction SilentlyContinue |
        Select-Object -First 1
      if ($inner) { Get-ChildItem $inner.Directory.FullName -Force | Move-Item -Destination $destFull -Force }
    }

    if (-not (Test-Path $apiMarker)) {
      throw "Provisioning completed but VintagestoryAPI.dll is missing under $dest. The archive layout may have changed."
    }
    if ($kind -eq 'client' -and -not (Test-Path $clientMarker)) {
      throw "Client install completed but Vintagestory.dll is missing under $dest."
    }

    Set-Content -Path $stamp -Value $version -NoNewline
    Publicize-GameApi $apiMarker
    Write-Host "Provisioned Vintage Story $version ($kind) at $dest"
  } finally {
    $lock.ReleaseMutex()
    $lock.Dispose()
  }
}

#endregion

#region test

# Runs the suite per game version, each version's projects in parallel. The mods stay single-target;
# legacy versions are tested by building the test projects with -p:Legacy=true against that version's
# TFM. Each build auto-provisions its game version on demand (Directory.Build.props), so a clean
# checkout just works.
function Invoke-Test([string[]]$Argv) {
  $positional = @(Get-Positional $Argv @('-Throttle') @('-Coverage'))
  $version = if ($positional.Count -gt 0) { $positional[0] } else { 'latest' }
  $throttle = [int](Get-Opt $Argv '-Throttle' 0)
  $coverage = Get-Flag $Argv '-Coverage'

  $tfms = [ordered]@{ '1.22' = 'net10.0'; '1.21' = 'net8.0'; '1.20' = 'net7.0' }
  # Dependency order: exlib -> iiex -> siex. One suite per mod; a test lives with the top mod it
  # touches, so there is no shared cross-mod project.
  $projects = @(
    'ExpandedLib.Tests',
    'IronIndustryExpanded.Tests',
    'SteelIndustryExpanded.Tests'
  )

  $wanted = switch ($version) {
    'latest' { @('1.22') }
    'all' { @($tfms.Keys) }
    default {
      if (-not $tfms.Contains($version)) { throw "Unknown version '$version'." }
      @($version)
    }
  }

  # Use the system dotnet when it already has every runtime major needed; otherwise provision a local
  # .dotnet and use ITS muxer, because the global muxer ignores DOTNET_ROOT. This is what lets a fresh
  # clone without .NET 7/8 run the legacy suites.
  $majors = @{ '1.22' = '10'; '1.21' = '8'; '1.20' = '7' }
  $needed = @($wanted | ForEach-Object { $majors[$_] } | Select-Object -Unique)
  $sysRuntimes = try { (& dotnet --list-runtimes 2>$null) -join "`n" } catch { '' }
  $missing = @($needed | Where-Object { $sysRuntimes -notmatch "Microsoft\.NETCore\.App $([regex]::Escape($_))\." })
  $dotnet = 'dotnet'
  if ($missing.Count -gt 0) {
    Write-Host "Missing .NET runtime major(s) system-wide: $($missing -join ', ') - provisioning a local .dotnet..."
    Invoke-ProvisionDotnet @('-Version', $version)
    $dotnet = Join-Path $RepoRoot ".dotnet/dotnet$ExeSuffix"
  }
  Write-Host "Using dotnet host: $dotnet"

  # Mirrors .github/workflows/tests.yml: collect cobertura over the solution and ratchet against
  # coverage_gate.py. The gate floors track the current build, so this always uses the latest version.
  if ($coverage) {
    $toolsDir = Join-Path $RepoRoot '.dotnet/tools'
    & $dotnet tool install dotnet-coverage --tool-path $toolsDir 2>$null | Out-Null
    $dc = Join-Path $toolsDir "dotnet-coverage$ExeSuffix"
    $cov = Join-Path $RepoRoot 'coverage.xml'
    Write-Host "Collecting coverage over the latest suite..."
    & $dc collect -f cobertura -o $cov "$dotnet test `"$(Join-Path $RepoRoot 'VintageStory.sln')`" -c Debug --nologo"
    if ($LASTEXITCODE -ne 0) { throw "Coverage collection failed." }
    $py = (Get-Command python -ErrorAction SilentlyContinue) ?? (Get-Command python3 -ErrorAction SilentlyContinue)
    if (-not $py) { throw "Python is required for the coverage gate but was not found (coverage.xml was still written)." }
    & $py.Source (Join-Path $PSScriptRoot 'tools/coverage_gate.py') $cov
    if ($LASTEXITCODE -ne 0) { throw "Coverage gate failed." }
    Write-Host "Coverage gate passed."
    return
  }

  $work = foreach ($v in $wanted) {
    foreach ($p in $projects) {
      [pscustomobject]@{
        Version = $v
        Tfm     = $tfms[$v]
        Project = $p
        Proj    = (Join-Path $RepoRoot "test/$p/$p.csproj")
        Legacy  = ($tfms[$v] -ne 'net10.0')   # legacy TFMs need the multi-target opt-in
      }
    }
  }
  if ($throttle -le 0) { $throttle = $work.Count }

  # Build serially: the test projects share the mod projects, so building concurrently races on the
  # same intermediate DLLs (CS2012). This also auto-provisions each version's game binaries once, up
  # front, letting the test phase run in parallel with --no-build.
  Write-Host "Building $($work.Count) test target(s) across version(s): $($wanted -join ', ')"
  $built = foreach ($item in $work) {
    $buildArgs = @('build', $item.Proj, '-f', $item.Tfm, '--nologo', '-v', 'q')
    if ($item.Legacy) { $buildArgs += '-p:Legacy=true' }
    & $dotnet @buildArgs | Out-Null
    $item | Add-Member -NotePropertyName BuildOk -NotePropertyValue ($LASTEXITCODE -eq 0) -PassThru
  }

  Write-Host "Running tests in parallel..."
  $results = $built | ForEach-Object -ThrottleLimit $throttle -Parallel {
    $dotnet = $using:dotnet
    $item = $_
    if (-not $item.BuildOk) {
      return [pscustomobject]@{ Name = "$($item.Version)/$($item.Project)"; Ok = $false; Line = 'build failed' }
    }
    $testArgs = @('test', $item.Proj, '-f', $item.Tfm, '--no-build', '--nologo')
    if ($item.Legacy) { $testArgs += '-p:Legacy=true' }
    $out = & $dotnet @testArgs 2>&1
    $ok = ($LASTEXITCODE -eq 0)
    $line = ($out | Select-String -Pattern 'Passed!|Failed!|error' | Select-Object -Last 1)

    # The exit code is not enough. An assembly that fails to load during discovery prints
    # "No test is available in ..." and exits 0 with no summary line, so the run reads as a blank PASS
    # while every test in the suite has silently vanished. Treat a missing summary as the failure it is.
    $total = ($out | Select-String -Pattern 'Total:\s*(\d+)' -AllMatches |
      ForEach-Object { $_.Matches } | Select-Object -Last 1)
    if (-not $total) {
      $ok = $false
      $line = 'NO TEST SUMMARY - the assembly discovered no tests (a type-load failure during ' +
      'discovery does this and still exits 0).'
    }

    [pscustomobject]@{ Name = "$($item.Version)/$($item.Project)"; Ok = $ok; Line = $line }
  }

  Write-Host ""
  Write-Host "===== Results ====="
  foreach ($r in $results | Sort-Object Name) {
    $tag = if ($r.Ok) { 'PASS' } else { 'FAIL' }
    Write-Host ("{0}  {1,-40} {2}" -f $tag, $r.Name, ($r.Line -replace '\s+', ' ').Trim())
  }

  $failed = @($results | Where-Object { -not $_.Ok })
  if ($failed) { throw "$($failed.Count) test run(s) failed: $($failed.Name -join ', ')" }
  Write-Host "All $($results.Count) test run(s) passed."
}

#endregion

#region format

# Formats every C# file under src/ and test/ in two passes, and the order is load-bearing. CSharpier
# wraps lines to the printWidth in .csharpierrc but always emits Allman braces and cannot be
# configured; dotnet format then applies .editorconfig, which moves the braces onto the same line.
# Running the pair is idempotent. Running CSharpier alone afterwards would undo the brace style.
#
# `csharpier check` exits 1 on the finished result, so -Check formats and compares against git rather
# than using the tool's own check mode.
function Invoke-Format([string[]]$Argv) {
  $check = Get-Flag $Argv '-Check'
  Push-Location $RepoRoot
  try {
    if ($check -and (git status --porcelain -- src test)) {
      Write-Host "src/ or test/ has uncommitted changes - -Check needs a clean tree." -ForegroundColor Red
      exit 1
    }

    csharpier format src test
    if ($LASTEXITCODE -ne 0) { throw "csharpier exited $LASTEXITCODE" }

    foreach ($dir in @('src', 'test')) {
      dotnet format whitespace $dir --folder
      if ($LASTEXITCODE -ne 0) { throw "dotnet format exited $LASTEXITCODE on $dir" }
    }

    if ($check) {
      if (git status --porcelain -- src test) {
        Write-Host "`nThese files are not formatted:" -ForegroundColor Red
        git diff --name-only -- src test | ForEach-Object { Write-Host "  $_" }
        Write-Host "`nRun scripts/exmod format and commit the result." -ForegroundColor Yellow
        exit 1
      }
      Write-Host "Formatting is clean." -ForegroundColor Green
    }
  } finally {
    Pop-Location
  }
}

#endregion

#region stage

# Copies built mods into a Mods folder for a manual playtest. Each entry is <name>=<source>.
function Invoke-Stage([string[]]$Argv) {
  $dest = Get-Opt $Argv '-Dest'
  if (-not $dest) { throw "stage needs -Dest <path>." }
  $mods = @(Get-Positional $Argv @("-Dest") @())
  if (-not $mods) { throw "stage needs at least one <name>=<src> pair." }

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

#endregion

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

#endregion

switch ($Command) {
  'test' { Invoke-Test $Arguments }
  'format' { Invoke-Format $Arguments }
  'stage' { Invoke-Stage $Arguments }
  'fix-registry' { Invoke-FixRegistry $Arguments }
  'provision' {
    $what = if ($Arguments.Count -gt 0) { $Arguments[0] } else { '' }
    $rest = if ($Arguments.Count -gt 1) { $Arguments[1..($Arguments.Count - 1)] } else { @() }
    switch ($what) {
      'game' { Invoke-ProvisionGame $rest }
      'dotnet' { Invoke-ProvisionDotnet $rest }
      default { throw "provision needs 'game' or 'dotnet'." }
    }
  }
  { $_ -in @('', $null, 'help', '-h', '--help') } {
    Write-Host "exmod - repo tasks`n"
    Write-Host "  exmod test [latest|all|1.22|1.21|1.20] [-Throttle N] [-Coverage]"
    Write-Host "  exmod format [-Check]"
    Write-Host "  exmod provision game -Version <x.y[.z]> [-Dest <path>] [-Kind server|client] [-Force]"
    Write-Host "  exmod provision dotnet [-Version latest|all|1.22|1.21|1.20] [-Force]"
    Write-Host "  exmod stage -Dest <path> <name>=<src> [...]"
    Write-Host "  exmod fix-registry [-InstallDir <path>]     (Windows only)"
  }
  default { throw "Unknown command '$Command'. Run exmod help." }
}
