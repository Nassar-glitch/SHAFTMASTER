$ErrorActionPreference='Stop'
$aaSourceRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$aaRoot=Join-Path $aaSourceRoot 'output/phase1/regression/ElevatorAutomation'
$aaOut=Join-Path $aaRoot 'output/section-aa'
New-Item -ItemType Directory -Force $aaOut | Out-Null
Copy-Item -LiteralPath (Join-Path $aaSourceRoot 'output/SectionBB_Baseline_Verified.dwg') -Destination (Join-Path $aaRoot 'output/SectionBB_Baseline_Verified.dwg') -Force
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$bin=Join-Path $aaRoot 'bin-phase1'
$refs=@('/reference:System.Web.Extensions.dll',"/reference:$bin/Elevator.Core.dll","/reference:$bin/Elevator.AutoCAD.dll")
foreach($name in @('AcDbMgd.dll','AcCoreMgd.dll','AcMgd.dll')){$refs+="/reference:C:/Program Files/Autodesk/AutoCAD 2023/$name"}
& $compiler /nologo /target:library "/out:$bin/AATests.dll" @refs (Join-Path $aaRoot 'tests/section-aa/AATests.cs')
if($LASTEXITCODE -ne 0){throw 'A-A test build failed'}
foreach($stage in @('matrix','reopen','install')){
    $source=if($stage -eq 'matrix'){Join-Path $aaSourceRoot 'output/section-aa/SectionAA_Review.dwg'}elseif($stage -eq 'reopen'){Join-Path $aaOut 'SectionAA_Review.dwg'}else{Join-Path $aaSourceRoot 'output/SectionBB_Baseline_Verified.dwg'}
    $hostFile=Join-Path $aaOut "$stage-host.dwg";Copy-Item -LiteralPath $source -Destination $hostFile -Force
    $script=(Get-Content (Join-Path $aaSourceRoot "tests/section-aa/$stage.scr") -Raw).Replace($aaSourceRoot.Replace('\','/'),$aaRoot.Replace('\','/')).Replace('/bin-section-aa/','/bin-phase1/')
    $scriptFile=Join-Path $aaOut "$stage.scr";Set-Content -LiteralPath $scriptFile -Value $script -Encoding ascii
    & 'C:/Program Files/Autodesk/AutoCAD 2023/accoreconsole.exe' /i $hostFile /s $scriptFile /l en-US *> (Join-Path $aaOut "$stage.log")
    if($LASTEXITCODE -ne 0){throw "A-A $stage failed"}
}
foreach($spec in @(@('native-tests.json',30),@('fresh-reopen-tests.json',1),@('installation-tests.json',4))){$r=@(Get-Content (Join-Path $aaOut $spec[0]) -Raw|ConvertFrom-Json);if($r.Count -ne $spec[1] -or @($r|Where-Object status -ne PASS).Count){throw "A-A report failed $($spec[0])"};Write-Output "$($spec[0]): $($r.Count) PASS"}
& (Join-Path $aaRoot 'tests/vertical/run-vertical-tests.ps1')
