# The source tree: everything that reads, rewrites or compiles it without running the game.
#
#   exmod build     compile
#   exmod test      the suites, one lane per game version
#   exmod format    CSharpier, then dotnet format
#   exmod verify    the shipped assets, checked the way the game loads them
#   exmod codes     regenerate a mod's block-code table
#   exmod check     all of the above in one pass - the gate
#   exmod clean     delete build output

#region build

# Get-ExmodTestProjects and Get-ExmodBuildTargets live in exmod.ps1: they read the manifest, so
# every stage that needs "every test project" or "every mod/sample" shares the one resolver.

# The directories holding the manifest's mods and samples - what format, clean and check treat as
# this repo's own source, as opposed to infra/ and templates/, which stay literal: they are tool
# conventions, true of every repo this CLI runs in, not something exmod.json names. A mod or sample's
# own parent (mods/, samples/) is walked whole rather than just its own path, so a test project
# sitting beside it (mods/<mod>/tests, samples/<Sample>.Tests) is covered too.
function Get-ExmodSourceRoots {
  $paths = @((Get-ExmodMods).Values.Path) + @((Get-ExmodSamples).Values.Path)
  return @($paths | ForEach-Object { Split-Path $_ -Parent } | Select-Object -Unique)
}

# Compiles the mod projects (and, with -Tests, the test projects) for one or more game series.
# Builds are always serial, across mods and across series: the projects share intermediate
# assemblies (exlib -> iiex -> siex, and every test project against its mod), so building two of
# them at once races on the same obj/ files and fails with CS2012. Warnings are never suppressed -
# a warning count is the point of this command - so nothing here passes -clp:ErrorsOnly or -v q.
function Invoke-Build([string[]]$Argv) {
  $positional = @(Get-Positional $Argv @('-Mod', '-Configuration') @('-Tests'))
  $version = if ($positional.Count -gt 0) { $positional[0] } else { 'latest' }
  $modFilter = Get-Opt $Argv '-Mod' $null
  $configuration = Get-Opt $Argv '-Configuration' 'Debug'
  $withTests = Get-Flag $Argv '-Tests'

  $wanted = Resolve-GameVersions $version
  $dotnet = Resolve-DotnetHost $wanted
  Write-Host "Using dotnet host: $dotnet"

  $targets = Get-ExmodBuildTargets
  $testTargets = Get-ExmodTestProjects
  $sampleIds = @((Get-ExmodSamples).Keys)
  if ($modFilter) {
    $key = $modFilter.ToLowerInvariant()
    if (-not $targets.Contains($key)) { throw "Unknown mod '$modFilter'. Known: $($targets.Keys -join ', ')." }
    $targets = [ordered]@{ $key = $targets[$key] }
    $testTargets = if ($testTargets.Contains($key)) { [ordered]@{ $key = $testTargets[$key] } } else { [ordered]@{} }
  }

  foreach ($v in $wanted) {
    $tfm = $GameTfms[$v]
    Write-Step "Building $v ($tfm, $configuration)"
    foreach ($mod in $targets.Keys) {
      # A sample targets $(CurrentGameTfm) only (see samples/*/*.csproj) - the legacy series buys
      # it nothing, so it is skipped the same way `exmod test` skips it.
      if ($mod -in $sampleIds -and $v -ne $CurrentGameVersion) {
        Write-Host "-- $mod ($v) skipped, targets $($GameTfms[$CurrentGameVersion]) only --"
        continue
      }
      $buildArgs = @('build', $targets[$mod], '-f', $tfm, '-c', $configuration)
      if ($tfm -ne 'net10.0') { $buildArgs += '-p:Legacy=true' }
      Write-Host "-- $mod ($v/$tfm) --"
      & $dotnet @buildArgs
      if ($LASTEXITCODE -ne 0) { throw "Build failed: $mod ($v/$tfm)." }
    }

    if ($withTests) {
      foreach ($mod in $testTargets.Keys) {
        if ($v -notin $testTargets[$mod].Series) {
          Write-Host "-- $($testTargets[$mod].Project) ($v) skipped, targets $($GameTfms[$CurrentGameVersion]) only --"
          continue
        }
        $buildArgs = @('build', $testTargets[$mod].Proj, '-f', $tfm, '-c', $configuration)
        if ($tfm -ne 'net10.0') { $buildArgs += '-p:Legacy=true' }
        Write-Host "-- $($testTargets[$mod].Project) ($v/$tfm) --"
        & $dotnet @buildArgs
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $($testTargets[$mod].Project) ($v/$tfm)." }
      }
    }
  }

  Write-Host "`nBuild succeeded."
}


