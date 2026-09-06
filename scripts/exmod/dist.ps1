# Packaging and release: turning the checkout into the files people download.
#
#   exmod pack      every mod, every game version, zipped
#   exmod bundle    the developer bundle: test harness, generators, docs
#   exmod nuget     the NuGet packages
#   exmod release   check that a version is ready to tag

#region cake

# infra/CakeBuild is a console project, so its targets run through `dotnet run` and take their
# arguments after the `--`. It resolves everything relative to its own directory (RunWorkingDirectory
# in CakeBuild.csproj), which is why nothing here passes paths to it.
function Invoke-CakeTarget([string]$Target, [string]$Configuration) {
  Push-Location $RepoRoot
  try {
    $cakeArgs = @('run', '--project', 'infra/CakeBuild', '--')
    if ($Target) { $cakeArgs += "--target=$Target" }
    if ($Configuration) { $cakeArgs += "--configuration=$Configuration" }
    dotnet @cakeArgs
    if ($LASTEXITCODE -ne 0) {
      throw "Cake target '$(if ($Target) { $Target } else { 'Default' })' failed."
    }
  } finally {
    Pop-Location
  }
}

#endregion

#region pack

# Every mod, built for every supported game version and zipped into dist/Releases/<gameVersion>/.
# Cake rebuilds from scratch (it wipes each mod's bin first), so this is minutes, not seconds.
function Invoke-Pack([string[]]$Argv) {
  $configuration = Get-Opt $Argv '-Configuration' 'Release'
  $all = Get-Flag $Argv '-All'

  Write-Step "Mod zips ($configuration)"
  Invoke-CakeTarget '' $configuration

  if ($all) {
    Invoke-Bundle $Argv
    Invoke-Nuget $Argv
  }

  $releases = Join-Path $RepoRoot 'dist/Releases'
  $zips = @(Get-ChildItem $releases -Recurse -Filter '*.zip' -ErrorAction SilentlyContinue)
  Write-Host ''
  Write-Host "$($zips.Count) archive(s) in dist/Releases:" -ForegroundColor Green
  foreach ($z in $zips) {
    Write-Host ('  {0}  {1:N0} KB' -f $z.FullName.Substring($releases.Length + 1), ($z.Length / 1KB))
  }
}

Add-ExmodCommand -Group package -Name pack -Summary 'zip every mod for every game version' -Action {
  param([string[]]$Argv) Invoke-Pack $Argv
} -Detail @'
exmod pack [-Configuration Release|Debug] [-All]

Builds every mod for every supported game version and zips one archive per (game version, mod) into
dist/Releases/<gameVersion>/. The archives carry the publish output, the per-version assets, the
mod icon, a modinfo whose game dependency is rewritten to that version, and LICENSE.txt.

Assets are taken from the publish output rather than from the source tree, because each game
version filters its own (a legacy-only patch whose codes do not resolve on a newer version is
removed there and nowhere else).

  -All   also build the developer bundle and the NuGet packages, which is what a release needs
'@

#endregion

#region bundle

# The harness dev bundle: ExpandedLib.Testing.dll, the exlib.dll it compiles against, the XML docs
# and the source generators. Built for the current game version only.
function Invoke-Bundle([string[]]$Argv) {
  $configuration = Get-Opt $Argv '-Configuration' 'Release'
  Write-Step "Developer bundle ($configuration)"
  Invoke-CakeTarget 'PackageTesting' $configuration
}

Add-ExmodCommand -Group package -Name bundle -Summary 'the developer bundle: harness and generators' -Action {
  param([string[]]$Argv) Invoke-Bundle $Argv
} -Detail @'
exmod bundle [-Configuration Release|Debug]

Builds the developer bundle - exlib-testing_<version>.zip - and leaves it beside the mod zips in
dist/Releases/<gameVersion>/. It holds ExpandedLib.Testing.dll, the exlib.dll it compiles against,
both XML documentation files, the source generators under analyzers/, LICENSE.txt and a README
explaining how to reference them from a test project outside this repo.

The harness is a developer library rather than a game mod, so it is not a mod zip. It is built for
the current game version only; on 1.21 and 1.20 a consumer references it from source.
'@

