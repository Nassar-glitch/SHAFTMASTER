param(
 [Parameter(Mandatory=$true)][ValidatePattern('^v\d{2,}$')][string]$Version,
 [switch]$DryRun,
 [switch]$SkipTests,
 [string]$AutoCADDir='C:\Program Files\Autodesk\AutoCAD 2023',
 [string]$SolidWorksDir='C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS')
# Release (ARCHITECTURE-PLAN P8). ./tools/release.ps1 -Version v04
#   1. checks the target: ElevatorAutomation/bin-shaftmaster-<Version> must not exist (a release folder is never overwritten) and
#      CHANGELOG.md must have a "## [<Version>]" section (the entries of "Unreleased" are moved there when a release is prepared);
#   2. builds into artifacts/release-build (tools/build.ps1, which also runs the pure rule tests and the STEP-input tests);
#   3. runs tiers 1, 2 and 3 (tools/test.ps1) on that build; any failure stops the release (-SkipTests leaves this out and says so);
#   4. copies the build to the target folder, writes RELEASE-NOTES.md (the CHANGELOG section, the key hashes and the test results)
#      and SHA256SUMS.txt;
#   5. verifies the copy against SHA256SUMS.txt, checks that the earlier release folders were not changed, and prints a summary.
# -DryRun does everything into artifacts/release-dryrun/bin-shaftmaster-<Version> (replaced on every dry run) and creates nothing in
# ElevatorAutomation; the CHANGELOG section may then be "Unreleased".
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$repo=Split-Path $root -Parent
$name="bin-shaftmaster-$Version"
$target=if($DryRun){Join-Path $root "artifacts/release-dryrun/$name"}else{Join-Path $root $name}
$buildRel='artifacts/release-build'
$build=Join-Path $root $buildRel
$sw=[Diagnostics.Stopwatch]::StartNew()
function Step([string]$text){Write-Output ("[{0,4}s] {1}" -f [int]$sw.Elapsed.TotalSeconds,$text)}

# ---- 1. target and changelog ----
if(-not $DryRun -and (Test-Path -LiteralPath $target)){throw "Refusing to overwrite the existing release folder: $target"}
$changelog=[IO.File]::ReadAllText((Join-Path $repo 'CHANGELOG.md'))
$section=$null;$sectionName=$null
foreach($candidate in "[$Version]",'[Unreleased]'){
 $m=[regex]::Match($changelog,'(?ms)^## '+[regex]::Escape($candidate)+'[^\r\n]*\r?\n(.*?)(?=^## \[|\z)')
 if($m.Success){$section=$m.Groups[1].Value.Trim();$sectionName=$candidate;break}
 if(-not $DryRun){break}
}
if(-not $section){throw "CHANGELOG.md has no section '## [$Version]'. Move the entries of 'Unreleased' into it first."}
Step "Target $target (changelog section $sectionName)$(if($DryRun){' [dry run]'})"

# ---- 2. build ----
if(Test-Path -LiteralPath $build){
 if(-not ((Resolve-Path -LiteralPath $build).Path.StartsWith((Join-Path $root 'artifacts'),[StringComparison]::OrdinalIgnoreCase))){throw "Unexpected build folder $build"}
 Remove-Item -LiteralPath $build -Recurse -Force
}
Step "Build into $buildRel"
& powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'build.ps1') -Bin $buildRel -AutoCADDir $AutoCADDir -SolidWorksDir $SolidWorksDir
if($LASTEXITCODE){throw 'Build failed'}

# ---- 3. tests ----
$testSummary=$null
if($SkipTests){Step 'Tests skipped (-SkipTests)'}
else{
 Step 'Tiers 1, 2 and 3'
 & powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'test.ps1') -Tier 1,2,3 -Bin $buildRel
 if($LASTEXITCODE){throw 'Tests failed; no release is written'}
 $latest=Get-ChildItem (Join-Path $root 'artifacts/test-results') -Directory|Where-Object{$_.Name -match '^[0-9]{8}-[0-9]{6}$' -and (Test-Path -LiteralPath (Join-Path $_.FullName 'summary.json'))}|Sort-Object Name|Select-Object -Last 1
 $testSummary=Get-Content -LiteralPath (Join-Path $latest.FullName 'summary.json') -Raw|ConvertFrom-Json
}

