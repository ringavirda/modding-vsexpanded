#!/usr/bin/env pwsh
# exmod - one entry point for every stage of this repo's life: provisioning a fresh clone, building,
# testing, running the game, and packaging a release. Runs on Windows, Linux and macOS under
# PowerShell 7; the platform differences live in $OnWindows branches rather than in a second copy of
# each script that has to be kept in step. exmod.sh is a launcher for POSIX shells, not a second
# implementation - it finds pwsh (bootstrapping it into .dotnet/tools if absent) and forwards here.
#
# This file is the dispatcher: the argument helpers and resolvers every command shares, and the
# registry they register themselves in. The commands live one file per stage under scripts/exmod/ -
# provision, src, run, dist, windows - and each of those opens with the list of commands it owns.
#
#   exmod                   the command list, grouped
#   exmod help <command>    one command in detail
#   exmod -RepoRoot <path> <command>   act on a checkout other than the one containing this script

[CmdletBinding()]
param(
  [Parameter()][string]$RepoRoot,
  [Parameter(Position = 0)][string]$Command,
  [Parameter(Position = 1, ValueFromRemainingArguments = $true)][string[]]$Arguments = @()
)

$ErrorActionPreference = 'Stop'

# The nearest directory above $PWD that holds exmod.json, so exmod works run from any subdirectory
# of a checkout - not only its root - the way git finds .git. Falls back to the wrapper's own parent
# when nothing above $PWD has one (e.g. this script run against a bare template). -RepoRoot skips
# the search and overrides both.
function Get-ExmodRepoRoot([string]$Override) {
  if ($Override) {
    if (-not (Test-Path $Override)) { throw "-RepoRoot path not found: $Override" }
    return (Resolve-Path $Override).Path
  }
  $dir = (Get-Location).Path
  while ($true) {
    if (Test-Path (Join-Path $dir 'exmod.json')) { return $dir }
    $parent = Split-Path $dir -Parent
    if (-not $parent -or $parent -eq $dir) { break }
    $dir = $parent
  }
  return (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

$RepoRoot = Get-ExmodRepoRoot $RepoRoot
$OnWindows = [System.OperatingSystem]::IsWindows()
$ExeSuffix = if ($OnWindows) { '.exe' } else { '' }

# MSBuild worker nodes are not kept alive after a build: on this install idle nodes never exit and
# a day of building left 74 of them holding 11 GB. Directory.Build.rsp says the same for builds
# started outside this script.
$env:MSBUILDDISABLENODEREUSE = '1'
# glibc 2.41 and later refuse to load a shared object that needs an executable stack; MonoMod's
# native helper for the .NET 7 lane (game 1.20) is one, so every Harmony patch there fails without
# this tunable. Harmless on older glibc and on the other lanes.
if (-not $OnWindows) { $env:GLIBC_TUNABLES = 'glibc.rtld.execstack=2' }

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
  # Returned bare. Every call site wraps the result in @(), which is what keeps a none- or one-element
  # result an array; returning `, $out` on top of that nests it, so the caller's [0] is the whole inner
  # array and interpolates as one space-joined string. That reads as a single malformed positional -
  # `exmod test 1.21 -Filter X` reported `Unknown version '1.21 -Filter X'` - and hides until a command
  # is given two positionals, which is why it survived in `test` and `stage` alike.
  return $out
}

function Assert-Windows([string]$What) {
  if (-not $OnWindows) { throw "$What is Windows-only." }
}

# One section header, so a command built out of several steps reads as those steps on the terminal.
function Write-Step([string]$Text) {
  Write-Host ''
  Write-Host "== $Text" -ForegroundColor Cyan
}

#endregion

#region Shared resolvers

# The three supported game series and what each one builds against - vendor knowledge, true in
# every repo that builds against this engine, so it stays here rather than in the manifest. Every
# version-taking command resolves through here, so a new series is added in one place.
$GameTfms = [ordered]@{ '1.22' = 'net10.0'; '1.21' = 'net8.0'; '1.20' = 'net7.0' }
$GameRuntimeMajors = @{ '1.22' = '10'; '1.21' = '8'; '1.20' = '7' }

#region Manifest

# Resolves a manifest-relative path to an absolute one. Fails naming the field and the path it
# named, rather than the missing-file error a downstream Get-ChildItem would give - the manifest
# naming a path that isn't there is a manifest bug, not a build that hasn't happened yet.
function Resolve-ManifestPath([string]$Field, [string]$RelPath) {
  if (-not $RelPath) { throw "exmod.json: '$Field' is required." }
  $full = if ([System.IO.Path]::IsPathRooted($RelPath)) { $RelPath } else { Join-Path $RepoRoot $RelPath }
  if (-not (Test-Path $full)) { throw "exmod.json: '$Field' names a path that does not exist: $RelPath" }
  return (Resolve-Path $full).Path
}

# The single .csproj directly under $Dir, or a naming error when there is none or more than one -
# the convention every mod, sample, test and package entry in the manifest relies on.
function Find-SingleCsproj([string]$Dir, [string]$Field) {
  $found = @(Get-ChildItem $Dir -Filter '*.csproj' -File)
  if ($found.Count -ne 1) {
    throw "exmod.json: '$Field' names $Dir, which holds $($found.Count) .csproj file(s) (want exactly one)."
  }
  return $found[0].FullName
}

# The directory a mod's project resolves under: <path>/src when that folder holds exactly one
# .csproj (the layout every real mod uses), otherwise <path> itself - including when src/ exists
# but holds none, the flat layout the samples and a generated starter use (csproj beside a src/ of
# .cs files, no csproj of its own).
function Get-ModProjectDir([string]$Path) {
  $srcDir = Join-Path $Path 'src'
  if ((Test-Path $srcDir) -and @(Get-ChildItem $srcDir -Filter '*.csproj' -File).Count -eq 1) { return $srcDir }
  return $Path
}

# Reads exmod.json once and applies its defaults - this is the only place they're written down.
# ConvertFrom-Json keeps a PSCustomObject's property order as the file's order, so `mods` and
# `samples` are read through .PSObject.Properties everywhere, never Keys/Values, to keep that order.
function Get-ExmodManifest {
  if ($Script:ExmodManifestCache) { return $Script:ExmodManifestCache }

  $path = Join-Path $RepoRoot 'exmod.json'
  if (-not (Test-Path $path)) { throw "No exmod.json under $RepoRoot." }
  $m = Get-Content $path -Raw | ConvertFrom-Json

  # 'solution' is resolved lazily, by Get-ExmodSolution, so a repo with neither a 'solution' field
  # nor a .sln at its root still loads and runs every command that names no solution.
  if (-not $m.PSObject.Properties['series'] -or -not $m.series) {
    # No series named at all: the first entry of the vendor table above is "the current series".
    $m | Add-Member -NotePropertyName series -NotePropertyValue @(@($GameTfms.Keys)[0]) -Force
  }
  foreach ($p in @('mods', 'samples')) {
    if (-not $m.PSObject.Properties[$p]) { $m | Add-Member -NotePropertyName $p -NotePropertyValue ([pscustomobject]@{}) }
  }
  foreach ($p in @('tests', 'packages')) {
    if (-not $m.PSObject.Properties[$p]) { $m | Add-Member -NotePropertyName $p -NotePropertyValue @() }
  }

  $Script:ExmodManifestCache = $m
  return $m
}

# Every mod this repo builds as its own, in manifest order (the build order: exlib before iiex
# before siex is a real ProjectReference chain, not a discovery accident), id -> @{ Path; Project;
# Tests; Overlays } (all absolute except Overlays). A mod's project is the single .csproj under
# <path>/src when that folder holds one, or under <path> itself otherwise (including a src/ that
# holds only sources - the flat layout the samples and a generated starter use); its test project
# is the single .csproj under <path>/tests when that folder exists.
function Get-ExmodMods {
  $manifest = Get-ExmodManifest
  $out = [ordered]@{}
  foreach ($prop in $manifest.mods.PSObject.Properties) {
    $id = $prop.Name
    $entry = $prop.Value
    $path = Resolve-ManifestPath "mods.$id.path" $entry.path
    $project = Find-SingleCsproj (Get-ModProjectDir $path) "mods.$id"
    $testsDir = Join-Path $path 'tests'
    $tests = if (Test-Path $testsDir) { Find-SingleCsproj $testsDir "mods.$id.tests" } else { $null }
    $out[$id] = [pscustomobject]@{ Path = $path; Project = $project; Tests = $tests; Overlays = $entry.overlays }
  }
  return $out
}

# Every sample this repo builds as its own mod, in manifest order, id -> @{ Path; Project; Tests }.
# A sample's project sits at its own path; its test project, when the manifest names one, is the
# single .csproj under that named folder.
function Get-ExmodSamples {
  $manifest = Get-ExmodManifest
  $out = [ordered]@{}
  foreach ($prop in $manifest.samples.PSObject.Properties) {
    $id = $prop.Name
    $entry = $prop.Value
    $path = Resolve-ManifestPath "samples.$id.path" $entry.path
    $project = Find-SingleCsproj $path "samples.$id"
    $tests = if ($entry.PSObject.Properties['tests'] -and $entry.tests) {
      Find-SingleCsproj (Resolve-ManifestPath "samples.$id.tests" $entry.tests) "samples.$id.tests"
    } else { $null }
    $out[$id] = [pscustomobject]@{ Path = $path; Project = $project; Tests = $tests }
  }
  return $out
}

# Every mod/sample this repo builds as its own mod, in manifest order (mods, then samples), as
# folder-name key -> its project path. Shared by build, run and verify so none of them can drift on
# what "every mod" means.
function Get-ExmodBuildTargets {
  $out = [ordered]@{}
  foreach ($mod in (Get-ExmodMods).GetEnumerator()) { $out[$mod.Key] = $mod.Value.Project }
  foreach ($sample in (Get-ExmodSamples).GetEnumerator()) { $out[$sample.Key] = $sample.Value.Project }
  return $out
}

# Every test project this repo runs: each mod's, in mod order, then each sample's, then
# $Manifest.tests, each carrying the series it runs for - every series in the manifest for a mod,
# the current series only for a sample or an extra project (it buys a legacy lane nothing). Shared
# by `test` and `build -Tests` so the two commands cannot drift on what "every test project" means.
function Get-ExmodTestProjects {
  $manifest = Get-ExmodManifest
  $series = @($manifest.series)
  $current = @($series[0])

  $out = [ordered]@{}
  foreach ($mod in (Get-ExmodMods).GetEnumerator()) {
    if (-not $mod.Value.Tests) { continue }
    $out[$mod.Key] = [pscustomobject]@{
      Project = [IO.Path]::GetFileNameWithoutExtension($mod.Value.Tests)
      Proj    = $mod.Value.Tests
      Series  = $series
    }
  }
  foreach ($sample in (Get-ExmodSamples).GetEnumerator()) {
    if (-not $sample.Value.Tests) { continue }
    $out[$sample.Key] = [pscustomobject]@{
      Project = [IO.Path]::GetFileNameWithoutExtension($sample.Value.Tests)
      Proj    = $sample.Value.Tests
      Series  = $current
    }
  }
  $i = 0
  foreach ($rel in @($manifest.tests)) {
    $proj = Find-SingleCsproj (Resolve-ManifestPath "tests[$i]" $rel) "tests[$i]"
    $name = [IO.Path]::GetFileNameWithoutExtension($proj)
    $id = ($name -replace '\.Tests$', '').ToLowerInvariant()
    $out[$id] = [pscustomobject]@{ Project = $name; Proj = $proj; Series = $current }
    $i++
  }
  return $out
}

# The packable project paths named by $Manifest.packages, each a folder holding exactly one
# .csproj.
function Get-ExmodPackages {
  $manifest = Get-ExmodManifest
  $i = 0
  $out = @()
  foreach ($rel in @($manifest.packages)) {
    $out += Find-SingleCsproj (Resolve-ManifestPath "packages[$i]" $rel) "packages[$i]"
    $i++
  }
  return $out
}

# The solution: $Manifest.solution, or the single .sln at the repo root when the manifest names none.
function Get-ExmodSolution {
  $manifest = Get-ExmodManifest
  if ($manifest.PSObject.Properties['solution'] -and $manifest.solution) {
    return Resolve-ManifestPath 'solution' $manifest.solution
  }
  $sln = @(Get-ChildItem $RepoRoot -Filter '*.sln' -File)
  if ($sln.Count -ne 1) { throw "exmod.json has no 'solution' and $RepoRoot does not hold exactly one .sln." }
  return $sln[0].FullName
}

#endregion

$Manifest = Get-ExmodManifest
$CurrentGameVersion = @($Manifest.series)[0]

# 'latest', 'all' or one series, as the list of series to act on.
function Resolve-GameVersions([string]$Spec) {
  switch ($Spec) {
    'latest' { return @($CurrentGameVersion) }
    'all' { return @($Manifest.series) }
    default {
      if (-not $GameTfms.Contains($Spec)) {
        throw "Unknown version '$Spec'. Use latest, all, or one of: $($GameTfms.Keys -join ', ')."
      }
      return @($Spec)
    }
  }
}

# The dotnet muxer to drive for $Versions: the system one when it already has every runtime major
# they need, otherwise the checkout's own. The global muxer ignores DOTNET_ROOT, so runtimes
# provisioned into .dotnet are only visible through .dotnet/dotnet - which is what lets a machine
# with only .NET 10 installed still run the 1.21 and 1.20 lanes.
function Resolve-DotnetHost([string[]]$Versions) {
  $needed = @($Versions | ForEach-Object { $GameRuntimeMajors[$_] } | Select-Object -Unique)
  $sysRuntimes = try { (& dotnet --list-runtimes 2>$null) -join "`n" } catch { '' }
  $missing = @($needed | Where-Object { $sysRuntimes -notmatch "Microsoft\.NETCore\.App $([regex]::Escape($_))\." })
  if ($missing.Count -eq 0) { return 'dotnet' }
  Write-Host "Missing .NET runtime major(s) system-wide: $($missing -join ', ') - provisioning a local .dotnet..."
  Invoke-ProvisionDotnet @('-Version', ($Versions.Count -eq 1 ? $Versions[0] : 'all'))
  return (Join-Path $RepoRoot ".dotnet/dotnet$ExeSuffix")
}

# A provisioned game install for $Version that can actually run here, preferring $Kind. A client
# package is a superset of a server one, so both slots are searched before anything is downloaded.
# Provisions one when neither answers, into a suffixed slot rather than over an install built for
# another platform: that one is what the owner plays from, and replacing it is their call.
function Resolve-GameInstall([string]$Version = $CurrentGameVersion, [string]$Kind = 'server') {
  if ($Kind -notin @('server', 'client')) { throw "Kind must be 'server' or 'client'." }
  $slug = ($Version -split '\.')[0..1] -join '.'
  $entry = if ($Kind -eq 'server') { 'VintagestoryServer.dll' } else { 'Vintagestory.dll' }
  $candidates = @(".game/$slug-$Kind", ".game/$slug")

  # The entry assembly is in the archive for every platform; the native libraries beside it are not.
  # A package left over from another OS has the dll and none of them, and starts only far enough to
  # fail, so it does not count as an install here.
  $usable = {
    param([string]$Dir)
    if (-not (Test-Path (Join-Path $Dir $entry))) { return $false }
    return $OnWindows -or (Test-Path (Join-Path $Dir 'Lib/libe_sqlite3.so'))
  }
  $find = {
    foreach ($c in $candidates) {
      $full = Join-Path $RepoRoot $c
      if (& $usable $full) { return $full }
    }
    return $null
  }

  $hit = & $find
  if ($hit) { return $hit }

  Write-Host "No usable $Kind install for $Version - provisioning one..."
  $provisionArgs = @('-Version', $Version, '-Kind', $Kind)
  # provision game redirects a server request away from a foreign client on its own; a client request
  # would land on top of it, so this one is redirected here instead.
  $defaultSlot = Join-Path $RepoRoot ".game/$slug"
  if ($Kind -eq 'client' -and (Test-Path (Join-Path $defaultSlot 'Vintagestory.dll'))) {
    $provisionArgs += @('-Dest', ".game/$slug-client")
  }
  Invoke-ProvisionGame $provisionArgs

  $hit = & $find
  if (-not $hit) {
    throw "Provisioning completed but no usable $Kind install was found under $($candidates -join ' or ')."
  }
  return $hit
}

# Every mod/sample the manifest names, as built output directories (the folder holding modinfo.json
# and the dll). A mod that has not been built yet is built first, so a fresh clone still works.
function Get-BuiltModDirs([string]$Configuration = 'Debug') {
  $out = @()
  foreach ($target in (Get-ExmodBuildTargets).GetEnumerator()) {
    $srcDir = Split-Path $target.Value -Parent
    $built = Join-Path $srcDir "bin/$Configuration/Mods/mod"
    if (-not (Test-Path (Join-Path $built 'modinfo.json'))) {
      Write-Host "Building $($target.Key) (not yet built) ..."
      dotnet build $target.Value -c $Configuration -clp:ErrorsOnly
      if ($LASTEXITCODE -ne 0) { throw "Build of $($target.Value) failed." }
    }
    $out += $built
  }
  return $out
}

# Resolves each caller-supplied path to one or more mod folders, so both "path/to/one/mod" and
# "path/to/several/mods" work wherever a -Mods list is accepted.
function Resolve-ModDirs([string[]]$Dirs) {
  $out = @()
  foreach ($d in $Dirs) {
    $full = if ([System.IO.Path]::IsPathRooted($d)) { $d } else { Join-Path $RepoRoot $d }
    if (-not (Test-Path $full)) { throw "Mod path not found: $full" }
    if (Test-Path (Join-Path $full 'modinfo.json')) {
      $out += $full
    }
    else {
      $subs = @(Get-ChildItem $full -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'modinfo.json') })
      if (-not $subs) { throw "'$full' is neither a mod folder (no modinfo.json) nor a folder of mod folders." }
      $out += @($subs.FullName)
    }
  }
  return $out
}

