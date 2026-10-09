param([string]$AutoCADDirectory='C:\Program Files\Autodesk\AutoCAD 2023',[string]$BinDirectory='bin')
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$bin=Join-Path $PSScriptRoot $BinDirectory
New-Item -ItemType Directory -Path $bin -Force|Out-Null
$core=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Elevator.Core') -Filter '*.cs' -Recurse|ForEach-Object {$_.FullName})
& $compiler /nologo /target:library /reference:System.Web.Extensions.dll ('/out:'+(Join-Path $bin 'Elevator.Core.dll')) @core
if($LASTEXITCODE -ne 0){throw 'Core build failed'}
$refs=@('/reference:System.Web.Extensions.dll',('/reference:'+(Join-Path $bin 'Elevator.Core.dll')))
foreach($assembly in @('AcDbMgd.dll','AcCoreMgd.dll','AcMgd.dll')) {$refs+=('/reference:'+(Join-Path $AutoCADDirectory $assembly))}
$sources=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Elevator.AutoCAD') -Filter '*.cs' -Recurse|ForEach-Object {$_.FullName})
& $compiler /nologo /target:library ('/out:'+(Join-Path $bin 'Elevator.AutoCAD.dll')) @refs @sources
if($LASTEXITCODE -ne 0){throw 'AutoCAD build failed'}
& $compiler /nologo /target:exe ('/out:'+(Join-Path $bin 'RuleTests.exe')) /reference:System.Web.Extensions.dll ('/reference:'+(Join-Path $bin 'Elevator.Core.dll')) (Join-Path $PSScriptRoot 'tests\RuleTests.cs')
if($LASTEXITCODE -ne 0){throw 'Rule tests build failed'}
& (Join-Path $bin 'RuleTests.exe') (Join-Path $PSScriptRoot 'config\equipment-profile.json')
if($LASTEXITCODE -ne 0){throw 'Rule tests failed'}
& $compiler /nologo /target:exe ('/out:'+(Join-Path $bin 'MachineRoomRuleTests.exe')) /reference:System.Web.Extensions.dll ('/reference:'+(Join-Path $bin 'Elevator.Core.dll')) (Join-Path $PSScriptRoot 'tests\MachineRoomRuleTests.cs')
if($LASTEXITCODE -ne 0){throw 'Machine room rule tests build failed'}
& (Join-Path $bin 'MachineRoomRuleTests.exe') (Join-Path $PSScriptRoot 'config\equipment-profile.json')
if($LASTEXITCODE -ne 0){throw 'Machine room rule tests failed'}
$refs+=('/reference:'+(Join-Path $bin 'Elevator.AutoCAD.dll'))
& $compiler /nologo /target:exe ('/out:'+(Join-Path $bin 'ReevingTests.exe')) ('/reference:'+(Join-Path $bin 'Elevator.Core.dll')) (Join-Path $PSScriptRoot 'tests\ReevingTests.cs')
if($LASTEXITCODE -ne 0){throw 'Reeving tests build failed'}
& (Join-Path $bin 'ReevingTests.exe')
if($LASTEXITCODE -ne 0){throw 'Reeving tests failed'}
$tests=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter '*.cs'|Where-Object Name -notin @('RuleTests.cs','MachineRoomRuleTests.cs','ReevingTests.cs','SolutionRoofRegression.cs')|ForEach-Object {$_.FullName})
& $compiler /nologo /target:library ('/out:'+(Join-Path $bin 'Elevator.Automation.Tests.dll')) @refs @tests
if($LASTEXITCODE -ne 0){throw 'CAD tests build failed'}
Write-Output 'Build and pure design-rule tests passed.'