# ---- 4. copy, release notes, checksums ----
if(Test-Path -LiteralPath $target){
 if(-not ((Resolve-Path -LiteralPath $target).Path.StartsWith((Join-Path $root 'artifacts/release-dryrun'),[StringComparison]::OrdinalIgnoreCase))){throw "Refusing to replace $target"}
 Remove-Item -LiteralPath $target -Recurse -Force
}
Step "Copy to $target"
New-Item -ItemType Directory -Force -Path $target|Out-Null
Copy-Item -Path (Join-Path $build '*') -Destination $target -Recurse -Force
function Hash([string]$file){(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash}
$date=Get-Date -Format 'yyyy-MM-dd'
$notes=New-Object System.Collections.ArrayList
[void]$notes.Add("# ShaftMaster $Version - release notes ($date)")
[void]$notes.Add('')
[void]$notes.Add('| File | SHA-256 |')
[void]$notes.Add('|---|---|')
foreach($f in 'ShaftMaster.exe','Elevator.Core.dll','presets.json'){if(Test-Path -LiteralPath (Join-Path $target $f)){[void]$notes.Add(('| `{0}` | `{1}` |' -f $f,(Hash (Join-Path $target $f))))}}
[void]$notes.Add('')
[void]$notes.Add($section)
[void]$notes.Add('')
[void]$notes.Add('## Release check')
[void]$notes.Add('')
if($testSummary){
 [void]$notes.Add("Tiers 1, 2 and 3 of ``tools/test.ps1`` on this build: $($testSummary.Status).")
 [void]$notes.Add('')
 [void]$notes.Add('| Tier | Suite | Assertions | Result |')
 [void]$notes.Add('|---|---|---|---|')
 foreach($s in $testSummary.Suites){[void]$notes.Add("| $($s.Tier) | $($s.Suite) | $($s.Assertions) | $($s.Status) |")}
}else{[void]$notes.Add('The tests were not run for this build (-SkipTests).')}
[void]$notes.Add('')
[void]$notes.Add('The engineering equations are adopted source-note rules. No standards compliance, manufacturer approval or certification is implied.')
[IO.File]::WriteAllText((Join-Path $target 'RELEASE-NOTES.md'),(($notes -join "`r`n")+"`r`n"),(New-Object Text.UTF8Encoding($false)))
$targetFull=(Resolve-Path -LiteralPath $target).Path
$files=Get-ChildItem -LiteralPath $targetFull -Recurse -File|Where-Object{$_.Name -ne 'SHA256SUMS.txt'}
$lines=$files|ForEach-Object{$rel=$_.FullName.Substring($targetFull.Length).TrimStart('\','/').Replace('\','/');[pscustomobject]@{Rel=$rel;Hash=(Hash $_.FullName).ToLowerInvariant()}}|Sort-Object {$_.Rel.ToLowerInvariant()}|ForEach-Object{"$($_.Hash)  $($_.Rel)"}
[IO.File]::WriteAllText((Join-Path $target 'SHA256SUMS.txt'),(($lines -join "`n")+"`n"),(New-Object Text.UTF8Encoding($false)))

# ---- 5. verify ----
Step 'Verify'
$bad=@()
foreach($line in (Get-Content -LiteralPath (Join-Path $target 'SHA256SUMS.txt'))){
 $hashValue,$rel=$line -split '  ',2
 $file=Join-Path $targetFull $rel
 if(-not (Test-Path -LiteralPath $file) -or ((Hash $file).ToLowerInvariant() -ne $hashValue)){$bad+=$rel}
}
if($bad.Count){throw ('Verification failed for: '+($bad -join ', '))}
foreach($must in 'ShaftMaster.exe','Elevator.Core.dll','Elevator.AutoCAD.dll','Elevator.Solution.Host.dll','Elevator.Solution.Bridge.dll','Elevator.Solution.StepExport.exe','presets.json','RELEASE-NOTES.md'){
 if(-not (Test-Path -LiteralPath (Join-Path $target $must))){throw "Missing in the release: $must"}
}
# The earlier releases are never written: each one's SHA256SUMS.txt (where it has one) still matches its files.
$earlier=@()
foreach($dir in Get-ChildItem -LiteralPath $root -Directory -Filter 'bin-shaftmaster-v*'|Where-Object{$_.Name -ne $name}){
 $sums=Join-Path $dir.FullName 'SHA256SUMS.txt'
 if(Test-Path -LiteralPath $sums){
  $broken=0
  foreach($line in (Get-Content -LiteralPath $sums)){
   $h,$r=$line -split '  ',2
   $f=Join-Path $dir.FullName $r
   if(-not (Test-Path -LiteralPath $f) -or ((Hash $f).ToLowerInvariant() -ne $h.ToLowerInvariant())){$broken++}
  }
  if($broken){throw "Earlier release $($dir.Name) no longer matches its SHA256SUMS.txt ($broken files)"}
  $earlier+="$($dir.Name): checksums match"
 }else{$earlier+="$($dir.Name): no SHA256SUMS.txt"}
}

# ---- summary ----
Step 'Done'
Write-Output ''
Write-Output "Release $Version$(if($DryRun){' (DRY RUN)'})"
Write-Output "  folder        $target"
Write-Output "  files         $($files.Count) (+ SHA256SUMS.txt)"
Write-Output "  ShaftMaster   $(Hash (Join-Path $target 'ShaftMaster.exe'))"
Write-Output "  notes         RELEASE-NOTES.md from CHANGELOG section $sectionName"
Write-Output ("  tests         "+$(if($testSummary){"tiers 1-3 $($testSummary.Status), $(@($testSummary.Suites).Count) suites"}else{'SKIPPED'}))
Write-Output "  verified      $($lines.Count) files against SHA256SUMS.txt"
foreach($e in $earlier){Write-Output "  earlier       $e"}
if($DryRun){Write-Output "  nothing was created in ElevatorAutomation; the dry-run copy is under artifacts/release-dryrun"}
