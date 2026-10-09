param([string]$AutoCADDirectory='C:\Program Files\Autodesk\AutoCAD 2023')
$ErrorActionPreference='Stop'
# The solution build is tools/build.ps1; this script then adds the Section A-A test libraries.
& (Join-Path $PSScriptRoot 'tools\build.ps1') -Bin 'bin-section-aa' -AutoCADDir $AutoCADDirectory
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$bin=Join-Path $PSScriptRoot 'bin-section-aa'
$refs=@('/r:System.Web.Extensions.dll',('/r:'+(Join-Path $bin 'Elevator.Core.dll')),('/r:'+(Join-Path $bin 'Elevator.AutoCAD.dll')))
foreach($name in @('AcDbMgd.dll','AcCoreMgd.dll','AcMgd.dll')){$refs+=('/r:'+(Join-Path $AutoCADDirectory $name))}
foreach($name in @('AATests','AAPlot')){
 & $compiler /nologo /target:library ('/out:'+(Join-Path $bin ($name+'.dll'))) @refs (Join-Path $PSScriptRoot ('tests\section-aa\'+$name+'.cs'))
 if($LASTEXITCODE -ne 0){throw "Failed $name"}
}
