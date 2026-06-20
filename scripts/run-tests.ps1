param(
    # Which game version(s) to test: latest (1.22) / a series (1.22, 1.21, 1.20) / all.
    [ValidateSet('latest', 'all', '1.22', '1.21', '1.20')]
    [string]$Version = 'latest',
    # Max test projects to run concurrently (default: all of them at once).
    [int]$Throttle = 0,
    # Collect coverage over the latest (primary) suite and run the coverage gate (mirrors CI). The
    # gate floors track the current build, so this always uses the latest version regardless of -Version.
    [switch]$Coverage
)

$ErrorActionPreference = 'Stop'

# Runs the test suite per game version, each version's projects in parallel. The mods stay
# single-target; legacy versions are tested by building the test projects with -p:Legacy=true and
# selecting that version's TFM. Each project's build auto-provisions its game version on demand
# (see Directory.Build.props), so a clean checkout just works.

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

# version -> game TFM. net10 (1.22) is the current/primary; the rest are legacy (need -p:Legacy=true).
$tfms = [ordered]@{ '1.22' = 'net10.0'; '1.21' = 'net8.0'; '1.20' = 'net7.0' }
$projects = @('ExpandedLib.Tests', 'PipesAndPowerExpanded.Tests', 'SteelmakingExpanded.Tests', 'Integration.Tests')

$wanted = switch ($Version) {
    'latest' { @('1.22') }
    'all' { @($tfms.Keys) }
    default { @($Version) }
}

# Pick the dotnet host. Use the system one if it already has every runtime major we need; otherwise
# provision a self-contained .dotnet and use ITS muxer - the global muxer ignores DOTNET_ROOT, so a
# local muxer is the only reliable way to run on locally-installed runtimes (verified). This is what
# lets a fresh clone without .NET 7/8 installed still run the legacy suites.
$majors = @{ '1.22' = '10'; '1.21' = '8'; '1.20' = '7' }
$needed = @($wanted | ForEach-Object { $majors[$_] } | Select-Object -Unique)
# Empty when dotnet isn't installed at all - that just means every runtime is "missing" and we
# bootstrap the whole .NET (SDK + runtimes) into .dotnet, so even a machine with no dotnet works.
$sysRuntimes = try { (& dotnet --list-runtimes 2>$null) -join "`n" } catch { '' }
$missing = @($needed | Where-Object { $sysRuntimes -notmatch "Microsoft\.NETCore\.App $([regex]::Escape($_))\." })
$dotnet = 'dotnet'
if ($missing.Count -gt 0) {
    Write-Host "Missing .NET runtime major(s) system-wide: $($missing -join ', ') - provisioning a local .dotnet..."
    & (Join-Path $PSScriptRoot 'provision-dotnet.ps1') -Version $Version
    $dotnet = Join-Path $repoRoot ('.dotnet/dotnet' + ($(if ([System.OperatingSystem]::IsWindows()) { '.exe' } else { '' })))
}
Write-Host "Using dotnet host: $dotnet"

# Coverage gate (mirrors .github/workflows/tests.yml): collect cobertura over the whole solution on the
# current TFM and ratchet against scripts/coverage_gate.py. Needs the dotnet-coverage tool + Python.
if ($Coverage) {
    $toolsDir = Join-Path $repoRoot '.dotnet/tools'
    & $dotnet tool install dotnet-coverage --tool-path $toolsDir 2>$null | Out-Null  # no-op if present
    $dc = Join-Path $toolsDir ('dotnet-coverage' + ($(if ([System.OperatingSystem]::IsWindows()) { '.exe' } else { '' })))
    $cov = Join-Path $repoRoot 'coverage.xml'
    Write-Host "Collecting coverage over the latest suite..."
    & $dc collect -f cobertura -o $cov "$dotnet test `"$(Join-Path $repoRoot 'VintageStory.sln')`" -c Debug --nologo"
    if ($LASTEXITCODE -ne 0) { throw "Coverage collection failed." }
    $py = (Get-Command python -ErrorAction SilentlyContinue) ?? (Get-Command python3 -ErrorAction SilentlyContinue)
    if (-not $py) { throw "Python is required for the coverage gate but was not found (coverage.xml was still written)." }
    & $py.Source (Join-Path $repoRoot 'scripts/coverage_gate.py') $cov
    if ($LASTEXITCODE -ne 0) { throw "Coverage gate failed." }
    Write-Host "Coverage gate passed."
    return
}

# One unit of work per (version, project).
$work = foreach ($v in $wanted) {
    foreach ($p in $projects) {
        $legacy = $tfms[$v] -ne 'net10.0'  # legacy TFMs need the multi-target opt-in
        [pscustomobject]@{
            Version = $v
            Tfm     = $tfms[$v]
            Project = $p
            Proj    = (Join-Path $repoRoot "test/$p/$p.csproj")
            Legacy  = $legacy
        }
    }
}
if ($Throttle -le 0) { $Throttle = $work.Count }

# Build phase, SERIAL: the test projects share the mod projects (exlib/ppex/smex), so building them
# concurrently would race on the same intermediate DLLs (CS2012). Building here also auto-provisions
# each version's game binaries once, up front. The test phase then runs in parallel with --no-build.
Write-Host "Building $($work.Count) test target(s) across version(s): $($wanted -join ', ')"
$built = foreach ($item in $work) {
    $args = @('build', $item.Proj, '-f', $item.Tfm, '--nologo', '-v', 'q')
    if ($item.Legacy) { $args += '-p:Legacy=true' }
    & $dotnet @args | Out-Null
    $item | Add-Member -NotePropertyName BuildOk -NotePropertyValue ($LASTEXITCODE -eq 0) -PassThru
}

Write-Host "Running tests in parallel..."
$results = $built | ForEach-Object -ThrottleLimit $Throttle -Parallel {
    $dotnet = $using:dotnet
    $item = $_
    if (-not $item.BuildOk) {
        return [pscustomobject]@{ Name = "$($item.Version)/$($item.Project)"; Ok = $false; Line = 'build failed' }
    }
    $args = @('test', $item.Proj, '-f', $item.Tfm, '--no-build', '--nologo')
    if ($item.Legacy) { $args += '-p:Legacy=true' }
    $out = & $dotnet @args 2>&1
    [pscustomobject]@{
        Name = "$($item.Version)/$($item.Project)"
        Ok   = ($LASTEXITCODE -eq 0)
        Line = ($out | Select-String -Pattern 'Passed!|Failed!|error' | Select-Object -Last 1)
    }
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
