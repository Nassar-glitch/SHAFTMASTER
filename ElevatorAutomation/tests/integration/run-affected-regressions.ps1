param([string]$CandidateBin='artifacts/bin',[string]$EvidenceDirectory)
# Tier 3. Affected-regression set as in output/ff-source-review-20261006, rerun against the built Elevator.Core.dll
# into a fresh folder: rule / annotation(in RuleTests) / machine-room / reeving / project tests, 200 calculation assertions,
# four-view snapshots versus the release Core, and the 32 protected artifacts.
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$cand=Join-Path $root $CandidateBin;$out=Join-Path $root $EvidenceDirectory;$bin=Join-Path $out 'bin'
if((Test-Path $out) -and @(Get-ChildItem $out -Force).Count){throw "Evidence folder not empty: $out"}
New-Item -ItemType Directory -Force -Path $bin | Out-Null
# Reviewed native evidence and protected-file hashes are local files (output/calculation-phase1), not in the repository.
$fixtures=Join-Path $root 'output\calculation-phase1'
if(-not (Test-Path -LiteralPath (Join-Path $fixtures 'protected-hashes.json'))){throw ('Local evidence folder not found: '+$fixtures+'. The affected-regression suite needs the reviewed native evidence of the calculation phase 1.')}
foreach($f in 'native-evidence-fixture.json','reference-plan.json','native-geometry.json','protected-hashes.json','native-readonly-result.json'){Copy-Item -LiteralPath (Join-Path $fixtures $f) -Destination (Join-Path $out $f)}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
function Compile([string]$name,[string]$kind,[string[]]$sources,[string[]]$refs){ & $compiler /nologo ('/target:'+$kind) ('/out:'+(Join-Path $bin $name)) @refs @sources; if($LASTEXITCODE -ne 0){throw ('Build failed: '+$name)} }
Copy-Item -LiteralPath (Join-Path $cand 'Elevator.Core.dll') -Destination (Join-Path $bin 'Elevator.Core.dll')
$coreRefs=@('/reference:System.Web.Extensions.dll',('/reference:'+(Join-Path $bin 'Elevator.Core.dll')))
foreach($test in @('RuleTests','MachineRoomRuleTests','ReevingTests'))
{
    Compile ($test+'.exe') 'exe' @((Join-Path $root ('tests\'+$test+'.cs'))) $coreRefs
    & (Join-Path $bin ($test+'.exe')) (Join-Path $root 'config\equipment-profile.json')
    if($LASTEXITCODE -ne 0){throw ('Test failed: '+$test)}
}
Compile 'ProjectTests.exe' 'exe' @((Join-Path $root 'tests\project\ProjectTests.cs')) $coreRefs
& (Join-Path $bin 'ProjectTests.exe') (Join-Path $root 'config\equipment-profile.json') (Join-Path $out 'project-roundtrip-test.json')
if($LASTEXITCODE -ne 0){throw 'Project tests failed'}
Compile 'CalculationTests.exe' 'exe' @((Join-Path $root 'tests\calculations\CalculationTests.cs')) $coreRefs
& (Join-Path $bin 'CalculationTests.exe') (Join-Path $out 'native-evidence-fixture.json') (Join-Path $out 'reference-plan.json') (Join-Path $out 'native-geometry.json') (Join-Path $root 'examples\ElevatorProject.example.json') (Join-Path $root 'config\equipment-profile.json')
if($LASTEXITCODE -ne 0){throw 'Calculation tests failed'}
Compile 'DrawingRegressionProbe.exe' 'exe' @((Join-Path $root 'tests\calculations\DrawingRegressionProbe.cs')) $coreRefs
& (Join-Path $bin 'DrawingRegressionProbe.exe') (Join-Path $out 'reference-plan.json') (Join-Path $root 'examples\ElevatorProject.example.json') (Join-Path $root 'config\equipment-profile.json') (Join-Path $out 'new-drawing-snapshots.json') (Join-Path $out 'native-evidence-fixture.json')
if($LASTEXITCODE -ne 0){throw 'Candidate drawing snapshot generation failed'}
$baseline=Join-Path $out 'baseline-bin';New-Item -ItemType Directory -Force -Path $baseline | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'bin-shaftmaster-v01\Elevator.Core.dll') -Destination (Join-Path $baseline 'Elevator.Core.dll')
& $compiler /nologo /target:exe ('/out:'+(Join-Path $baseline 'DrawingRegressionProbe.exe')) /reference:System.Web.Extensions.dll ('/reference:'+(Join-Path $baseline 'Elevator.Core.dll')) (Join-Path $root 'tests\calculations\DrawingRegressionProbe.cs')
if($LASTEXITCODE -ne 0){throw 'Release baseline probe build failed'}
& (Join-Path $baseline 'DrawingRegressionProbe.exe') (Join-Path $out 'reference-plan.json') (Join-Path $root 'examples\ElevatorProject.example.json') (Join-Path $root 'config\equipment-profile.json') (Join-Path $out 'release-drawing-snapshots.json')
if($LASTEXITCODE -ne 0){throw 'Release baseline failed'}
$env:RC_OUT=$out;$env:RC_ROOT=$root
python (Join-Path $PSScriptRoot 'verify_affected.py')
if($LASTEXITCODE -ne 0){throw 'Four-view comparison/protected-file verification failed'}
Write-Output 'PASS: all four calculated views equal to release Core for 1450/1600, 1650/1800, 1850/2000 (candidate Core).'
