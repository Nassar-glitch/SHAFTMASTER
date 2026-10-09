param([string]$AutoCADDirectory='C:\Program Files\Autodesk\AutoCAD 2023',[string]$Python='C:\Users\ABDULLAH NASSAR\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe')
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$out=Join-Path $root 'output\calculation-phase1'
$bin=Join-Path $out 'bin'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Force -Path $bin | Out-Null
function Compile([string]$name,[string]$kind,[string[]]$sources,[string[]]$refs){ & $compiler /nologo ('/target:'+$kind) ('/out:'+(Join-Path $bin $name)) @refs @sources; if($LASTEXITCODE -ne 0){throw ('Build failed: '+$name)} }
$coreSources=@(Get-ChildItem -LiteralPath (Join-Path $root 'Elevator.Core') -Filter '*.cs' -Recurse | ForEach-Object FullName)
Compile 'Elevator.Core.dll' 'library' $coreSources @('/reference:System.Web.Extensions.dll')
$coreRefs=@('/reference:System.Web.Extensions.dll',('/reference:'+(Join-Path $bin 'Elevator.Core.dll')))
foreach($test in @('RuleTests','MachineRoomRuleTests','ReevingTests'))
{
    Compile ($test+'.exe') 'exe' @((Join-Path $root ('tests\'+$test+'.cs'))) $coreRefs
    & (Join-Path $bin ($test+'.exe')) (Join-Path $root 'config\equipment-profile.json')
    if($LASTEXITCODE -ne 0){throw ('Test failed: '+$test)}
}
Compile 'ProjectTests.exe' 'exe' @((Join-Path $root 'tests\project\ProjectTests.cs')) $coreRefs
& (Join-Path $bin 'ProjectTests.exe') (Join-Path $root 'config\equipment-profile.json') (Join-Path $out 'project-roundtrip-test.json')
if($LASTEXITCODE -ne 0){throw 'Project tests failed'}
Compile 'CalculationTests.exe' 'exe' @((Join-Path $PSScriptRoot 'CalculationTests.cs')) $coreRefs
& (Join-Path $bin 'CalculationTests.exe') (Join-Path $out 'native-evidence-fixture.json') (Join-Path $out 'reference-plan.json') (Join-Path $out 'native-geometry.json') (Join-Path $root 'examples\ElevatorProject.example.json') (Join-Path $root 'config\equipment-profile.json')
if($LASTEXITCODE -ne 0){throw 'Calculation tests failed'}
$cadRefs=$coreRefs
foreach($assembly in @('AcDbMgd.dll','AcCoreMgd.dll','AcMgd.dll')){$cadRefs+=('/reference:'+(Join-Path $AutoCADDirectory $assembly))}
Compile 'Elevator.AutoCAD.dll' 'library' @(Get-ChildItem -LiteralPath (Join-Path $root 'Elevator.AutoCAD') -Filter '*.cs' -Recurse | ForEach-Object FullName) $cadRefs
Compile 'SchemaGenerator.exe' 'exe' @((Join-Path $root 'tests\project\SchemaGenerator.cs')) $coreRefs
& (Join-Path $bin 'SchemaGenerator.exe') (Join-Path $out 'ElevatorProject.schema.json')
if($LASTEXITCODE -ne 0){throw 'Schema generation failed'}
Compile 'DrawingRegressionProbe.exe' 'exe' @((Join-Path $PSScriptRoot 'DrawingRegressionProbe.cs')) $coreRefs
& (Join-Path $bin 'DrawingRegressionProbe.exe') (Join-Path $out 'reference-plan.json') (Join-Path $root 'examples\ElevatorProject.example.json') (Join-Path $root 'config\equipment-profile.json') (Join-Path $out 'new-drawing-snapshots.json') (Join-Path $out 'native-evidence-fixture.json')
if($LASTEXITCODE -ne 0){throw 'New drawing snapshot generation failed'}
$baseline=Join-Path $out 'baseline-bin';New-Item -ItemType Directory -Force -Path $baseline | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'bin-shaftmaster-v01\Elevator.Core.dll') -Destination (Join-Path $baseline 'Elevator.Core.dll')
& $compiler /nologo /target:exe ('/out:'+(Join-Path $baseline 'DrawingRegressionProbe.exe')) /reference:System.Web.Extensions.dll ('/reference:'+(Join-Path $baseline 'Elevator.Core.dll')) (Join-Path $PSScriptRoot 'DrawingRegressionProbe.cs')
if($LASTEXITCODE -ne 0){throw 'Release baseline probe build failed'}
& (Join-Path $baseline 'DrawingRegressionProbe.exe') (Join-Path $out 'reference-plan.json') (Join-Path $root 'examples\ElevatorProject.example.json') (Join-Path $root 'config\equipment-profile.json') (Join-Path $out 'release-drawing-snapshots.json')
if($LASTEXITCODE -ne 0){throw 'Release baseline failed'}
& $Python (Join-Path $PSScriptRoot 'verify_outputs.py')
if($LASTEXITCODE -ne 0){throw 'Four-view comparison/protected-file verification failed'}
Write-Output 'PASS: all four calculated views equal to release Core for 1450/1600, 1650/1800, 1850/2000. Release EXE was not rebuilt.'
