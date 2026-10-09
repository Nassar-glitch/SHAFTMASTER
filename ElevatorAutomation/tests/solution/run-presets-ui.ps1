param([string]$CandidateBin='bin-shaftmaster-presets-candidate',[string]$EvidenceDirectory='output/presets-20261008/presets-ui')
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bin=Join-Path $root $CandidateBin;$out=Join-Path $root $EvidenceDirectory
if($CandidateBin -in @('bin-shaftmaster-v01','bin-shaftmaster-v02','bin-shaftmaster-basic-candidate','bin-shaftmaster-calculations-candidate','bin-shaftmaster-safety-pit-candidate','bin-shaftmaster-ff-candidate','bin-shaftmaster-calcsheet-candidate','bin-shaftmaster-ropes-candidate')){throw 'Use the separate presets candidate, not a release or a previous candidate.'}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$wpf=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/WPF'
$refs=@('/reference:System.Web.Extensions.dll','/reference:System.Xaml.dll',('/reference:'+(Join-Path $bin 'Elevator.Core.dll')),('/reference:'+(Join-Path $bin 'Elevator.Solution.Bridge.dll')),('/reference:'+(Join-Path $bin 'ShaftMaster.exe')))
foreach($a in @('PresentationCore','PresentationFramework','WindowsBase')){$refs+=('/reference:'+(Join-Path $wpf ($a+'.dll')))}
& $compiler /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $bin 'PresetsUiTests.exe')) @refs (Get-Item -LiteralPath (Join-Path $PSScriptRoot 'PresetsUiTests.cs')).FullName
if($LASTEXITCODE){throw 'Presets UI test build failed'}
& (Join-Path $bin 'PresetsUiTests.exe') $out
if($LASTEXITCODE){throw 'Presets UI tests failed; see ui-error.json'}