Add-ExmodCommand -Group source -Name build -Summary 'compile the mods for one or more game series' -Action {
  param([string[]]$Argv) Invoke-Build $Argv
} -Detail @'
exmod build [latest|all|1.22|1.21|1.20] [-Mod <name>] [-Configuration Debug|Release] [-Tests]

Compiles the mod projects, in their dependency order (exlib -> iiex -> siex, then the
HelloExpanded sample), for the requested series. Builds are serial, on purpose: the projects
share intermediate assemblies, and building two of them at once races on the same obj/ files
(CS2012). Warnings are never suppressed - a warning count is exactly what this command is for.

  latest          1.22 only (the default)
  all             every supported series
  -Mod <name>     build just one mod or sample (see exmod.json's mods and samples)
  -Configuration  Debug (the default) or Release
  -Tests          also build the test projects `exmod test` would build, for the same series(es)
'@

#endregion

#region test

# Runs the suite per game version, each version's projects in parallel. The mods stay single-target;
# legacy versions are tested by building the test projects with -p:Legacy=true against that version's
# TFM. Each build auto-provisions its game version on demand (Directory.Build.props), so a clean
# checkout just works.
function Invoke-Test([string[]]$Argv) {
  $positional = @(Get-Positional $Argv @('-Throttle', '-Filter') @('-Coverage'))
  $version = if ($positional.Count -gt 0) { $positional[0] } else { 'latest' }
  $throttle = [int](Get-Opt $Argv '-Throttle' 0)
  $coverage = Get-Flag $Argv '-Coverage'
  # Passed straight to `dotnet test --filter`, so it takes that expression grammar
  # (`FullyQualifiedName~Boiler`, `Name=X|Name=Y`). A bare class name works because the runner treats
  # an unqualified term as a substring match on the fully qualified name.
  $filter = Get-Opt $Argv '-Filter' ''

  # Dependency order and project paths come from Get-ExmodTestProjects, shared with
  # `build -Tests` so the two commands can't drift on what "every test project" means.
  $projects = Get-ExmodTestProjects

  $wanted = Resolve-GameVersions $version
  $dotnet = Resolve-DotnetHost $wanted
  Write-Host "Using dotnet host: $dotnet"

  # Mirrors .github/workflows/tests.yml: collect cobertura over the solution and ratchet against
  # coverage_gate.py. The gate floors track the current build, so this always uses the latest version.
  if ($coverage) {
    $toolsDir = Join-Path $RepoRoot '.dotnet/tools'
    & $dotnet tool install dotnet-coverage --tool-path $toolsDir 2>$null | Out-Null
    $dc = Join-Path $toolsDir "dotnet-coverage$ExeSuffix"
    $cov = Join-Path $RepoRoot 'coverage.xml'
    Write-Host "Collecting coverage over the latest suite..."
    & $dc collect -f cobertura -o $cov "$dotnet test `"$(Get-ExmodSolution)`" -c Debug --nologo"
    if ($LASTEXITCODE -ne 0) { throw "Coverage collection failed." }
    $py = (Get-Command python -ErrorAction SilentlyContinue) ?? (Get-Command python3 -ErrorAction SilentlyContinue)
    if (-not $py) { throw "Python is required for the coverage gate but was not found (coverage.xml was still written)." }
    & $py.Source (Join-Path $RepoRoot 'infra/tools/coverage_gate.py') $cov
    if ($LASTEXITCODE -ne 0) { throw "Coverage gate failed." }
    Write-Host "Coverage gate passed."
    return
  }

  $work = foreach ($v in $wanted) {
    foreach ($mod in $projects.Keys) {
      # A sample or an extra project (samples/HelloExpanded.csproj, infra/tools/ExlibVerify.Tests.csproj)
      # only targets $(CurrentGameTfm) - a legacy matrix buys it nothing, so it is skipped rather
      # than failed on '-f net8.0'/'net7.0'.
      if ($v -notin $projects[$mod].Series) { continue }
      [pscustomobject]@{
        Version = $v
        Tfm     = $GameTfms[$v]
        Project = $projects[$mod].Project
        Proj    = $projects[$mod].Proj
        Legacy  = ($GameTfms[$v] -ne 'net10.0')   # legacy TFMs need the multi-target opt-in
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
    $filter = $using:filter
    $item = $_
    if (-not $item.BuildOk) {
      return [pscustomobject]@{ Name = "$($item.Version)/$($item.Project)"; Ok = $false; Line = 'build failed' }
    }
    $testArgs = @('test', $item.Proj, '-f', $item.Tfm, '--no-build', '--nologo')
    if ($filter) { $testArgs += @('--filter', $filter) }
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
      # Under a filter, a suite holding nothing that matches is the ordinary case - the filter names a
      # class that lives in one project of three - so it reports zero rather than failing. Without one,
      # a missing summary is the failure it looks like: an assembly that fails to load during discovery
      # prints "No test is available in ..." and exits 0 with no summary line, so the run would
      # otherwise read as a blank PASS while every test in the suite had silently vanished.
      if ($filter -and $ok) {
        $line = 'no test matched the filter'
      }
      else {
        $ok = $false
        $line = 'NO TEST SUMMARY - the assembly discovered no tests (a type-load failure during ' +
        'discovery does this and still exits 0).'
      }
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


Add-ExmodCommand -Group source -Name test -Summary 'run the test suites, one lane per game version' -Action {
  param([string[]]$Argv) Invoke-Test $Argv
} -Detail @'
exmod test [latest|all|1.22|1.21|1.20] [-Filter <expr>] [-Throttle <n>] [-Coverage]

Builds every test project for the requested series, then runs the suites in parallel - one lane per
(series, project). Missing runtimes and game binaries are provisioned on demand, so a fresh clone
needs nothing installed first. Builds are serial on purpose: the test projects share the mod
projects and building them at once races on the same intermediate assemblies.

  latest      1.22 only (the default)
  all         every supported series
  -Filter     handed to dotnet test --filter, so it takes that grammar: FullyQualifiedName~Boiler,
              Name=X|Name=Y, or a bare class name (an unqualified term is a substring match)
  -Throttle   lanes to run at once; the default is all of them
  -Coverage   instead of the lanes, collect cobertura over the solution and ratchet it against
              infra/test/coverage-floors.json - the same gate CI runs
'@

#endregion

#region format

# Formats every C# file under mods/ and infra/ in two passes, and the order is load-bearing. CSharpier
# wraps lines to the printWidth in .csharpierrc but always emits Allman braces and cannot be
# configured; dotnet format then applies .editorconfig, which moves the braces onto the same line.
# Running the pair is idempotent. Running CSharpier alone afterwards would undo the brace style.
#
# `csharpier check` exits 1 on the finished result, so -Check formats and compares against git rather
# than using the tool's own check mode.
# The formatter, installed into the checkout's own tool folder when the machine has none - the same
# bootstrap scripts/exmod.sh does for pwsh, and for the same reason: a contributor should not have to
# install anything by hand before the format gate will run.
function Resolve-CSharpier() {
  $onPath = Get-Command csharpier -ErrorAction SilentlyContinue
  if ($onPath) { return $onPath.Source }

  $toolsDir = Join-Path $RepoRoot '.dotnet/tools'
  $local = Join-Path $toolsDir "csharpier$ExeSuffix"
  if (Test-Path $local) { return $local }

  Write-Host 'CSharpier not found - installing it into .dotnet/tools ...'
  dotnet tool install csharpier --tool-path $toolsDir | Out-Null
  if (-not (Test-Path $local)) { throw 'Could not install CSharpier into .dotnet/tools.' }
  return $local
}

function Invoke-Format([string[]]$Argv) {
  $check = Get-Flag $Argv '-Check'
  $dirs = @(Get-ExmodSourceRoots) + @('infra')
  Push-Location $RepoRoot
  try {
    if ($check -and (git status --porcelain -- @dirs)) {
      Write-Host "the mod/sample directories or infra/ have uncommitted changes - -Check needs a clean tree." -ForegroundColor Red
      exit 1
    }

    & (Resolve-CSharpier) format @dirs
    if ($LASTEXITCODE -ne 0) { throw "csharpier exited $LASTEXITCODE" }

    foreach ($dir in $dirs) {
      dotnet format whitespace $dir --folder
      if ($LASTEXITCODE -ne 0) { throw "dotnet format exited $LASTEXITCODE on $dir" }
    }

    if ($check) {
      if (git status --porcelain -- @dirs) {
        Write-Host "`nThese files are not formatted:" -ForegroundColor Red
        git diff --name-only -- @dirs | ForEach-Object { Write-Host "  $_" }
        Write-Host "`nRun scripts/exmod format and commit the result." -ForegroundColor Yellow
        exit 1
      }
      Write-Host "Formatting is clean." -ForegroundColor Green
    }
  } finally {
    Pop-Location
  }
}


