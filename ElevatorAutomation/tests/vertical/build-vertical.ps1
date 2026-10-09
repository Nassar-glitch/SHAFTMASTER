param([switch]$SkipMainBuild)
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if(-not $SkipMainBuild){& (Join-Path $root 'tools/build.ps1') -Bin bin-vertical-experiment}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$bin=Join-Path $root 'bin-vertical-experiment'
$refs=@('/reference:System.Web.Extensions.dll')
foreach($name in @('Elevator.Core.dll','Elevator.AutoCAD.dll')){$refs+=('/reference:'+(Join-Path $bin $name))}
foreach($name in @('AcDbMgd.dll','AcCoreMgd.dll','AcMgd.dll')){$refs+=('/reference:'+(Join-Path 'C:\Program Files\Autodesk\AutoCAD 2023' $name))}
& $compiler /nologo /target:library ('/out:'+(Join-Path $bin 'Elevator.Vertical.Tests.dll')) @refs (Join-Path $PSScriptRoot 'VerticalTests.cs')
if($LASTEXITCODE -ne 0){throw 'Vertical native test build failed'}
& $compiler /nologo /target:exe ('/out:'+(Join-Path $bin 'VerticalCoreTests.exe')) /reference:System.Web.Extensions.dll ('/reference:'+(Join-Path $bin 'Elevator.Core.dll')) (Join-Path $PSScriptRoot 'VerticalCoreTests.cs')
if($LASTEXITCODE -ne 0){throw 'Vertical Core test build failed'}
