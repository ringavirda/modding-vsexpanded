# Provisioning: the toolchain and the game, both fetched into the checkout rather than installed on
# the machine. Everything here is safe to re-run; nothing is downloaded twice unless -Force says so.
#
#   exmod setup                  the whole first run on a fresh clone
#   exmod provision dotnet       a self-contained .NET with every runtime major the lanes need
#   exmod provision game         a Vintage Story install under .game/

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
      # The shell installer takes POSIX-style kebab-case flags rather than PowerShell parameter
      # names: -InstallDir becomes --install-dir, -Channel becomes --channel.
      $sh = @()
      for ($i = 0; $i -lt $InstallArgs.Count; $i += 2) {
        $name = $InstallArgs[$i].TrimStart('-')
        $sh += ('--' + [regex]::Replace($name, '(?<!^)([A-Z])', '-$1').ToLower())
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
  $patcher = Join-Path $RepoRoot 'infra/tools/patch-api.cs'
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
# "Superset" assumes the client at the default slot was built for the platform doing the provisioning;
# when it was not (e.g. this slug's slot still holds a Windows client after a move to Linux/macOS), a
# default -Dest is redirected to "<dest>-server" rather than overwriting the client the owner plays from.
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
  $destGiven = [bool]$dest
  if (-not $dest) { $dest = ".game/$slug" }
  # An absolute -Dest is used as given; Join-Path would otherwise concatenate it onto the repo root
  # (an absolute second segment does not make Join-Path treat it as rooted).
  $destFull = if ([System.IO.Path]::IsPathRooted($dest)) { $dest } else { Join-Path $RepoRoot $dest }
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
    # Present only in a tarball/zip built for this platform - a client left over from another OS (e.g.
    # a Windows package on a box that has since moved to Linux) has Vintagestory.dll but none of these.
    $nativeMarker = Join-Path $destFull 'Lib/libe_sqlite3.so'
    $stamp = Join-Path $destFull '.vsversion'
    $installed = if (Test-Path $stamp) { (Get-Content $stamp -Raw).Trim() } else { '' }
    $clientPresent = Test-Path $clientMarker
    $clientUsableHere = $clientPresent -and ($OnWindows -or (Test-Path $nativeMarker))

    if (-not $destGiven -and $kind -eq 'server' -and $clientPresent -and -not $clientUsableHere) {
      # The default slot holds a client for a different platform. It is still the install the owner
      # plays from (perhaps from before a Linux/macOS migration) and must not be overwritten just
      # because it cannot serve this platform's dedicated server; provision alongside it instead.
      Write-Host "Vintage Story client at $dest is not usable as this platform's server (no native Lib/*.so) - provisioning a separate server layout."
      $dest = "$dest-server"
      $destFull = Join-Path $RepoRoot $dest
      $apiMarker = Join-Path $destFull 'VintagestoryAPI.dll'
      $clientMarker = Join-Path $destFull 'Vintagestory.dll'
      $nativeMarker = Join-Path $destFull 'Lib/libe_sqlite3.so'
      $stamp = Join-Path $destFull '.vsversion'
      $installed = if (Test-Path $stamp) { (Get-Content $stamp -Raw).Trim() } else { '' }
      $clientPresent = Test-Path $clientMarker
      $clientUsableHere = $clientPresent -and ($OnWindows -or (Test-Path $nativeMarker))
    }

    if (-not $force) {
      # A server request must never downgrade an existing usable client: the client already satisfies
      # the build and tests, and this is what stops an auto-provisioning build clobbering it.
      if ($kind -eq 'server' -and $clientUsableHere) {
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


Add-ExmodCommand -Group start -Name provision -Summary 'fetch the toolchain or the game into the checkout' -Action {
  param([string[]]$Argv)
  $what = if ($Argv.Count -gt 0) { $Argv[0] } else { '' }
  $rest = if ($Argv.Count -gt 1) { $Argv[1..($Argv.Count - 1)] } else { @() }
  switch ($what) {
    'game' { Invoke-ProvisionGame $rest }
    'dotnet' { Invoke-ProvisionDotnet $rest }
    default { throw "provision needs 'game' or 'dotnet'." }
  }
} -Detail @'
exmod provision dotnet [-Version latest|all|1.22|1.21|1.20] [-Force]
exmod provision game -Version <x.y[.z]> [-Kind server|client] [-Dest <path>] [-Force]

Both fetch into the checkout, never onto the machine, and both are safe to re-run.

  dotnet   a self-contained SDK plus every runtime major the requested series need (net10 for 1.22,
           net8 for 1.21, net7 for 1.20) under .dotnet/. Commands that need those runtimes drive
           .dotnet/dotnet, because the global muxer ignores DOTNET_ROOT and cannot see them.

  game     a Vintage Story install under .game/<series>. -Kind server (the default) takes the
           dedicated-server archive, which carries every assembly the build and the tests need and
           needs no game licence; -Kind client takes the full client, to play in. A server request
           never overwrites a client that can already serve - it lands in .game/<series>-server
           instead. -Version takes a full patch (1.22.3) or a series (1.22, its latest patch).
'@

#endregion

#region setup

# The whole first run on a fresh clone: the runtimes the requested series need, a game install for
# each, and a solution restore. Everything it does is something another command would do on demand,
# so this is a convenience rather than a precondition - it just does the waiting up front, once,
# instead of in the middle of the first build.
function Invoke-Setup([string[]]$Argv) {
  $spec = @(Get-Positional $Argv @('-Version', '-Kind') @('-Force'))
  $version = if ($spec.Count -gt 0) { $spec[0] } else { (Get-Opt $Argv '-Version' 'latest') }
  $kind = Get-Opt $Argv '-Kind' 'server'
  $force = Get-Flag $Argv '-Force'
  $versions = Resolve-GameVersions $version

  Write-Step ".NET for $($versions -join ', ')"
  $dotnet = Resolve-DotnetHost $versions
  Write-Host "dotnet host: $dotnet"

  foreach ($v in $versions) {
    Write-Step "Vintage Story $v ($kind)"
    $provisionArgs = @('-Version', $v, '-Kind', $kind)
    if ($force) { $provisionArgs += '-Force' }
    Invoke-ProvisionGame $provisionArgs
  }

  Write-Step 'Restore'
  Push-Location $RepoRoot
  try {
    & $dotnet restore (Get-ExmodSolution)
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
  } finally {
    Pop-Location
  }

  Write-Host ''
  Write-Host 'Ready.' -ForegroundColor Green
  Write-Host '  exmod build      compile the mods'
  Write-Host '  exmod test       run the suites'
  Write-Host '  exmod client     play this checkout'
  Write-Host '  exmod            everything else'
}

Add-ExmodCommand -Group start -Name setup -Summary 'provision .NET and the game, then restore' -Action {
  param([string[]]$Argv) Invoke-Setup $Argv
} -Detail @'
exmod setup [latest|all|1.22|1.21|1.20] [-Kind server|client] [-Force]

The first run on a fresh clone. Provisions the .NET runtimes the requested series need (only the
ones the machine does not already have), a Vintage Story install for each, and restores the
solution.

Defaults to the current series and the dedicated-server archive, which carries every assembly the
build and the tests need and needs no game licence. Pass -Kind client to get something to play in,
or `all` to set up the legacy lanes too.

Nothing here is a precondition: every command provisions what it needs on demand. This only does
the waiting once, up front, rather than in the middle of the first build.
'@

#endregion
