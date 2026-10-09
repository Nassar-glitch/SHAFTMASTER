$ErrorActionPreference='Stop'
$projectTestsDirectory=$PSScriptRoot
$sourceRoot=Split-Path (Split-Path $projectTestsDirectory -Parent) -Parent
$workspaceRoot=Split-Path $sourceRoot -Parent
$isolation=Join-Path $sourceRoot 'output/solution-v01/regression'
$isolatedRoot=Join-Path $isolation 'ElevatorAutomation'
New-Item -ItemType Directory -Force $isolatedRoot,(Join-Path $isolation 'legacy/ElevatorPlanGenerator/tests'),(Join-Path $isolation 'legacy/ElevatorPlanGenerator/templates'),(Join-Path $isolation 'legacy/ElevatorPlanGenerator/output') | Out-Null
foreach($dir in @('Elevator.Core','Elevator.AutoCAD','Elevator.Solution.Bridge','Elevator.Solution.Host','Elevator.Solution.StepExport','Elevator.Solution.Desktop','config','Templates','tests','examples','tools','docs')){Copy-Item -LiteralPath (Join-Path $sourceRoot $dir) -Destination $isolatedRoot -Recurse -Force}
foreach($file in @('build.ps1','ElevatorAutomation.sln','Directory.Build.props')){Copy-Item -LiteralPath (Join-Path $sourceRoot $file) -Destination $isolatedRoot -Force}
Copy-Item -LiteralPath (Join-Path $workspaceRoot 'legacy/ElevatorPlanGenerator/tests/cad-test-results.json') -Destination (Join-Path $isolation 'legacy/ElevatorPlanGenerator/tests') -Force
Copy-Item -LiteralPath (Join-Path $workspaceRoot 'legacy/ElevatorPlanGenerator/templates/plan codex.dwg') -Destination (Join-Path $isolation 'legacy/ElevatorPlanGenerator/templates') -Force
$goldens=Get-Content (Join-Path $workspaceRoot 'legacy/ElevatorPlanGenerator/tests/cad-test-results.json') -Raw|ConvertFrom-Json
foreach($r in $goldens|Where-Object kind -eq 'current-drawing'){$leaf=Split-Path $r.output -Leaf;Copy-Item -LiteralPath (Join-Path $workspaceRoot "legacy/ElevatorPlanGenerator/output/$leaf") -Destination (Join-Path $isolation "legacy/ElevatorPlanGenerator/output/$leaf") -Force}
# The root project drawings are local files, not in the repository: copy those present; tests that need a missing one skip with a message.
foreach($name in @('plan codex.dwg','Machine room codex.dwg','Vert MR and Reeving codex.dwg')){$local=Join-Path $workspaceRoot $name;if(Test-Path -LiteralPath $local){Copy-Item -LiteralPath $local -Destination $isolation -Force}else{Write-Warning ('Local project drawing not found (not in the repository): '+$local)}}
& (Join-Path $isolatedRoot 'tests/run-tests.ps1') -BinDirectory bin-phase1
& (Join-Path $isolatedRoot 'tests/project/build-project-tests.ps1') -BinDirectory bin-phase1
$out=Join-Path $isolatedRoot 'output/phase1'
New-Item -ItemType Directory -Force $out | Out-Null
$nativeHost=Join-Path $out 'native-host.dwg'
Copy-Item -LiteralPath (Join-Path $sourceRoot 'output/section-aa/SectionAA_Review.dwg') -Destination $nativeHost -Force
$core='C:/Program Files/Autodesk/AutoCAD 2023/accoreconsole.exe'
foreach($stage in @('run-native','reopen','create')){
    $script=(Get-Content (Join-Path $isolatedRoot "tests/project/$stage.scr") -Raw).Replace('{ROOT}',$isolatedRoot.Replace('\','/'))
    $scriptPath=Join-Path $out "$stage.scr"
    Set-Content -LiteralPath $scriptPath -Value $script -Encoding ascii
    if($stage -eq 'reopen'){Copy-Item -LiteralPath (Join-Path $out 'Unified_Reopen.dwg') -Destination $nativeHost -Force}
    if($stage -eq 'create'){Copy-Item -LiteralPath (Join-Path $isolatedRoot 'Templates/ExistingPlan_Master.dwg') -Destination $nativeHost -Force}
    & $core /i $nativeHost /s $scriptPath /l en-US *> (Join-Path $out "$stage.log")
    if($LASTEXITCODE -ne 0){throw "Native stage failed: $stage"}
}
foreach($spec in @(@('project-native-tests.json',6),@('project-reopen-tests.json',1),@('project-create-tests.json',2))){
    $rows=@(Get-Content (Join-Path $out $spec[0]) -Raw|ConvertFrom-Json)
    if($rows.Count -ne $spec[1] -or @($rows|Where-Object status -ne PASS).Count){throw "Failed/incomplete report: $($spec[0])"}
    Write-Output "$($spec[0]): $($rows.Count) PASS"
}