#region Dependency mods

# A version string with any prerelease suffix (everything from the first '-' on) stripped, cast to
# [version] so two versions compare numerically rather than lexically ("0.7.10" > "0.7.9").
function Get-VersionCore([string]$Version) {
  return [version](($Version -split '-', 2)[0])
}

# The workspace-sibling build output for a dependency id, or $null when no directory beside
# $RepoRoot names it. A sibling is a directory next to this checkout holding its own exmod.json
# whose `mods` names $Id; its manifest is read raw rather than through Get-ExmodManifest, which is
# fixed to this checkout's $RepoRoot.
function Get-ExmodDependencySiblingProject([string]$Id) {
  $parent = Split-Path $RepoRoot -Parent
  if (-not $parent -or -not (Test-Path $parent)) { return $null }
  foreach ($dir in Get-ChildItem $parent -Directory -ErrorAction SilentlyContinue) {
    if ($dir.FullName -eq $RepoRoot) { continue }
    $manifestPath = Join-Path $dir.FullName 'exmod.json'
    if (-not (Test-Path $manifestPath -ErrorAction SilentlyContinue)) { continue }
    $sib = $null
    try { $sib = Get-Content $manifestPath -Raw -ErrorAction Stop | ConvertFrom-Json } catch { continue }
    if (-not $sib.PSObject.Properties['mods']) { continue }
    $entry = $sib.mods.PSObject.Properties[$Id]
    if (-not $entry) { continue }
    $modPath = Join-Path $dir.FullName $entry.Value.path
    if (-not (Test-Path $modPath)) { continue }
    return Find-SingleCsproj (Get-ModProjectDir $modPath) "sibling mods.$Id"
  }
  return $null
}

