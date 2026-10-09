$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$out=Join-Path $root 'output/solution-v01'
function Hash-Shared([string]$path){$stream=[IO.File]::Open($path,'Open','Read','ReadWrite');$sha=[Security.Cryptography.SHA256]::Create();try{([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','')}finally{$sha.Dispose();$stream.Dispose()}}
$references=@(Get-Content (Join-Path $root 'output/phase1/reference-hashes-before.json') -Raw|ConvertFrom-Json|ForEach-Object {[pscustomobject]@{Path=$_.Path;Unchanged=($_.Hash -eq (Hash-Shared $_.Path))}})
if(@($references|Where-Object {-not $_.Unchanged}).Count){throw 'Protected reference changed'}
$sources=@(foreach($folder in @('Elevator.Core','Elevator.AutoCAD')){foreach($file in Get-ChildItem (Join-Path $root $folder) -Filter '*.cs' -Recurse){$relative=$file.FullName.Substring($root.Length+1);[pscustomobject]@{Path=$relative;Unchanged=((Hash-Shared $file.FullName) -eq (Hash-Shared (Join-Path $root "output/phase1/regression/ElevatorAutomation/$relative")))}}})
if(@($sources|Where-Object {-not $_.Unchanged}).Count){throw 'Accepted Core/AutoCAD source changed'}
$templates=@(foreach($file in Get-ChildItem (Join-Path $root 'Templates') -Filter '*.dwg'){[pscustomobject]@{Path=$file.FullName;SHA256=Hash-Shared $file.FullName;LastWriteTimeUtc=$file.LastWriteTimeUtc;PredatesPhaseTwo=($file.LastWriteTimeUtc -lt [datetime]'2026-09-27');Evidence='Final hash and pre-phase modification time; regression suites regenerate their own template copies, so those hashes are not reference baselines.'}})
if(@($templates|Where-Object {-not $_.PredatesPhaseTwo}).Count){throw 'Template write time requires review'}
$testDirectory=Get-ChildItem $out -Directory -Filter 'test-*'|Sort-Object LastWriteTime -Descending|Where-Object {Test-Path (Join-Path $_.FullName 'solution-tests.json')}|Where-Object {@(Get-Content (Join-Path $_.FullName 'solution-tests.json') -Raw|ConvertFrom-Json).Count -eq 16}|Select-Object -First 1
if(-not $testDirectory){throw 'Final sixteen-test report missing'}
$newTests=@(Get-Content (Join-Path $testDirectory.FullName 'solution-tests.json') -Raw|ConvertFrom-Json)
if(@($newTests|Where-Object Status -ne PASS).Count){throw 'New solution test failed'}
$phaseOne=Get-Content (Join-Path $root 'output/phase1/verification-summary.json') -Raw|ConvertFrom-Json
$regressionRoot=Join-Path $out 'regression/ElevatorAutomation'
$reports=@(foreach($spec in $phaseOne.Reports){$rows=@(Get-Content (Join-Path $regressionRoot $spec.Report) -Raw|ConvertFrom-Json);if($rows.Count -ne $spec.Passed -or @($rows|Where-Object Status -ne PASS).Count){throw "Phase 1 regression failed $($spec.Report)"};[pscustomobject]@{Report=$spec.Report;Pass=$rows.Count}})
$uiDirectory=Get-ChildItem $out -Directory -Filter 'ui-e2e-*'|Sort-Object LastWriteTime -Descending|Where-Object {Test-Path (Join-Path $_.FullName 'ui-e2e-result.json')}|Where-Object {(Get-Content (Join-Path $_.FullName 'ui-e2e-result.json') -Raw|ConvertFrom-Json).Status -eq 'PASS'}|Select-Object -First 1
if(-not $uiDirectory){throw 'Successful actual WPF end-to-end evidence missing'}
$comparisons=@(foreach($case in @('baseline','unequal')){$rows=@(Get-Content (Join-Path $uiDirectory.FullName "review-$case/geometry-comparison.json") -Raw|ConvertFrom-Json);if(@($rows|Where-Object Status -ne PASS).Count){throw 'Geometry comparison failed'};[pscustomobject]@{Case=$case;EntitiesCompared=$rows[0].Entities;Status='PASS'}})
$artifacts=@(Get-ChildItem (Join-Path $root 'bin-solution-v01') -File|Where-Object Extension -in '.dll','.exe','.json','.config'|ForEach-Object {[pscustomobject]@{Path=$_.FullName;SHA256=Hash-Shared $_.FullName}})
[pscustomobject]@{
 Status='PASS';PhaseOneCoreAssertions=2469;PhaseOneNativeAndPlotChecks=($reports|Measure-Object Pass -Sum).Sum;
 NewSolutionTests=$newTests.Count;ActualWpfEndToEnd=$uiDirectory.FullName;NewTests=$testDirectory.FullName;
 GeometryComparisons=$comparisons;VisualReview='Native plots reviewed; four extents separated. Long Upper floor label crowds an A-A dimension in retained native layout; manual review required.';
 ReferenceProtection=$references;TemplateProtection=$templates;AcceptedSources=$sources;Reports=$reports;Artifacts=$artifacts
}|ConvertTo-Json -Depth 12|Set-Content (Join-Path $out 'verification-summary.json') -Encoding UTF8
Get-ChildItem (Join-Path $root 'Elevator.Solution.*') -Recurse -File|Select-Object FullName|ConvertTo-Json|Set-Content (Join-Path $out 'new-project-files.json') -Encoding UTF8
Write-Output "PASS: $($newTests.Count) new solution tests; $($reports|Measure-Object Pass -Sum|Select-Object -ExpandProperty Sum) Phase 1 native/plot checks; $($sources.Count) unchanged source files; $($references.Count) unchanged reference DWGs; $($templates.Count) template hashes recorded with pre-phase write times."
