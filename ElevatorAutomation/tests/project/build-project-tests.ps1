param([string]$BinDirectory='bin-phase1')
$ErrorActionPreference='Stop'
$projectTestDirectory=$PSScriptRoot
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
& (Join-Path $root 'tools/build.ps1') -Bin $BinDirectory
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$bin=Join-Path $root $BinDirectory
$core=Join-Path $bin 'Elevator.Core.dll'
& $compiler /nologo /target:exe /reference:System.Web.Extensions.dll "/reference:$core" "/out:$bin/ProjectTests.exe" (Join-Path $projectTestDirectory 'ProjectTests.cs')
if($LASTEXITCODE -ne 0){throw 'Project tests compile failed'}
& "$bin/ProjectTests.exe" "$root/config/equipment-profile.json" "$root/examples/ElevatorProject.example.json"
if($LASTEXITCODE -ne 0){throw 'Project tests failed'}
& $compiler /nologo /target:exe /reference:System.Web.Extensions.dll "/reference:$core" "/out:$bin/SchemaGenerator.exe" (Join-Path $projectTestDirectory 'SchemaGenerator.cs')
if($LASTEXITCODE -ne 0){throw 'Schema generator compile failed'}
& "$bin/SchemaGenerator.exe" "$root/config/ElevatorProject.schema.json"
$refs=@('/reference:System.Web.Extensions.dll',"/reference:$core","/reference:$bin/Elevator.AutoCAD.dll")
foreach($name in @('AcDbMgd.dll','AcCoreMgd.dll','AcMgd.dll')){$refs+="/reference:C:/Program Files/Autodesk/AutoCAD 2023/$name"}
& $compiler /nologo /target:library "/out:$bin/ProjectNativeTests.dll" @refs (Join-Path $projectTestDirectory 'ProjectNativeTests.cs')
if($LASTEXITCODE -ne 0){throw 'Native project tests compile failed'}