# The ModDB release to use for $Id at floor $Floor: the release whose modversion equals the floor
# exactly, else the lowest modversion above it. A prerelease floor ModDB never carries (it only ever
# publishes stable releases) fails naming the two branches that do carry one.
function Resolve-ModDbRelease([string]$Id, [string]$Floor) {
  $record = Invoke-RestMethod -Uri "https://mods.vintagestory.at/api/mod/$Id" -TimeoutSec 15
  if (-not $record.mod.releases) { throw "ModDB has no releases for '$Id'." }
  $exact = $record.mod.releases | Where-Object { $_.modversion -eq $Floor } | Select-Object -First 1
  if ($exact) { return $exact }
  $floorCore = Get-VersionCore $Floor
  $above = @($record.mod.releases | Where-Object { (Get-VersionCore $_.modversion) -gt $floorCore }) |
    Sort-Object { Get-VersionCore $_.modversion }
  if ($above) { return $above[0] }
  if ($Floor -match '-') {
    throw "No ModDB release of '$Id' at or above prerelease floor $Floor - ModDB does not carry prereleases. Name 'url' or 'github' for it in exmod.json's depends.$Id."
  }
  throw "No ModDB release of '$Id' at or above $Floor."
}

# Extracts $Zip into $Dest (replaced if present), unwrapping one top-level folder when the archive
# wraps its content that way (GitHub's "download zip" habit) - the same shape ExlibVerify's
# ModSource.Load applies to a mod zip.
function Expand-DependencyZip([string]$Zip, [string]$Dest) {
  if (Test-Path $Dest) { Remove-Item -Recurse -Force $Dest }
  New-Item -ItemType Directory -Force -Path $Dest | Out-Null
  Expand-Archive -Path $Zip -DestinationPath $Dest -Force
  if (Test-Path (Join-Path $Dest 'modinfo.json')) { return }
  $inner = Get-ChildItem $Dest -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'modinfo.json') } |
    Select-Object -First 1
  if (-not $inner) { throw "$Zip has no modinfo.json at its root or one level down." }
  Get-ChildItem $inner.FullName -Force | Move-Item -Destination $Dest -Force
  Remove-Item -Recurse -Force $inner.FullName
}