Add-ExmodCommand -Group source -Name format -Summary 'rewrite with CSharpier, then dotnet format' -Action {
  param([string[]]$Argv) Invoke-Format $Argv
} -Detail @'
exmod format [-Check]

Formats every C# file under every mod and sample directory the manifest names, and under infra/, in
two passes, and the order is load-bearing: CSharpier wraps lines but always emits Allman braces and
cannot be configured out of it, then dotnet format applies .editorconfig and puts the braces back.
The pair is idempotent; CSharpier alone afterwards would undo the brace style.

  -Check   format, then fail if git sees a change. It needs a clean tree and refuses on a dirty
           one, where every finding would be an edit of your own.

CSharpier is installed into .dotnet/tools on first use when the machine has none, the same way the
POSIX launcher bootstraps pwsh.
'@

#endregion

#region verify

# Every mod/sample this repo can verify, as folder-name key -> its build-output source directory
# (mods/<mod>/src, or samples/<Sample> directly) - the same directories Get-BuiltModDirs builds
# from. Keyed by folder name, which is also every mod's own modid here.
function Get-ExmodVerifySources {
  $out = [ordered]@{}
  foreach ($target in (Get-ExmodBuildTargets).GetEnumerator()) {
    $out[$target.Key] = Split-Path $target.Value -Parent
  }
  return $out
}

