param(
    # Game version to fetch. A full patch (e.g. 1.22.3) is used as-is; a major.minor series (e.g.
    # 1.22) resolves to the newest stable patch of that series via the Vintage Story version API.
    [Parameter(Mandatory = $true)][string]$Version,
    # Workspace-relative folder to install into. Defaults to .game/<major.minor> off the repo root.
    [string]$Dest,
    # Which distribution to fetch (see the comment block below). Default: server.
    [ValidateSet('server', 'client')][string]$Kind = 'server',
    # Re-provision even when the install already looks complete.
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

# Provisions a Vintage Story install into the repo (no machine-wide install needed) by downloading
# the freely-available distribution for $Version from the public CDN into .game/<slug>. Idempotent.
#
#   -Kind server (default): the dedicated-server zip - a plain archive carrying every assembly the
#       build + headless tests need (VintagestoryAPI.dll, Mods/VSSurvivalMod.dll, Lib/*). Extracted
#       with Expand-Archive: no installer, no registry, no admin, no prompts. This is what CI and the
#       day-to-day build/test loop use.
#   -Kind client: the full playable client, needed only to LAUNCH the game. It ships solely as an Inno
#       Setup installer (no portable client archive exists, and no innoextract build can yet read Inno
#       6.4.3), so this runs a silent install into .game/<slug>. That writes the usual uninstall
#       registry entry and, if a machine-wide install with the same AppId exists, the installer may
#       prompt once - unavoidable for the GUI client. The client install is a superset of the server,
#       so it also satisfies the build/test assemblies.

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

# Resolve a major.minor series (e.g. 1.22) to its newest stable patch via the VS version API; a full
# patch (e.g. 1.22.3) passes through unchanged. Lets the launch tasks track the latest patch while the
# build's compatibility floor stays pinned at the series .0 in Directory.Build.props.
function Resolve-Version([string]$v) {
    if ($v -notmatch '^\d+\.\d+$') { return $v }
    Write-Host "Resolving newest stable patch for series $v"
    $json = Invoke-RestMethod -Uri 'https://api.vintagestory.at/stable.json' -UseBasicParsing
    $cands = @($json.PSObject.Properties.Name | Where-Object { $_ -like "$v.*" })
    if (-not $cands) { throw "No stable release found for series $v." }
    return ($cands | Sort-Object { [version]$_ } -Descending | Select-Object -First 1)
}
$Version = Resolve-Version $Version

# slug = major.minor (1.22.3 -> 1.22); the per-version folder name shared with Directory.Build.props.
$slug = ($Version -split '\.')[0..1] -join '.'
if (-not $Dest) { $Dest = ".game/$slug" }
$destFull = Join-Path $repoRoot $Dest
$cacheDir = Join-Path $repoRoot '.game/.cache'

# Serialize concurrent provisions of the same slug (e.g. parallel MSBuild nodes both auto-provisioning
# on a fresh build). Held for this process's lifetime; the OS releases it when the process exits. An
# AbandonedMutexException means a prior holder exited without releasing - we still acquired the lock
# (the next provision just re-checks the "already provisioned" markers), so it's safe to ignore.
$lock = [System.Threading.Mutex]::new($false, "Local\vs-provision-$($slug -replace '[^\w]', '_')")
try { [void]$lock.WaitOne() } catch [System.Threading.AbandonedMutexException] { }

# "Complete" markers. The build/tests need VintagestoryAPI.dll (present in both kinds); a client
# install additionally carries the client entry assembly, so we only treat .game/<slug> as a finished
# client when that is present too.
$apiMarker = Join-Path $destFull 'VintagestoryAPI.dll'
$clientMarker = Join-Path $destFull 'Vintagestory.dll'
# A version stamp records the exact patch provisioned, so resolving a newer patch (e.g. 1.22.0 -> the
# latest 1.22.x) re-provisions instead of being skipped by a folder that only checks for the slug.
$stamp = Join-Path $destFull '.vsversion'
$installed = if (Test-Path $stamp) { (Get-Content $stamp -Raw).Trim() } else { '' }
$clientPresent = Test-Path $clientMarker
if (-not $Force) {
    # A client install is a SUPERSET of the server (same assemblies + the client entry/native libs), so
    # a server request must never downgrade an existing client - it already satisfies build/test. This
    # is what keeps an auto-provisioning build from clobbering a client provisioned for launch.
    if ($Kind -eq 'server' -and $clientPresent) {
        Write-Host "Vintage Story client already at $Dest - keeping it (it satisfies the server binaries)."
        exit 0
    }
    $haveKind = (Test-Path $apiMarker) -and ($Kind -eq 'server' -or $clientPresent)
    if ($haveKind -and $installed -eq $Version) {
        Write-Host "Vintage Story $Version ($Kind) already provisioned at $Dest"
        exit 0
    }
}

New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null

# Download $Url to $OutFile unless already cached (atomic via a .part temp file).
function Get-Cached {
    param([string]$Url, [string]$OutFile, [string]$Label)
    if (Test-Path $OutFile) { Write-Host "Using cached $Label"; return }
    Write-Host "Downloading $Url"
    $tmp = "$OutFile.part"
    try {
        Invoke-WebRequest -Uri $Url -OutFile $tmp -UseBasicParsing
        Move-Item -Force $tmp $OutFile
    }
    catch {
        if (Test-Path $tmp) { Remove-Item -Force $tmp }
        throw "Failed to download $Url - $($_.Exception.Message)"
    }
}

function Provision-Server {
    $name = "vs_server_win-x64_$Version.zip"
    $zip = Join-Path $cacheDir $name
    Get-Cached -Url "https://cdn.vintagestory.at/gamefiles/stable/$name" -OutFile $zip -Label $name

    Write-Host "Extracting $name to $Dest"
    if (Test-Path $destFull) { Remove-Item -Recurse -Force $destFull }
    New-Item -ItemType Directory -Force -Path $destFull | Out-Null
    Expand-Archive -Path $zip -DestinationPath $destFull -Force

    # Robustness: if the archive ever nests everything under one top dir, lift it up to the root.
    if (-not (Test-Path $apiMarker)) {
        $inner = Get-ChildItem $destFull -Recurse -Depth 1 -Filter VintagestoryAPI.dll -ErrorAction SilentlyContinue |
        Select-Object -First 1
        if ($inner) { Get-ChildItem $inner.Directory.FullName -Force | Move-Item -Destination $destFull -Force }
    }
}

# The VS installer registers under this fixed Inno AppId; per-user installs land in HKCU.
$VsUninstallKey = '{70364653-036D-49B3-8B80-AF39665F29C1}_is1'

# reg.exe-format path of the existing VS uninstall entry, or $null if none is registered.
function Find-VsUninstallReg {
    foreach ($r in @(
            'HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall',
            'HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall',
            'HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall')) {
        $p = "$r\$VsUninstallKey"
        & reg query $p *> $null
        if ($LASTEXITCODE -eq 0) { return $p }
    }
    return $null
}

function Provision-Client {
    $name = "vs_install_win-x64_$Version.exe"
    $exe = Join-Path $cacheDir $name
    Get-Cached -Url "https://cdn.vintagestory.at/gamefiles/stable/$name" -OutFile $exe -Label $name

    # Snapshot the shared uninstall entry so the installer can't permanently clobber it (it shares one
    # AppId with any machine-wide install). We restore it verbatim afterwards; the .game client stays
    # unregistered, which is fine - launching never needs an Add/Remove-Programs entry.
    $regKey = Find-VsUninstallReg
    $backup = $null
    if ($regKey) {
        $b = Join-Path $cacheDir "vs-uninstall-backup-$PID.reg"  # per-process: never reuse a stale file
        & reg export $regKey $b /y *> $null
        if ($LASTEXITCODE -eq 0 -and (Test-Path $b)) { $backup = $b }
    }

    Write-Host "Silent-installing the client to $Dest"
    if (Test-Path $destFull) { Remove-Item -Recurse -Force $destFull }
    New-Item -ItemType Directory -Force -Path $destFull | Out-Null

    try {
        # Inno Setup: /DIR sets the target; /VERYSILENT + /SUPPRESSMSGBOXES run it headless.
        $args = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOICONS', "/DIR=$destFull")
        Write-Host "Running: $name $($args -join ' ')"
        $proc = Start-Process -FilePath $exe -ArgumentList $args -Wait -PassThru
        if ($proc.ExitCode -ne 0) {
            throw "Client install exited with code $($proc.ExitCode). If a UAC prompt appeared, run from an elevated shell."
        }
    }
    finally {
        # Put the original uninstall entry back exactly as it was (delete the installer's, re-import).
        if ($backup -and (Test-Path $backup)) {
            & reg delete $regKey /f *> $null
            & reg import $backup *> $null
            Remove-Item -Force $backup -ErrorAction SilentlyContinue
            Write-Host "Restored the existing Vintage Story uninstall registry entry."
        }
    }
}

if ($Kind -eq 'client') { Provision-Client } else { Provision-Server }

# Verify the result matches what the chosen kind should produce.
if (-not (Test-Path $apiMarker)) {
    throw "Provisioning completed but VintagestoryAPI.dll is missing under $Dest. The archive layout may have changed."
}
if ($Kind -eq 'client' -and -not (Test-Path $clientMarker)) {
    throw "Client install completed but Vintagestory.dll is missing under $Dest."
}

Set-Content -Path $stamp -Value $Version -NoNewline
Write-Host "Provisioned Vintage Story $Version ($Kind) at $Dest"
