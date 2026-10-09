param([string]$CandidateBin='bin-shaftmaster-safety-pit-candidate',[string]$EvidenceDirectory='output/safety-pit-ui-20261006/ui',[switch]$SkipBuild,[switch]$SkipUi)
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bin=Join-Path $root $CandidateBin;$out=Join-Path $root $EvidenceDirectory
if($CandidateBin -in @('bin-shaftmaster-v01','bin-shaftmaster-basic-candidate','bin-shaftmaster-calculations-candidate')){throw 'Use a separate safety/pit candidate folder.'}
New-Item -ItemType Directory -Force -Path $out | Out-Null
if(!$SkipBuild){& (Join-Path $root 'tools/build.ps1') -Bin $CandidateBin}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$refs=@('/reference:System.Web.Extensions.dll',('/reference:'+(Join-Path $bin 'Elevator.Core.dll')),('/reference:'+(Join-Path $bin 'Elevator.Solution.Bridge.dll')),('/reference:'+(Join-Path $bin 'Elevator.Reporting.dll')))
$sources=@(@('tests/solution/SafetyPitTests.cs')|ForEach-Object{(Get-Item -LiteralPath (Join-Path $root $_)).FullName})
& $compiler /nologo /target:exe ('/out:'+(Join-Path $bin 'SafetyPitTests.exe')) @refs @sources
if($LASTEXITCODE){throw 'Safety/pit tests build failed'}
& (Join-Path $bin 'SafetyPitTests.exe') $bin $out $root
if($LASTEXITCODE){throw 'Safety/pit calculation tests failed'}
$schemaSource=(Get-Item -LiteralPath (Join-Path $root 'tests/project/SchemaGenerator.cs')).FullName
& $compiler /nologo /target:exe ('/out:'+(Join-Path $bin 'SchemaGenerator.exe')) @refs $schemaSource
if($LASTEXITCODE){throw 'Schema generator build failed'}
& (Join-Path $bin 'SchemaGenerator.exe') (Join-Path $root 'config/ElevatorProject.schema.json')
if($LASTEXITCODE){throw 'Schema generation failed'}
if($SkipUi){return}
$wpf=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/WPF'
$refs+=@('/reference:System.Xaml.dll',('/reference:'+(Join-Path $bin 'ShaftMaster.exe')))
foreach($a in @('PresentationCore','PresentationFramework','WindowsBase')){$refs+=('/reference:'+(Join-Path $wpf ($a+'.dll')))}
& $compiler /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $bin 'SafetyPitUiTests.exe')) @refs (Get-Item -LiteralPath (Join-Path $PSScriptRoot 'SafetyPitUiTests.cs')).FullName
if($LASTEXITCODE){throw 'Rendered safety/pit UI tests build failed'}
& (Join-Path $bin 'SafetyPitUiTests.exe') $out
if($LASTEXITCODE){throw 'Rendered safety/pit UI tests failed; see ui-error.json'}
