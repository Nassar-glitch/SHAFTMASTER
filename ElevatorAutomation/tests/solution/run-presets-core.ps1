param([string]$CandidateBin='bin-shaftmaster-presets-candidate',[string]$EvidenceDirectory='output/presets-20261008/presets-core')
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bin=Join-Path $root $CandidateBin;$out=Join-Path $root $EvidenceDirectory
if($CandidateBin -in @('bin-shaftmaster-v01','bin-shaftmaster-v02')){throw 'Use a separate candidate; do not touch a release.'}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
# References Elevator.Reporting for CalculationWorkspace and DesignNotes (Q sweep recommendations).
$wpf=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/WPF'
$refs=@('/reference:System.Web.Extensions.dll','/reference:System.Xaml.dll',('/reference:'+(Join-Path $bin 'Elevator.Core.dll')),('/reference:'+(Join-Path $bin 'Elevator.Solution.Bridge.dll')),('/reference:'+(Join-Path $bin 'Elevator.Reporting.dll')),('/reference:'+(Join-Path $bin 'ShaftMaster.exe')))
foreach($a in @('PresentationCore','PresentationFramework','WindowsBase')){$refs+=('/reference:'+(Join-Path $wpf ($a+'.dll')))}
$sources=@((Get-Item -LiteralPath (Join-Path $root 'tests/solution/PresetsTests.cs')).FullName)
& $compiler /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $bin 'PresetsTests.exe')) @refs @sources
if($LASTEXITCODE){throw 'Presets tests build failed'}
& (Join-Path $bin 'PresetsTests.exe') $bin $out
if($LASTEXITCODE){throw 'Presets tests failed; see presets-error.json'}
