param([string]$AutoCADDirectory='C:\Program Files\Autodesk\AutoCAD 2023')
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$bin=Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $bin -Force|Out-Null
$sources=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -Recurse|ForEach-Object {$_.FullName})
$arguments=@('/nologo','/target:library',('/out:'+(Join-Path $bin 'ElevatorPlanGenerator.dll')),'/reference:System.Web.Extensions.dll')
foreach($assembly in @('AcDbMgd.dll','AcCoreMgd.dll','AcMgd.dll')) {$arguments+=('/reference:'+(Join-Path $AutoCADDirectory $assembly))}
& $compiler @arguments @sources
if($LASTEXITCODE -ne 0){throw 'C# build failed.'}
Write-Output (Join-Path $bin 'ElevatorPlanGenerator.dll')
