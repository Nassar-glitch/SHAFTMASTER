param([string]$CandidateBin='bin-shaftmaster-ff-candidate',[string]$EvidenceDirectory='output/ff-ui-20261006/core',[switch]$SkipBuild)
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bin=Join-Path $root $CandidateBin;$out=Join-Path $root $EvidenceDirectory
if($CandidateBin -in @('bin-shaftmaster-v01','bin-shaftmaster-basic-candidate','bin-shaftmaster-calculations-candidate','bin-shaftmaster-safety-pit-candidate')){throw 'Use a separate FF candidate folder.'}
New-Item -ItemType Directory -Force -Path $out | Out-Null
if(!$SkipBuild){& (Join-Path $root 'build-solution.ps1') -BinDirectory $CandidateBin}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$refs=@('/reference:System.Web.Extensions.dll',('/reference:'+(Join-Path $bin 'Elevator.Core.dll')),('/reference:'+(Join-Path $bin 'Elevator.Solution.Bridge.dll')))
$sources=@('tests/solution/CarGuideForceTests.cs','Elevator.Solution.Desktop/CalculationWorkspace.cs','Elevator.Solution.Desktop/ProjectSession.cs')|ForEach-Object{(Get-Item -LiteralPath (Join-Path $root $_)).FullName}
& $compiler /nologo /target:exe ('/out:'+(Join-Path $bin 'CarGuideForceTests.exe')) @refs @sources
if($LASTEXITCODE){throw 'Car guide force test build failed'}
& (Join-Path $bin 'CarGuideForceTests.exe') $bin $out $root
if($LASTEXITCODE){throw 'Car guide force tests failed'}
