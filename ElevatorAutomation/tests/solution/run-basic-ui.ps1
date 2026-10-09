param([string]$EvidenceDirectory='output/basic-calculation-ui-20261006/ui',[string]$CandidateBin='bin-shaftmaster-basic-candidate',[switch]$SkipBuild)
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bin=Join-Path $root $CandidateBin;$out=Join-Path $root $EvidenceDirectory
if($CandidateBin -eq 'bin-shaftmaster-v01'){throw 'Use a separate candidate; do not rebuild the release.'}
New-Item -ItemType Directory -Force -Path $out | Out-Null
if(!$SkipBuild){& (Join-Path $root 'build-solution.ps1') -BinDirectory $CandidateBin}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$refs=@('/reference:System.Web.Extensions.dll',('/reference:'+(Join-Path $bin 'Elevator.Core.dll')),('/reference:'+(Join-Path $bin 'Elevator.Solution.Bridge.dll')))
$sources=@('tests/solution/SimplifiedTests.cs','Elevator.Solution.Desktop/CalculationWorkspace.cs','Elevator.Solution.Desktop/ProjectSession.cs')|ForEach-Object{(Get-Item -LiteralPath (Join-Path $root $_)).FullName}
& $compiler /nologo /target:exe ('/out:'+(Join-Path $bin 'SimplifiedTests.exe')) @refs @sources
if($LASTEXITCODE){throw 'Basic tests build failed'}
& (Join-Path $bin 'SimplifiedTests.exe') $bin $out
if($LASTEXITCODE){throw 'Basic tests failed'}
$wpf=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/WPF'
$refs+=@('/reference:System.Xaml.dll',('/reference:'+(Join-Path $bin 'ShaftMaster.exe')))
foreach($a in @('PresentationCore','PresentationFramework','WindowsBase')){$refs+=('/reference:'+(Join-Path $wpf ($a+'.dll')))}
& $compiler /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $bin 'BasicUiTests.exe')) @refs (Get-Item -LiteralPath (Join-Path $PSScriptRoot 'BasicUiTests.cs')).FullName
if($LASTEXITCODE){throw 'Basic UI tests build failed'}
& (Join-Path $bin 'BasicUiTests.exe') $out
if($LASTEXITCODE){throw 'Basic UI tests failed; see ui-error.json'}
