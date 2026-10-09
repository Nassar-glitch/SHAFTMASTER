$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /target:exe ('/out:'+(Join-Path $PSScriptRoot 'RuleTests.exe')) /reference:System.Web.Extensions.dll (Join-Path $root 'src\ElevatorDesignRules\DesignRules.cs') (Join-Path $PSScriptRoot 'RuleTests.cs')
if($LASTEXITCODE -ne 0){throw 'Rule test build failed'}
& (Join-Path $PSScriptRoot 'RuleTests.exe') (Join-Path $root 'config\equipment-profile.json')
if($LASTEXITCODE -ne 0){throw 'Rule tests failed'}
$refs=@('/reference:System.Web.Extensions.dll',('/reference:'+(Join-Path $root 'bin\ElevatorPlanGenerator.dll')))
foreach($a in @('AcDbMgd.dll','AcCoreMgd.dll','AcMgd.dll')){$refs+=('/reference:C:\Program Files\Autodesk\AutoCAD 2023\'+$a)}
& $csc /nologo /target:library ('/out:'+(Join-Path $root 'bin\ElevatorPlanGenerator.Tests.dll')) @refs (Join-Path $PSScriptRoot 'CadIntegrationTests.cs')
if($LASTEXITCODE -ne 0){throw 'CAD test build failed'}
