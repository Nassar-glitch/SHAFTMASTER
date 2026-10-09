param([string]$CandidateBin='bin-shaftmaster-calculations-candidate',[string]$EvidenceDirectory='output/calculation-ui-20261006/final-ui',[switch]$SkipBuild,[switch]$SkipUi)
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bin=Join-Path $root $CandidateBin
if([IO.Path]::GetFullPath($bin) -eq [IO.Path]::GetFullPath((Join-Path $root 'bin-shaftmaster-v01'))){throw 'Use a separate candidate package; do not rebuild the release.'}
$out=Join-Path $root $EvidenceDirectory
New-Item -ItemType Directory -Force -Path $out | Out-Null
if(!$SkipBuild){& (Join-Path $root 'tools/build.ps1') -Bin $CandidateBin}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$refs=@('/reference:System.Web.Extensions.dll',('/reference:'+(Join-Path $bin 'Elevator.Core.dll')),('/reference:'+(Join-Path $bin 'Elevator.Solution.Bridge.dll')),('/reference:'+(Join-Path $bin 'Elevator.Reporting.dll')))
$sources=@(@('tests/solution/CalculationWorkflowTests.cs')|ForEach-Object{(Get-Item -LiteralPath (Join-Path $root $_)).FullName})
& $compiler /nologo /target:exe ('/out:'+(Join-Path $bin 'CalculationWorkflowTests.exe')) @refs @sources
if($LASTEXITCODE){throw 'Calculation workflow test build failed'}
& (Join-Path $bin 'CalculationWorkflowTests.exe') $bin $out
if($LASTEXITCODE){throw 'Calculation workflow tests failed'}
if($SkipUi){return}
# Actual WPF controls and native generation, driven by programmatic events. This does
# not substitute for mouse/keyboard testing; the report explicitly labels the method.
$wpf=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/WPF'
$refs+=@('/reference:System.Xaml.dll',('/reference:'+(Join-Path $bin 'ShaftMaster.exe')))
foreach($a in @('PresentationCore','PresentationFramework','WindowsBase')){$refs+=('/reference:'+(Join-Path $wpf ($a+'.dll')))}
& $compiler /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $bin 'CalculationUiTests.exe')) @refs (Get-Item -LiteralPath (Join-Path $PSScriptRoot 'CalculationUiTests.cs')).FullName
if($LASTEXITCODE){throw 'Rendered calculation UI tests build failed'}
& (Join-Path $bin 'CalculationUiTests.exe') $out
if($LASTEXITCODE){throw 'Rendered calculation UI tests failed; see ui-error.json'}
