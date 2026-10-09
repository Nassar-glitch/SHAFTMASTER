param([string]$CandidateBin='bin-shaftmaster-ff-candidate',[string]$EvidenceDirectory='output/ff-ui-20261006/integration')
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bin=Join-Path $root $CandidateBin;$out=Join-Path $root $EvidenceDirectory
if($CandidateBin -ne 'bin-shaftmaster-ff-candidate'){throw 'Use the separate FF candidate for this integration regression.'}
New-Item -ItemType Directory -Force -Path $out | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$refs=@('/reference:System.Web.Extensions.dll',('/reference:'+(Join-Path $bin 'Elevator.Core.dll')),('/reference:'+(Join-Path $bin 'Elevator.Solution.Bridge.dll')))
& $compiler /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $bin 'CarGuideForceIntegrationTests.exe')) @refs (Get-Item -LiteralPath (Join-Path $PSScriptRoot 'CarGuideForceIntegrationTests.cs')).FullName
if($LASTEXITCODE){throw 'FF native/STEP integration test build failed'}
& (Join-Path $bin 'CarGuideForceIntegrationTests.exe') $root $bin $out
if($LASTEXITCODE){throw 'FF native/STEP integration tests failed; see integration-error.json'}