# One dependency, resolved: a workspace sibling's build output (built first if missing), else a
# cached extraction reused when its version already matches the floor, else a fresh download - the
# manifest's depends.<Id>.url (with {version}/{id} substituted), else depends.<Id>.github
# (owner/repo, resolved against its v<version> release tag), else the ModDB API. Downloads land
# under $RepoRoot/.exmod/cache and extract under $RepoRoot/.exmod/mods/<Id>.
function Resolve-OneDependency([string]$Id, [string]$Floor, [string]$Configuration) {
  $siblingProject = Get-ExmodDependencySiblingProject $Id
  if ($siblingProject) {
    $srcDir = Split-Path $siblingProject -Parent
    $built = Join-Path $srcDir "bin/$Configuration/Mods/mod"
    if (-not (Test-Path (Join-Path $built 'modinfo.json'))) {
      Write-Host "Building $Id (workspace sibling, not yet built) ..."
      dotnet build $siblingProject -c $Configuration -clp:ErrorsOnly
      if ($LASTEXITCODE -ne 0) { throw "Build of $siblingProject failed." }
    }
    Write-Host "$Id : workspace sibling, built output at $built"
    return [pscustomobject]@{ Id = $Id; Version = $Floor; Path = $built; Source = 'workspace sibling' }
  }

  $cacheDir = Join-Path $RepoRoot ".exmod/mods/$Id"
  $cacheModinfo = Join-Path $cacheDir 'modinfo.json'
  if (Test-Path $cacheModinfo) {
    $cached = Get-Content $cacheModinfo -Raw | ConvertFrom-Json
    if ($cached.version -eq $Floor) {
      Write-Host "$Id : cached release $Floor at $cacheDir"
      return [pscustomobject]@{ Id = $Id; Version = $Floor; Path = $cacheDir; Source = 'cache' }
    }
  }

  $manifest = Get-ExmodManifest
  $dependsEntry = $null
  if ($manifest.PSObject.Properties['depends']) {
    $prop = $manifest.depends.PSObject.Properties[$Id]
    if ($prop) { $dependsEntry = $prop.Value }
  }

  $version = $Floor
  if ($dependsEntry -and $dependsEntry.PSObject.Properties['url'] -and $dependsEntry.url) {
    $url = $dependsEntry.url.Replace('{version}', $Floor).Replace('{id}', $Id)
    $sourceLabel = "download: $url"
  }
  elseif ($dependsEntry -and $dependsEntry.PSObject.Properties['github'] -and $dependsEntry.github) {
    $url = "https://github.com/$($dependsEntry.github)/releases/download/v$Floor/${Id}_$Floor.zip"
    $sourceLabel = "GitHub release: $($dependsEntry.github) v$Floor"
  }
  else {
    $release = Resolve-ModDbRelease $Id $Floor
    $version = $release.modversion
    $url = $release.mainfile
    $sourceLabel = "ModDB release $version"
  }

  $cacheZipDir = Join-Path $RepoRoot '.exmod/cache'
  New-Item -ItemType Directory -Force -Path $cacheZipDir | Out-Null
  $zip = Join-Path $cacheZipDir "${Id}_$version.zip"
  if (-not (Test-Path $zip)) {
    Write-Host "Downloading $Id $version from $url"
    Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
  }
  Expand-DependencyZip $zip $cacheDir
  Write-Host "$Id : $sourceLabel, extracted to $cacheDir"
  return [pscustomobject]@{ Id = $Id; Version = $version; Path = $cacheDir; Source = $sourceLabel }
}