#endregion

#region nuget

# The four packable projects. Versions come from exlib's modinfo.json (see each csproj), so a
# release bumps one number and all four follow.
function Invoke-Nuget([string[]]$Argv) {
  $configuration = Get-Opt $Argv '-Configuration' 'Release'
  $out = Get-Opt $Argv '-Out' 'dist/nuget'
  $outFull = if ([System.IO.Path]::IsPathRooted($out)) { $out } else { Join-Path $RepoRoot $out }

  $projects = @(
    'mods/exlib/src/ExpandedLib.csproj',
    'mods/exlib/industry/ExpandedLib.Industry.csproj',
    'mods/exlib/testing/ExpandedLib.Testing.csproj',
    'infra/tools/ExlibVerify/ExlibVerify.csproj'
  )

  Write-Step "NuGet packages ($configuration) -> $out"
  Push-Location $RepoRoot
  try {
    foreach ($p in $projects) {
      dotnet pack $p -c $configuration -o $outFull
      if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed for $p." }
    }
  } finally {
    Pop-Location
  }

  foreach ($n in Get-ChildItem $outFull -Filter '*.nupkg' -ErrorAction SilentlyContinue) {
    Write-Host "  $($n.Name)"
  }
}

Add-ExmodCommand -Group package -Name nuget -Summary 'the NuGet packages' -Action {
  param([string[]]$Argv) Invoke-Nuget $Argv
} -Detail @'
exmod nuget [-Configuration Release|Debug] [-Out <path>]

Packs the four projects that ship on NuGet into dist/nuget (or -Out): ExpandedLib, its family
domain layer ExpandedLib.Industry, its test harness ExpandedLib.Testing, and ExpandedLib.Verify -
the last as a .NET tool, so a consumer gets `exlib-verify` with no game licence, no built mod and
no test runner.

All four read their version from mods/exlib/src/modinfo.json, so a release bumps one number.
Nothing here pushes to NuGet.org: that is a separate decision, and the step in release.yml that
would do it is present and commented out.
'@

#endregion

#region release

# Reads the newest released heading of a keep-a-changelog file: the first "## [x.y.z]" after
# "## [Unreleased]". Returns $null when the file has no released entry at all.
function Get-ChangelogVersion([string]$Path) {
  if (-not (Test-Path $Path)) { return $null }
  foreach ($line in Get-Content $Path) {
    $m = [regex]::Match($line, '^##\s*\[(\d+\.\d+\.\d+)\]')
    if ($m.Success) { return $m.Groups[1].Value }
  }
  return $null
}

# Every mod in the checkout as its modinfo record plus the paths a release cares about.
function Get-ModManifests() {
  $out = @()
  foreach ($modDir in Get-ChildItem (Join-Path $RepoRoot 'mods') -Directory | Sort-Object Name) {
    $modinfoPath = Join-Path $modDir.FullName 'src/modinfo.json'
    if (-not (Test-Path $modinfoPath)) { continue }
    $modinfo = Get-Content $modinfoPath -Raw | ConvertFrom-Json
    $out += [pscustomobject]@{
      Folder    = $modDir.Name
      ModId     = $modinfo.modid
      Version   = $modinfo.version
      Depends   = $modinfo.dependencies
      Changelog = Join-Path $modDir.FullName 'CHANGELOG.md'
    }
  }
  return $out
}

# The published versions on the mod DB, or $null when the lookup fails. Read-only, and only when
# -Online asks: the command has to work on a machine with no network.
function Get-PublishedVersions([string]$ModId) {
  try {
    $record = Invoke-RestMethod -Uri "https://mods.vintagestory.at/api/mod/$ModId" -TimeoutSec 15
    if (-not $record.mod.releases) { return @() }
    return @($record.mod.releases | ForEach-Object { $_.modversion })
  } catch {
    return $null
  }
}

