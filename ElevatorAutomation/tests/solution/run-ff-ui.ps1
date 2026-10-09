param([string]$CandidateBin='bin-shaftmaster-ff-candidate',[string]$EvidenceDirectory='output/ff-ui-20261006/wpf')
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bin=Join-Path $root $CandidateBin;$out=Join-Path $root $EvidenceDirectory
if($CandidateBin -in @('bin-shaftmaster-v01','bin-shaftmaster-basic-candidate','bin-shaftmaster-calculations-candidate','bin-shaftmaster-safety-pit-candidate')){throw 'Use the separate FF candidate, not a previous candidate or the release.'}
New-Item -ItemType Directory -Force -Path $out | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$wpf=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/WPF'
$refs=@('/reference:System.Web.Extensions.dll','/reference:System.Xaml.dll',('/reference:'+(Join-Path $bin 'Elevator.Core.dll')),('/reference:'+(Join-Path $bin 'Elevator.Solution.Bridge.dll')),('/reference:'+(Join-Path $bin 'Elevator.Reporting.dll')),('/reference:'+(Join-Path $bin 'ShaftMaster.exe')))
foreach($a in @('PresentationCore','PresentationFramework','WindowsBase')){$refs+=('/reference:'+(Join-Path $wpf ($a+'.dll')))}
& $compiler /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $bin 'CarGuideForceUiTests.exe')) @refs (Get-Item -LiteralPath (Join-Path $PSScriptRoot 'CarGuideForceUiTests.cs')).FullName
if($LASTEXITCODE){throw 'FF rendered WPF test build failed'}
& (Join-Path $bin 'CarGuideForceUiTests.exe') $out
if($LASTEXITCODE){throw 'FF rendered WPF tests failed; see ui-error.json'}