# Every runtime dependency this repo does not build itself: the `dependencies` of every mod and
# sample the manifest names, minus 'game' and every id this repo's own mods/samples build, each
# resolved at the highest floor version any of them names for it. Returns an ordered list of
# @{ Id; Version; Path; Source }, in the order the ids were first seen.
function Resolve-DependencyMods([string]$Configuration = 'Debug') {
  $builtIds = @(@((Get-ExmodMods).Keys) + @((Get-ExmodSamples).Keys))

  $floors = [ordered]@{}
  foreach ($m in (@((Get-ExmodMods).Values) + @((Get-ExmodSamples).Values))) {
    $modinfoPath = Join-Path (Split-Path $m.Project -Parent) 'modinfo.json'
    if (-not (Test-Path $modinfoPath)) { continue }
    $modinfo = Get-Content $modinfoPath -Raw | ConvertFrom-Json
    if (-not $modinfo.dependencies) { continue }
    foreach ($dep in $modinfo.dependencies.PSObject.Properties) {
      if ($dep.Name -eq 'game') { continue }
      if ($builtIds -contains $dep.Name) { continue }
      if (-not $floors.Contains($dep.Name) -or (Get-VersionCore $dep.Value) -gt (Get-VersionCore $floors[$dep.Name])) {
        $floors[$dep.Name] = $dep.Value
      }
    }
  }

  $out = @()
  foreach ($id in $floors.Keys) {
    $out += Resolve-OneDependency $id $floors[$id] $Configuration
  }
  return $out
}