# Checks a release is ready to tag, and prints the commands that would do it. This command never
# writes: not the working tree, not a tag, not the remote. Tagging stays a deliberate act.
function Invoke-Release([string[]]$Argv) {
  $online = Get-Flag $Argv '-Online'
  $wantVersion = Get-Opt $Argv '-Version'
  $modFilter = Get-Opt $Argv '-Mod'

  $checks = [System.Collections.Generic.List[object]]::new()
  function Add-Check([string]$Name, [string]$Status, [string]$Detail) {
    $checks.Add([pscustomobject]@{ Name = $Name; Status = $Status; Detail = $Detail })
  }

  Push-Location $RepoRoot
  try {
    $mods = @(Get-ModManifests)
    if ($modFilter) {
      $mods = @($mods | Where-Object { $_.Folder -eq $modFilter -or $_.ModId -eq $modFilter })
      if (-not $mods) { throw "No mod '$modFilter' under mods/." }
    }
    $byId = @{}
    foreach ($m in Get-ModManifests) { $byId[$m.ModId] = $m }

    Write-Step 'Versions'
    foreach ($m in $mods) {
      Write-Host ('  {0,-6} {1,-8} {2}' -f $m.ModId, $m.Version, $m.Changelog.Substring($RepoRoot.Length + 1))
    }

    $dirty = @(git status --porcelain)
    if ($dirty.Count -eq 0) {
      Add-Check 'working tree' 'PASS' 'clean'
    } else {
      Add-Check 'working tree' 'FAIL' "$($dirty.Count) uncommitted change(s); a tag would not describe what ships"
    }

    $branch = (git rev-parse --abbrev-ref HEAD).Trim()
    Add-Check 'branch' 'INFO' $branch

    # The changelog's newest released heading is what a player reads to learn what this version is.
    # A modinfo ahead of it means the entry was never written, or was left under Unreleased.
    foreach ($m in $mods) {
      $logged = Get-ChangelogVersion $m.Changelog
      if ($logged -eq $m.Version) {
        Add-Check "changelog ($($m.ModId))" 'PASS' "newest entry is $logged"
      } elseif (-not $logged) {
        Add-Check "changelog ($($m.ModId))" 'FAIL' 'no released entry at all'
      } else {
        Add-Check "changelog ($($m.ModId))" 'FAIL' "modinfo says $($m.Version), newest entry is $logged"
      }
    }

    # A modinfo dependency is a MINIMUM the game checks, not a pin, so a stale one passes the load
    # check and then fails at runtime against the sibling that actually shipped.
    foreach ($m in $mods) {
      if (-not $m.Depends) { continue }
      foreach ($dep in $m.Depends.PSObject.Properties) {
        if (-not $byId.ContainsKey($dep.Name)) { continue }
        $have = $byId[$dep.Name].Version
        if ($dep.Value -eq $have) {
          Add-Check "depends ($($m.ModId) -> $($dep.Name))" 'PASS' $dep.Value
        } else {
          Add-Check "depends ($($m.ModId) -> $($dep.Name))" 'FAIL' "declares $($dep.Value), sibling is at $have"
        }
      }
    }

    $version = if ($wantVersion) { $wantVersion } else { ($mods | Select-Object -First 1).Version }
    $tag = "v$version"
    $existing = @(git tag -l $tag) + @(git tag -l $version)
    if ($existing.Count -eq 0) {
      Add-Check "tag $tag" 'PASS' 'free'
    } else {
      Add-Check "tag $tag" 'FAIL' "already exists: $($existing -join ', ')"
    }

    # The block-code manifest is the migration contract for worlds built on an earlier release. It is
    # regenerated from dist/Releases AFTER packing, and dist/Releases keeps only the newest zip per
    # mod, so a release that is packed and never recorded loses its codes for good. Each mod's seed
    # records the last version that made it in, under the mod id that shipped it - iiex and siex
    # succeeded ppex and smex, so their history is filed under the old ids.
    foreach ($modDir in Get-ChildItem (Join-Path $RepoRoot 'mods') -Directory | Sort-Object Name) {
      $seed = Join-Path $modDir.FullName 'tests/ReleasedHistorySeed.cs'
      if (-not (Test-Path $seed)) { continue }
      $text = Get-Content $seed -Raw
      foreach ($m in [regex]::Matches($text, '\["(?<id>[^"]+)"\]\s*=\s*"(?<ver>[^"]+)"')) {
        Add-Check "shipped ($($m.Groups['id'].Value))" 'INFO' "manifest records $($m.Groups['ver'].Value) as the last release"
      }
    }

    $releases = Join-Path $RepoRoot 'dist/Releases'
    $zips = @(Get-ChildItem $releases -Recurse -Filter "*_$version*.zip" -ErrorAction SilentlyContinue)
    if ($zips.Count -gt 0) {
      Add-Check 'archives' 'PASS' "$($zips.Count) zip(s) for $version in dist/Releases"
    } else {
      Add-Check 'archives' 'WARN' "nothing for $version in dist/Releases; run exmod pack -All"
    }

    if ($online) {
      foreach ($m in $mods) {
        $published = Get-PublishedVersions $m.ModId
        if ($null -eq $published) {
          Add-Check "mod DB ($($m.ModId))" 'WARN' 'lookup failed'
        } elseif ($published -contains $m.Version) {
          Add-Check "mod DB ($($m.ModId))" 'WARN' "$($m.Version) is already published"
        } else {
          $newest = if ($published.Count -gt 0) { $published[0] } else { 'nothing' }
          Add-Check "mod DB ($($m.ModId))" 'INFO' "published: $newest; this release would span $($published.Count) version(s) of notes"
        }
      }
    }

    Write-Step 'Checks'
    $width = ($checks | ForEach-Object { $_.Name.Length } | Measure-Object -Maximum).Maximum
    foreach ($c in $checks) {
      $colour = switch ($c.Status) {
        'PASS' { 'Green' }
        'FAIL' { 'Red' }
        'WARN' { 'Yellow' }
        default { 'Gray' }
      }
      Write-Host ('  {0}  {1,-4}  {2}' -f $c.Name.PadRight($width), $c.Status, $c.Detail) -ForegroundColor $colour
    }

    $failed = @($checks | Where-Object { $_.Status -eq 'FAIL' })
    Write-Host ''
    if ($failed.Count -gt 0) {
      Write-Host "$($failed.Count) check(s) failed - not ready to tag." -ForegroundColor Red
      exit 1
    }

    Write-Host "Ready to tag $tag." -ForegroundColor Green
    Write-Host 'Nothing here writes git. To release, run these yourself:'
    Write-Host ''
    Write-Host "  git tag -a $tag -m `"$version`""
    Write-Host "  git push origin $tag"
    Write-Host ''
    Write-Host '.github/workflows/release.yml runs on a v* tag: it tests the tagged commit, builds'
    Write-Host 'the zips, the bundle and the packages, and attaches them to a GitHub release.'
    Write-Host ''
    Write-Host 'Afterwards, run infra/tools/gen-released-codes.py so this release joins the block-code'
    Write-Host 'manifest. dist/Releases keeps only the newest zips, so it is the last chance to record it.'
  } finally {
    Pop-Location
  }
}

Add-ExmodCommand -Group package -Name release -Summary 'check that a version is ready to tag' -Action {
  param([string[]]$Argv) Invoke-Release $Argv
} -Detail @'
exmod release [-Version <x.y.z>] [-Mod <name>] [-Online]

Checks a release is ready and prints the commands that would tag it. It never writes: not the tree,
not a tag, not the remote.

What it checks:
  working tree     clean, so the tag describes what ships
  changelog        each mod's modinfo version has a matching released heading, not one still sitting
                   under Unreleased
  depends          a modinfo dependency on a sibling mod names that sibling's current version. The
                   game treats a dependency as a MINIMUM rather than a pin, so a stale one loads and
                   then fails at runtime - this is the check that catches it before release
  tag              v<version> is free
  shipped          what each mod's ReleasedHistorySeed records as its last shipped version, so it
                   is clear what this release spans. The block-code manifest is regenerated from
                   dist/Releases after packing, and that folder keeps only the newest zips, so a
                   release that is packed and never recorded loses its codes for good
  archives         dist/Releases holds this version

  -Version   the version to check the tag for; defaults to the first mod's modinfo version
  -Mod       check one mod rather than all of them
  -Online    also ask the mod DB what is already published, to see what this release spans
'@

#endregion
