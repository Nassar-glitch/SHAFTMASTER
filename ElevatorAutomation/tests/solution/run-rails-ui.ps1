param([string]$CandidateBin='artifacts/bin',[string]$EvidenceDirectory='output/rails-ui')
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bin=Join-Path $root $CandidateBin;$out=Join-Path $root $EvidenceDirectory
if($CandidateBin -like 'bin-shaftmaster-v*'){throw 'Use a working build, not a release folder.'}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$wpf=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/WPF'
$refs=@('/reference:System.Web.Extensions.dll','/reference:System.Xaml.dll',('/reference:'+(Join-Path $bin 'Elevator.Core.dll')),('/reference:'+(Join-Path $bin 'Elevator.Solution.Bridge.dll')),('/reference:'+(Join-Path $bin 'Elevator.Reporting.dll')),('/reference:'+(Join-Path $bin 'ShaftMaster.exe')))
foreach($a in @('PresentationCore','PresentationFramework','WindowsBase')){$refs+=('/reference:'+(Join-Path $wpf ($a+'.dll')))}
& $compiler /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $bin 'RailsUiTests.exe')) @refs (Get-Item -LiteralPath (Join-Path $PSScriptRoot 'RailsUiTests.cs')).FullName
if($LASTEXITCODE){throw 'Rails UI test build failed'}
& (Join-Path $bin 'RailsUiTests.exe') $out
if($LASTEXITCODE){throw 'Rails UI tests failed; see ui-error.json'}
