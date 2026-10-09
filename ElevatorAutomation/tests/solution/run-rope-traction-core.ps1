param([string]$CandidateBin='bin-shaftmaster-ropes-candidate',[string]$EvidenceDirectory='output/ropes-20261008/core')
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bin=Join-Path $root $CandidateBin;$out=Join-Path $root $EvidenceDirectory
if($CandidateBin -eq 'bin-shaftmaster-v01' -or $CandidateBin -eq 'bin-shaftmaster-v02'){throw 'Use a separate candidate; do not touch a release.'}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$refs=@('/reference:System.Web.Extensions.dll',('/reference:'+(Join-Path $bin 'Elevator.Core.dll')),('/reference:'+(Join-Path $bin 'Elevator.Solution.Bridge.dll')))
$sources=@('tests/solution/RopeTractionTests.cs','Elevator.Solution.Desktop/CalculationWorkspace.cs','Elevator.Solution.Desktop/ProjectSession.cs')|ForEach-Object{(Get-Item -LiteralPath (Join-Path $root $_)).FullName}
& $compiler /nologo /target:exe ('/out:'+(Join-Path $bin 'RopeTractionTests.exe')) @refs @sources
if($LASTEXITCODE){throw 'Rope / traction tests build failed'}
& (Join-Path $bin 'RopeTractionTests.exe') $bin $out
if($LASTEXITCODE){throw 'Rope / traction tests failed; see rope-traction-error.json'}
python (Join-Path $PSScriptRoot 'verify_rope_traction.py') $out
if($LASTEXITCODE){throw 'Independent rope / traction hand check failed'}