# Runs infra/tools/ExlibVerify - the headless check of shipped assets (patch application, recipe
# codes, handbook lang coverage) - over one or more built mods, with no game running. Each named
# mod is checked in its own run, with every OTHER named mod's built folder passed as --mods, so a
# cross-mod patch (e.g. iiex patching into exlib's or the game's own assets) still resolves
# against its real target the way it would when both are loaded together.
function Invoke-Verify([string[]]$Argv) {
  $modsCsv = Get-Opt $Argv '-Mod' $null
  $version = Get-Opt $Argv '-Version' $CurrentGameVersion
  $configuration = Get-Opt $Argv '-Configuration' 'Debug'
  $strict = Get-Flag $Argv '-Strict'
  $json = Get-Flag $Argv '-Json'
  if (-not $GameTfms.Contains($version)) {
    throw "Unknown version '$version'. Use one of: $($GameTfms.Keys -join ', ')."
  }

  $sources = Get-ExmodVerifySources
  $names = if ($modsCsv) {
    @($modsCsv -split ',' | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
  } else {
    @($sources.Keys)
  }
  foreach ($n in $names) {
    if (-not $sources.Contains($n)) { throw "Unknown mod '$n'. Known: $($sources.Keys -join ', ')." }
  }

  # Get-BuiltModDirs already builds anything missing when it is asked for every mod; a named
  # subset gets the same on-demand build here, one at a time, so `exmod verify -Mod exlib` on a
  # fresh clone works without building iiex/siex first.
  $built = [ordered]@{}
  foreach ($n in $names) {
    $srcDir = $sources[$n]
    $dir = Join-Path $srcDir "bin/$configuration/Mods/mod"
    if (-not (Test-Path (Join-Path $dir 'modinfo.json'))) {
      $csproj = Get-ChildItem $srcDir -Filter '*.csproj' -File | Select-Object -First 1
      if (-not $csproj) { throw "No .csproj under $srcDir to build." }
      Write-Host "Building $n (not yet built) ..."
      dotnet build $csproj.FullName -c $configuration -clp:ErrorsOnly
      if ($LASTEXITCODE -ne 0) { throw "Build of $csproj failed." }
    }
    $built[$n] = $dir
  }

  $game = Resolve-GameInstall $version 'server'
  $exlibVerifyProj = Join-Path $RepoRoot 'infra/tools/ExlibVerify/ExlibVerify.csproj'

  $results = @()
  foreach ($n in $names) {
    $others = @($built.Keys | Where-Object { $_ -ne $n } | ForEach-Object { $built[$_] })
    $toolArgs = @($built[$n], '--game', $game)
    if ($others) { $toolArgs += @('--mods') + $others }
    if ($strict) { $toolArgs += '--strict' }
    if ($json) { $toolArgs += '--json' }

    Write-Host "`n-- $n --"
    dotnet run --project $exlibVerifyProj -- @toolArgs
    $exit = $LASTEXITCODE
    $results += [pscustomobject]@{ Name = $n; Ok = ($exit -eq 0); Exit = $exit }
  }

  Write-Host ""
  Write-Host "===== Results ====="
  foreach ($r in $results) {
    $tag = if ($r.Ok) { 'PASS' } else { 'FAIL' }
    Write-Host ("{0}  {1} (exit {2})" -f $tag, $r.Name, $r.Exit)
  }

  $failed = @($results | Where-Object { -not $_.Ok })
  if ($failed) { throw "$($failed.Count) mod(s) failed verification: $($failed.Name -join ', ')" }
  Write-Host "All $($results.Count) mod(s) verified."
}


Add-ExmodCommand -Group source -Name verify -Summary 'headless-check shipped assets, no game running' -Action {
  param([string[]]$Argv) Invoke-Verify $Argv
} -Detail @'
exmod verify [-Mod <name>[,<name>...]] [-Version <x.y>] [-Configuration Debug] [-Strict] [-Json]

Runs infra/tools/ExlibVerify over the built mods: does every patch apply against its real target,
does every recipe/handbook code resolve, all without the game running. Builds anything not yet
built first. Each mod is checked in its own run, against a --game install, with every other named
mod's built folder passed as --mods so a cross-mod patch still resolves the way it would with both
mods loaded together.

  -Mod            one or more mods/samples to check, comma-separated (default: every mod built)
  -Version <x.y>  the game series to check against (default: 1.22)
  -Configuration  the build configuration to read (default: Debug)
  -Strict         an informational finding fails the run too, not only an error
  -Json           ExlibVerify's own --json report instead of its plain-text one
'@

#endregion

#region codes

# Regenerates one mod's `{Mod}Blocks.g.cs` code-code table from its code-first block definitions,
# through the standalone infra/tools/BlockCodeEmitter console tool. Builds the mod first (so the
# emitter reads today's definitions), writes the table, then rebuilds so a change that no longer
# compiles is caught here rather than by the next `exmod test`.
function Invoke-Codes([string[]]$Argv) {
  $projects = @{
    exlib = 'mods/exlib/src/ExpandedLib.csproj'
    iiex  = 'mods/iiex/src/IronIndustryExpanded.csproj'
    siex  = 'mods/siex/src/SteelIndustryExpanded.csproj'
  }
  $mod = if ($Argv.Count -gt 0) { $Argv[0] } else { $null }
  if (-not $mod -or -not $projects.Contains($mod)) {
    throw "codes needs a mod: one of $($projects.Keys -join ', ')."
  }

  Push-Location $RepoRoot
  try {
    Write-Host "Building $mod..."
    dotnet build $projects[$mod] -clp:ErrorsOnly
    if ($LASTEXITCODE -ne 0) { throw "Build of $mod failed." }

    Write-Host "Regenerating $mod's block-code table..."
    dotnet run --project infra/tools/BlockCodeEmitter/BlockCodeEmitter.csproj -- $mod
    if ($LASTEXITCODE -ne 0) { throw "BlockCodeEmitter failed for $mod." }

    Write-Host "Rebuilding $mod against the regenerated table..."
    dotnet build $projects[$mod] -clp:ErrorsOnly
    if ($LASTEXITCODE -ne 0) { throw "Rebuild of $mod failed after regenerating its block codes." }
  } finally {
    Pop-Location
  }
}


Add-ExmodCommand -Group source -Name codes -Summary "regenerate a mod's block-code table" -Action {
  param([string[]]$Argv) Invoke-Codes $Argv
} -Detail @'
exmod codes <exlib|iiex|siex>

Regenerates one mod's {Mod}Blocks.g.cs from its code-first block definitions, through
infra/tools/BlockCodeEmitter. Builds the mod so the emitter reads today's definitions, writes the
table, then rebuilds - so a table that no longer compiles fails here rather than in the next test
run.
'@

#endregion

#region check

# The gate: format, build, verify, test, in that order, stopping at the first failure. `format
# -Check` runs in a child pwsh (its own -Check fails by calling `exit`, which would otherwise take
# this whole process down with it before the summary below ever printed) so its result is read
# back the ordinary way, from $LASTEXITCODE.
function Invoke-Check([string[]]$Argv) {
  $positional = @(Get-Positional $Argv @() @('-Coverage', '-NoFormat'))
  $version = if ($positional.Count -gt 0) { $positional[0] } else { 'latest' }
  $coverage = Get-Flag $Argv '-Coverage'
  $noFormat = Get-Flag $Argv '-NoFormat'

  $results = [ordered]@{ format = 'PENDING'; build = 'PENDING'; verify = 'PENDING'; test = 'PENDING' }
  $stop = $false

  Write-Step 'format'
  if ($noFormat) {
    Write-Host "Skipped: -NoFormat was given."
    $results.format = 'SKIPPED'
  }
  elseif (git -C $RepoRoot status --porcelain -- @(Get-ExmodSourceRoots) infra) {
    Write-Host ("Skipped: the mod/sample directories or infra/ have uncommitted changes - the format " +
      "gate compares against git, so on a dirty tree every finding would be one of this run's own edits.")
    $results.format = 'SKIPPED'
  }
  else {
    $pwshExe = Join-Path $PSHOME "pwsh$ExeSuffix"
    & $pwshExe -NoProfile -File (Join-Path $RepoRoot 'scripts/exmod.ps1') format -Check
    if ($LASTEXITCODE -eq 0) { $results.format = 'PASS' } else { $results.format = 'FAIL'; $stop = $true }
  }

  if (-not $stop) {
    Write-Step 'build'
    try { Invoke-Build @($version); $results.build = 'PASS' }
    catch { Write-Host $_.Exception.Message -ForegroundColor Red; $results.build = 'FAIL'; $stop = $true }
  } else { $results.build = 'SKIPPED' }

  if (-not $stop) {
    Write-Step 'verify'
    try { Invoke-Verify @(); $results.verify = 'PASS' }
    catch { Write-Host $_.Exception.Message -ForegroundColor Red; $results.verify = 'FAIL'; $stop = $true }
  } else { $results.verify = 'SKIPPED' }

  if (-not $stop) {
    Write-Step 'test'
    $testArgv = @($version)
    if ($coverage) { $testArgv += '-Coverage' }
    try { Invoke-Test $testArgv; $results.test = 'PASS' }
    catch { Write-Host $_.Exception.Message -ForegroundColor Red; $results.test = 'FAIL'; $stop = $true }
  } else { $results.test = 'SKIPPED' }

  Write-Host ''
  Write-Host '===== check summary =====' -ForegroundColor Cyan
  foreach ($step in $results.Keys) { Write-Host ("{0,-8} {1}" -f $step, $results[$step]) }

  if ($results.Values -contains 'FAIL') { throw "exmod check failed." }
  Write-Host "`nAll steps passed." -ForegroundColor Green
}


Add-ExmodCommand -Group source -Name check -Summary 'the gate: format, build, verify, test' -Action {
  param([string[]]$Argv) Invoke-Check $Argv
} -Detail @'
exmod check [latest|all|1.22|1.21|1.20] [-Coverage] [-NoFormat]

One command that answers "is this tree good". Runs format -Check, build, verify, then test, in
that order, stopping at the first failing step. Ends with a PASS/FAIL/SKIPPED summary of every
step and exits nonzero if any of them did.

  latest      1.22 only (the default), for the build and test steps
  all         every supported series, for the build and test steps
  -Coverage   passed through to the test step, in place of its own per-version lanes
  -NoFormat   skip the format step outright. It is also skipped, for a different reason, when
              mods/ or infra/ already has uncommitted changes - the format gate compares against
              git, so on a dirty tree every finding would be one of your own edits, not a real one.
'@

#endregion

#region clean

# Every directory literally named bin or obj under $Roots (each absolute or repo-relative), at any
# depth, collected before any deletion happens and sorted shallowest-first - so removing a parent
# (mods/exlib/src/obj) doesn't leave a later Remove-Item call finding nothing left where one of its
# own children used to be.
function Get-ExmodCleanTargets([string[]]$Roots) {
  $found = @()
  foreach ($root in $Roots) {
    $base = if ([System.IO.Path]::IsPathRooted($root)) { $root } else { Join-Path $RepoRoot $root }
    if (-not (Test-Path $base)) { continue }
    $found += Get-ChildItem $base -Recurse -Directory -Force |
      Where-Object { $_.Name -in @('bin', 'obj') }
  }
  return @($found | Sort-Object { $_.FullName.Length })
}

# Deletes build output. Never touches .game/, .dotnet/ or .compat/: those are the expensive
# downloads `exmod provision` fetches, and `exmod provision -Force` is how they get replaced -
# not this command.
function Invoke-Clean([string[]]$Argv) {
  $deep = Get-Flag $Argv '-Deep'
  $removed = 0

  foreach ($dir in Get-ExmodCleanTargets (@(Get-ExmodSourceRoots) + @('infra', 'templates'))) {
    if (-not (Test-Path $dir.FullName)) { continue }   # a parent already removed this one
    Write-Host "Removing $($dir.FullName)"
    Remove-Item -Recurse -Force $dir.FullName
    $removed++
  }

  $extras = @((Join-Path $RepoRoot 'TestResults'))
  $extras += @(Get-ChildItem $RepoRoot -Filter 'coverage.*' -File -ErrorAction SilentlyContinue |
    ForEach-Object { $_.FullName })
  if ($deep) {
    # Never Saves/, Backups/, BackupSaves/ or Playerdata/ under .gamedata - those hold the owner's
    # worlds. Cache/ and Logs/ are the only .gamedata folders that are pure churn.
    $extras += @(
      (Join-Path $RepoRoot 'dist/Releases'),
      (Join-Path $RepoRoot 'dist/nuget'),
      (Join-Path $RepoRoot 'dist/assets'),
      (Join-Path $RepoRoot '.gamedata/Cache'),
      (Join-Path $RepoRoot '.gamedata/Logs')
    )
  }
  foreach ($path in $extras) {
    if (-not (Test-Path $path)) { continue }
    Write-Host "Removing $path"
    Remove-Item -Recurse -Force $path
    $removed++
  }

  Write-Host "`nRemoved $removed path(s)."
}


Add-ExmodCommand -Group source -Name clean -Summary 'delete build output (bin/obj, TestResults)' -Action {
  param([string[]]$Argv) Invoke-Clean $Argv
} -Detail @'
exmod clean [-Deep]

Deletes build output: every bin/ and obj/ under mods/, samples/, infra/ and templates/, plus the
root-level TestResults/ folder and any coverage.* file. Never touches .game/, .dotnet/ or
.compat/ - those are expensive downloads, and `exmod provision -Force` is how they get replaced,
not this command.

  -Deep   also remove dist/Releases, dist/nuget, dist/assets, and .gamedata/Cache and
          .gamedata/Logs. Never Saves/, Backups/, BackupSaves/ or Playerdata/ under .gamedata -
          those hold the owner's worlds.
'@

#endregion
