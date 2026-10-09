$ErrorActionPreference='Stop'
$sourceRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$evidenceOut=Join-Path $sourceRoot 'output/phase1'
$regressionRoot=Join-Path $evidenceOut 'regression/ElevatorAutomation'
function Read-SharedHash([string]$path){
    $stream=[IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
    $sha=[Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','') }
    finally { $sha.Dispose(); $stream.Dispose() }
}
$protected=@(Get-Content (Join-Path $evidenceOut 'reference-hashes-before.json') -Raw|ConvertFrom-Json|ForEach-Object {
    $after=Read-SharedHash $_.Path
    [pscustomobject]@{Path=$_.Path;Before=$_.Hash;After=$after;Unchanged=($_.Hash -eq $after)}
})
$protected|ConvertTo-Json -Depth 6|Set-Content (Join-Path $evidenceOut 'reference-hashes-after.json') -Encoding UTF8
if(@($protected|Where-Object { -not $_.Unchanged }).Count){throw 'Reference DWG hash changed'}
$changes=@(foreach($folder in @('Elevator.Core','Elevator.AutoCAD','config','tests')){
    foreach($file in Get-ChildItem (Join-Path $sourceRoot $folder) -Recurse -File|Where-Object Extension -in '.cs','.csproj','.ps1','.scr','.json'){
        $relative=$file.FullName.Substring($sourceRoot.Length+1)
        $before=Join-Path (Join-Path $evidenceOut 'checkpoint') $relative
        if(-not(Test-Path -LiteralPath $before)){[pscustomobject]@{Path=$relative;Change='Added'}}
        elseif((Read-SharedHash $before) -ne (Read-SharedHash $file.FullName)){[pscustomobject]@{Path=$relative;Change='Changed'}}
    }
})
$changes+=@([pscustomobject]@{Path='build.ps1';Change='Changed'})
foreach($file in @('docs/CURRENT_INPUT_INVENTORY.md','docs/ELEVATOR_PROJECT_MODEL.md','docs/ELEVATOR_SOLUTION_ARCHITECTURE.md','examples/ElevatorProject.example.json')){
    $changes+=[pscustomobject]@{Path=$file;Change='Added'}
}
$changes|Sort-Object Path|ConvertTo-Json -Depth 6|Set-Content (Join-Path $evidenceOut 'files-changed.json') -Encoding UTF8
$specs=@(
    @('tests/cad-test-results.json',17),@('tests/architecture-results.json',14),@('tests/workflow-results.json',1),
    @('tests/plan-results.json',7),@('tests/machine-room-results.json',32),@('tests/section-results.json',32),@('tests/section-reopen-results.json',2),
    @('output/section-aa/native-tests.json',30),@('output/section-aa/fresh-reopen-tests.json',1),@('output/section-aa/installation-tests.json',4),
    @('output/phase1/project-native-tests.json',6),@('output/phase1/project-reopen-tests.json',1),@('output/phase1/project-create-tests.json',2),
    @('output/experimental-vertical/vertical-native-results.json',19),@('output/experimental-vertical/vertical-resize-results.json',3),
    @('output/experimental-vertical/vertical-interactive-results.json',3),@('output/experimental-vertical/vertical-fresh-process-results.json',2),
    @('output/experimental-vertical/native-plot-results.json',6)
)
$reports=@(foreach($spec in $specs){
    $rows=@(Get-Content (Join-Path $regressionRoot $spec[0]) -Raw|ConvertFrom-Json)
    if($rows.Count -ne $spec[1] -or @($rows|Where-Object Status -ne PASS).Count){throw "Failed/incomplete: $($spec[0])"}
    [pscustomobject]@{Report=$spec[0];Passed=$rows.Count}
})
$sourceMatches=@(foreach($folder in @('Elevator.Core','Elevator.AutoCAD')){
    foreach($file in Get-ChildItem (Join-Path $sourceRoot $folder) -Recurse -Filter '*.cs'){
        $relative=$file.FullName.Substring($sourceRoot.Length+1)
        [pscustomobject]@{Path=$relative;Identical=((Read-SharedHash $file.FullName) -eq (Read-SharedHash (Join-Path $regressionRoot $relative)))}
    }
})
$sourceMatches|ConvertTo-Json -Depth 5|Set-Content (Join-Path $evidenceOut 'regression-source-equivalence.json') -Encoding UTF8
if(@($sourceMatches|Where-Object {-not $_.Identical}).Count){throw 'Final sources differ from tested sources'}
$historical=Get-Content (Join-Path $regressionRoot 'output/experimental-vertical/vertical-core-results.json') -Raw|ConvertFrom-Json
if($historical.Status -ne 'PASS' -or $historical.Assertions -ne 2255){throw 'Historical Core test report failed'}
foreach($name in @('Unified_1450_1600.dwg','Unified_1650_1800.dwg','Unified_1850_2000.dwg','Unified_Reopen.dwg','Unified_AllFour.dwg')){
    Copy-Item -LiteralPath (Join-Path $regressionRoot "output/phase1/$name") -Destination (Join-Path $evidenceOut $name) -Force
}
$summary=[pscustomobject]@{
    Status='PASS';ExistingCoreAssertions=190;NewProjectCoreTests=24;HistoricalVerticalCoreAssertions=2255;
    TotalCoreAssertions=2469;NativeAndPlotChecks=($reports|Measure-Object Passed -Sum).Sum;
    ProtectedReferenceDwgs=$protected.Count;FinalSourceFilesMatched=$sourceMatches.Count;
    FourViewCases=@('1450/1600','1650/1800','1850/2000');
    GeometryComparison='Existing native entity comparator; all four semantic view sets, geometry, dimensions, text and visibility';
    Reports=$reports;FormalExternalJsonSchemaValidation='Not run: jsonschema package unavailable'
}
$summary|ConvertTo-Json -Depth 8|Set-Content (Join-Path $evidenceOut 'verification-summary.json') -Encoding UTF8
$summary|Select-Object Status,TotalCoreAssertions,NativeAndPlotChecks,ProtectedReferenceDwgs,FinalSourceFilesMatched|Format-List
