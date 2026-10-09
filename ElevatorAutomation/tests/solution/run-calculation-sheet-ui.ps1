param([string]$CandidateBin='bin-shaftmaster-calcsheet-candidate',[string]$EvidenceDirectory='output/calcsheet-20261007/ui')
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bin=Join-Path $root $CandidateBin;$out=Join-Path $root $EvidenceDirectory
if($CandidateBin -in @('bin-shaftmaster-v01','bin-shaftmaster-basic-candidate','bin-shaftmaster-calculations-candidate','bin-shaftmaster-safety-pit-candidate','bin-shaftmaster-ff-candidate')){throw 'Use the separate calculation-sheet candidate, not the release or a previous candidate.'}
New-Item -ItemType Directory -Force -Path $out | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$wpf=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/WPF'
$refs=@('/reference:System.Web.Extensions.dll','/reference:System.Xaml.dll',('/reference:'+(Join-Path $bin 'Elevator.Core.dll')),('/reference:'+(Join-Path $bin 'Elevator.Solution.Bridge.dll')),('/reference:'+(Join-Path $bin 'ShaftMaster.exe')))
foreach($a in @('PresentationCore','PresentationFramework','WindowsBase')){$refs+=('/reference:'+(Join-Path $wpf ($a+'.dll')))}
& $compiler /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $bin 'CalculationSheetUiTests.exe')) @refs (Get-Item -LiteralPath (Join-Path $PSScriptRoot 'CalculationSheetUiTests.cs')).FullName
if($LASTEXITCODE){throw 'Calculation sheet UI test build failed'}
& (Join-Path $bin 'CalculationSheetUiTests.exe') $out
if($LASTEXITCODE){throw 'Calculation sheet UI tests failed; see ui-error.json'}
