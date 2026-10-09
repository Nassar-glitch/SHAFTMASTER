param([string]$Bin='artifacts/bin',[string]$OutDir='',[switch]$ApproveNative)
# Tier-3 integration runner (ARCHITECTURE-PLAN P0): needs AutoCAD 2023 and the local evidence folders.
#   affected-regressions  rules, machine room, reeving, project, 200 calculation assertions, four-view snapshots equal to the
#                         release Core (bin-shaftmaster-v01, read only) and the 32 protected files;
#   ff-integration        FF native generate / reopen and STEP parameters (CarGuideForceIntegrationTests);
#   step-input            STEP input and parameter tests (StepExportTests, no SOLIDWORKS session);
#   native-golden         the default new project generated natively: entity counts per view and the reopened project JSON
#                         compared with tests/golden/approved-native (-ApproveNative: initial baseline only).
# Each step writes into its own folder under -OutDir; the runner fails if any step fails.
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$binPath=Join-Path $root $Bin
if((Split-Path $Bin -Leaf) -like 'bin-shaftmaster-v*'){throw 'Do not use a release folder.'}
if(-not $OutDir){$OutDir=Join-Path 'artifacts/test-results' ((Get-Date -Format 'yyyyMMdd-HHmmss')+'/tier3-integration')}
$out=Join-Path $root $OutDir
if((Test-Path -LiteralPath $out) -and @(Get-ChildItem -LiteralPath $out -Force).Count){throw "Output folder is not empty: $out"}
New-Item -ItemType Directory -Force -Path $out | Out-Null
$failed=@()
function Step([string]$name,[scriptblock]$body){
 $t=Get-Date;$ok=$true;$global:LASTEXITCODE=0;$saved=$ErrorActionPreference;$ErrorActionPreference='Continue'
 try{& $body *>&1 | Tee-Object -FilePath (Join-Path $out ($name+'.log')) | Out-Null;if($LASTEXITCODE){$ok=$false}}catch{$ok=$false;$_ | Out-File -Append (Join-Path $out ($name+'.log'))}
 $ErrorActionPreference=$saved
 $state=if($ok){'PASS'}else{'FAIL'};if(-not $ok){$script:failed+=$name}
 Write-Output ("{0,-22} {1} ({2}s)" -f $name,$state,[int]((Get-Date)-$t).TotalSeconds)
 Get-Content (Join-Path $out ($name+'.log')) | Select-String -Pattern '^PASS: |^FAIL: ' | ForEach-Object {'   '+$_.Line}
}
Step 'affected-regressions' {powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'run-affected-regressions.ps1') -CandidateBin $Bin -EvidenceDirectory (Join-Path $OutDir 'affected-regressions')}
Step 'ff-integration' {powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'run-native-test.ps1') -CandidateBin $Bin -EvidenceDirectory (Join-Path $OutDir 'ff-integration') -Test CarGuideForceIntegrationTests}
Step 'step-input' {
 $stepOut=Join-Path $out 'step-input'
 $o=& (Join-Path $binPath 'StepExportTests.exe') $binPath $stepOut
 $o
 if($LASTEXITCODE){throw 'STEP input tests failed'}
 Write-Output ('PASS: '+@($o | Select-String -Pattern '"Status":"PASS"').Count+' STEP input assertions.')
}
Step 'native-golden' {
 $compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
 $refs=@('/reference:System.Web.Extensions.dll',('/reference:'+(Join-Path $binPath 'Elevator.Core.dll')),('/reference:'+(Join-Path $binPath 'Elevator.Solution.Bridge.dll')))
 & $compiler /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $binPath 'NativeGoldenTests.exe')) @refs (Join-Path $PSScriptRoot 'NativeGoldenTests.cs') (Join-Path $root 'tests/golden/JsonMembers.cs')
 if($LASTEXITCODE){throw 'Native golden build failed'}
 & (Join-Path $binPath 'NativeGoldenTests.exe') $root $binPath (Join-Path $out 'native-golden') $(if($ApproveNative){'approve'}else{'compare'})
 if($LASTEXITCODE){throw 'Native golden tests failed'}
}
if($failed.Count){throw ('Tier-3 integration steps failed: '+($failed -join ', '))}
Write-Output 'Tier-3 integration: all steps PASS.'