#endregion

#region Command registry

# Every command registers itself here, next to its own implementation, so the help text and the
# dispatch table cannot disagree about what exists.
$Script:ExmodCommands = [ordered]@{}

# Group orders the summary and titles its sections.
$Script:ExmodGroups = [ordered]@{
  start   = 'first run'
  source  = 'source'
  run     = 'run'
  package = 'package'
  machine = 'machine'
}

# Registers one command. Summary is its line in the grouped list; Detail is what `exmod help <name>`
# prints; Action receives the arguments after the command name as a string array.
function Add-ExmodCommand {
  param(
    [Parameter(Mandatory)][string]$Group,
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][string]$Summary,
    [Parameter(Mandatory)][string]$Detail,
    [Parameter(Mandatory)][scriptblock]$Action,
    [string[]]$Alias = @()
  )
  if (-not $Script:ExmodGroups.Contains($Group)) { throw "Unknown command group '$Group'." }
  $Script:ExmodCommands[$Name] = [pscustomobject]@{
    Group   = $Group
    Name    = $Name
    Summary = $Summary
    Detail  = $Detail.Trim()
    Action  = $Action
    Alias   = $Alias
  }
}

function Resolve-ExmodCommand([string]$Name) {
  if (-not $Name) { return $null }
  if ($Script:ExmodCommands.Contains($Name)) { return $Script:ExmodCommands[$Name] }
  foreach ($c in $Script:ExmodCommands.Values) {
    if ($c.Alias -contains $Name) { return $c }
  }
  return $null
}

