$ErrorActionPreference='Stop'
$phaseSource=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$phaseRoot=Join-Path $phaseSource 'output/phase1/regression/ElevatorAutomation'
$phaseOut=Join-Path $phaseRoot 'output/phase1'
$core='C:/Program Files/Autodesk/AutoCAD 2023/accoreconsole.exe'
foreach($stage in @('run-native','reopen','create')){
    $source=if($stage -eq 'run-native'){Join-Path $phaseSource 'output/section-aa/SectionAA_Review.dwg'}elseif($stage -eq 'create'){Join-Path $phaseRoot 'Templates/ExistingPlan_Master.dwg'}else{Join-Path $phaseOut 'Unified_Reopen.dwg'}
    $hostFile=Join-Path $phaseOut "$stage-host.dwg"
    Copy-Item -LiteralPath $source -Destination $hostFile -Force
    $script=(Get-Content (Join-Path $phaseSource "tests/project/$stage.scr") -Raw).Replace('{ROOT}',$phaseRoot.Replace('\','/'))
    $scriptFile=Join-Path $phaseOut "$stage.scr"
    Set-Content -LiteralPath $scriptFile -Value $script -Encoding ascii
    & $core /i $hostFile /s $scriptFile /l en-US *> (Join-Path $phaseOut "$stage.log")
    if($LASTEXITCODE -ne 0){throw "Native stage failed $stage"}
}
foreach($spec in @(@('project-native-tests.json',6),@('project-reopen-tests.json',1),@('project-create-tests.json',2))){
    $rows=@(Get-Content (Join-Path $phaseOut $spec[0]) -Raw|ConvertFrom-Json)
    if($rows.Count -ne $spec[1] -or @($rows|Where-Object status -ne PASS).Count){throw "Failed/incomplete report: $($spec[0])"}
    Write-Output "$($spec[0]): $($rows.Count) PASS"
}
