param([string]$CandidateBin='artifacts/bin',[string]$EvidenceDirectory,[string]$Test,[string[]]$TestArguments=@())
# Tier 3. Runs a packaged-candidate native test (tests/solution/<Test>.cs: CarGuideForceIntegrationTests, PresetsNativeTests, ReportFieldsNativeTests or NativeGolden) against any non-release candidate,
# into a fresh evidence folder. Same compile/run steps as tests/solution/run-ff-integration.ps1 without its FF-candidate guard.
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bin=Join-Path $root $CandidateBin;$out=Join-Path $root $EvidenceDirectory
if((Split-Path $CandidateBin -Leaf) -like 'bin-shaftmaster-v*'){throw 'Do not use a release folder.'}
if((Test-Path $out) -and @(Get-ChildItem $out -Force).Count){throw "Evidence folder not empty: $out"}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$refs=@('/reference:System.Web.Extensions.dll',('/reference:'+(Join-Path $bin 'Elevator.Core.dll')),('/reference:'+(Join-Path $bin 'Elevator.Solution.Bridge.dll')))
& $compiler /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $bin ($Test+'.exe'))) @refs (Join-Path $root ('tests/solution/'+$Test+'.cs'))
if($LASTEXITCODE){throw "$Test build failed"}
& (Join-Path $bin ($Test+'.exe')) $root $bin $out @TestArguments
if($LASTEXITCODE){throw "$Test failed"}