function Show-ExmodHelp([string]$Name) {
  if ($Name) {
    $cmd = Resolve-ExmodCommand $Name
    if (-not $cmd) {
      Write-Host "exmod: no such command: $Name" -ForegroundColor Red
      Show-ExmodHelp
      exit 1
    }
    Write-Host ''
    Write-Host $cmd.Detail
    Write-Host ''
    return
  }

  Write-Host ''
  Write-Host 'exmod - every task in this repo, from a fresh clone to a tagged release.'
  Write-Host ''
  Write-Host '  exmod <command> [arguments]        exmod help <command> for one in detail'
  $width = ($Script:ExmodCommands.Values | ForEach-Object { $_.Name.Length } | Measure-Object -Maximum).Maximum
  foreach ($group in $Script:ExmodGroups.Keys) {
    $members = @($Script:ExmodCommands.Values | Where-Object { $_.Group -eq $group })
    if (-not $members) { continue }
    Write-Host ''
    Write-Host $Script:ExmodGroups[$group] -ForegroundColor Cyan
    foreach ($c in $members) {
      Write-Host ('  {0}  {1}' -f $c.Name.PadRight($width), $c.Summary)
    }
  }
  Write-Host ''
}

#endregion

# The commands themselves, one file per stage. Dot-sourced, so everything above is in scope for them
# and their Add-ExmodCommand calls run before dispatch. A file that is not there is skipped rather
# than fatal: another repo copies this dispatcher with only the stages it wants (see
# templates/ci/tests.yml), and here a missing one shows up as a missing command in `exmod`.
foreach ($module in @('provision', 'src', 'run', 'dist', 'windows')) {
  $path = Join-Path $PSScriptRoot "exmod/$module.ps1"
  if (Test-Path $path) { . $path }
}

if ($Command -in @('', $null, 'help', '-h', '--help', 'commands')) {
  Show-ExmodHelp ($Arguments | Select-Object -First 1)
  return
}

$resolved = Resolve-ExmodCommand $Command
if (-not $resolved) {
  # A mistyped command is a usage error, not a crash: the list is more use here than a stack trace.
  Write-Host "exmod: no such command: $Command" -ForegroundColor Red
  Show-ExmodHelp
  exit 1
}
& $resolved.Action $Arguments
