param([string[]]$Tier=@('1'),[string]$Bin='artifacts/bin',[switch]$NoBuild)
# Test entry point (ARCHITECTURE-PLAN section 3; skeleton for P7). Calls the existing runners by tier:
#   1  golden master, presets / rope / calculation-sheet / FF / safety-pit core suites, xUnit Core tests on net8.0 and net48
#      (no AutoCAD, no Word);
#   2  programmatic WPF suites;
#   3  native AutoCAD, Word render, STEP-input and affected-regression suites.
# Usage: ./tools/test.ps1 -Tier 1   or   ./tools/test.ps1 -Tier 1,2,3
# Builds into -Bin first (tools/build.ps1) unless -NoBuild. Every run writes into a fresh folder
# artifacts/test-results/<timestamp>/ (one subfolder per suite, logs, summary.json and summary.md) and prints the time per tier.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if((Split-Path $Bin -Leaf) -like 'bin-shaftmaster-v*'){throw 'Tests never run in a release folder.'}
$stamp=Get-Date -Format 'yyyyMMdd-HHmmss'
$runRel="artifacts/test-results/$stamp";$run=Join-Path $root $runRel
if(Test-Path -LiteralPath $run){throw "Run folder exists: $run"}
New-Item -ItemType Directory -Force -Path $run | Out-Null
$suites=New-Object System.Collections.ArrayList
function Count([string]$dir,[string]$log){
 # Assertions: the result files (*tests.json with Status and Assertions), else the "PASS: N ..." lines of the log.
 $n=0;$found=$false
 if(Test-Path -LiteralPath $dir){foreach($f in Get-ChildItem -LiteralPath $dir -Recurse -Filter '*tests.json' -ErrorAction SilentlyContinue){try{$j=Get-Content -LiteralPath $f.FullName -Raw|ConvertFrom-Json;if($j.Assertions -ne $null -and $j.Status){$n+=[int]$j.Assertions;$found=$true}}catch{}}}
 if(-not $found -and (Test-Path -LiteralPath $log)){foreach($m in (Select-String -LiteralPath $log -Pattern 'PASS: (\d+)' -AllMatches)){foreach($x in $m.Matches){$n+=[int]$x.Groups[1].Value}};foreach($m in (Select-String -LiteralPath $log -Pattern 'HAND CHECK PASS (\d+) fixtures')){$n+=[int]$m.Matches[0].Groups[1].Value}}
 return $n
}
function Suite([int]$tierNumber,[string]$name,[scriptblock]$body){
 $rel="$runRel/tier$tierNumber/$name";$dir=Join-Path $root $rel;$log=Join-Path $run "tier$tierNumber-$name.log"
 $t=Get-Date;$ok=$true;$global:LASTEXITCODE=0;$saved=$ErrorActionPreference;$ErrorActionPreference='Continue'
 try{& $body $rel *>&1 | Out-File -Encoding utf8 $log;if($LASTEXITCODE){$ok=$false}}catch{$ok=$false;$_ | Out-File -Append -Encoding utf8 $log}
 $ErrorActionPreference=$saved
 $s=[pscustomobject]@{Tier=$tierNumber;Suite=$name;Status=$(if($ok){'PASS'}else{'FAIL'});Assertions=(Count $dir $log);Seconds=[int]((Get-Date)-$t).TotalSeconds;Folder=$rel}
 [void]$suites.Add($s)
 Write-Output ("  {0,-24} {1}  {2,6} assertions  {3,4}s" -f $name,$s.Status,$s.Assertions,$s.Seconds)
}
function Run([string]$script,[string[]]$arguments){& powershell -ExecutionPolicy Bypass -File (Join-Path $root $script) @arguments;if($LASTEXITCODE){throw "$script failed"}}
$times=[ordered]@{}
if(-not $NoBuild){
 $t=Get-Date;Write-Output "Build into $Bin"
 if(Get-Process ShaftMaster -ErrorAction SilentlyContinue | Where-Object {$_.Path -and $_.Path.StartsWith((Join-Path $root $Bin),[StringComparison]::OrdinalIgnoreCase)}){throw "ShaftMaster is running from $Bin; close it before the build."}
 & powershell -ExecutionPolicy Bypass -File (Join-Path $root 'tools/build.ps1') -Bin $Bin *> (Join-Path $run 'build.log')
 if($LASTEXITCODE){throw "Build failed; see $run/build.log"}
 $times['build']=[int]((Get-Date)-$t).TotalSeconds;Write-Output ("  build {0}s" -f $times['build'])
}
# -Tier 1,2,3 arrives as one string through powershell -File: split on commas.
$tiers=@($Tier | ForEach-Object {$_ -split ','} | Where-Object {$_.Trim()} | ForEach-Object {[int]$_.Trim()} | Sort-Object -Unique)
foreach($n in $tiers){
 $t=Get-Date;Write-Output "Tier $n"
 switch($n){
  1{
   Suite 1 'golden' {param($o) Run 'tools/run-golden.ps1' @('-Bin',$Bin,'-OutDir',$o)}
   Suite 1 'presets-core' {param($o) Run 'tests/solution/run-presets-core.ps1' @('-CandidateBin',$Bin,'-EvidenceDirectory',$o)}
   Suite 1 'rope-core' {param($o) Run 'tests/solution/run-rope-traction-core.ps1' @('-CandidateBin',$Bin,'-EvidenceDirectory',$o)}
   Suite 1 'sheet-core' {param($o) Run 'tests/solution/run-calculation-sheet.ps1' @('-CandidateBin',$Bin,'-EvidenceDirectory',$o)}
   Suite 1 'ff-core' {param($o) Run 'tests/solution/run-car-guide-force-core.ps1' @('-CandidateBin',$Bin,'-SkipBuild','-EvidenceDirectory',$o)}
   Suite 1 'safety-pit-core' {param($o) Run 'tests/solution/run-safety-pit-ui.ps1' @('-CandidateBin',$Bin,'-SkipBuild','-SkipUi','-EvidenceDirectory',$o)}
   Suite 1 'xunit-core' {param($o) & (Join-Path $root 'tests/Elevator.Core.Tests/run-xunit.ps1') -OutDir $o;if($LASTEXITCODE){throw 'xUnit Core tests failed'}}
  }
  2{
   Suite 2 'presets-ui' {param($o) Run 'tests/solution/run-presets-ui.ps1' @('-CandidateBin',$Bin,'-EvidenceDirectory',$o)}
   Suite 2 'rope-ui' {param($o) Run 'tests/solution/run-rope-traction-ui.ps1' @('-CandidateBin',$Bin,'-EvidenceDirectory',$o)}
   Suite 2 'sheet-ui' {param($o) Run 'tests/solution/run-calculation-sheet-ui.ps1' @('-CandidateBin',$Bin,'-EvidenceDirectory',$o)}
   Suite 2 'calculation-ui' {param($o) Run 'tests/solution/run-calculation-ui.ps1' @('-CandidateBin',$Bin,'-SkipBuild','-EvidenceDirectory',$o)}
   Suite 2 'basic' {param($o) Run 'tests/solution/run-basic-ui.ps1' @('-CandidateBin',$Bin,'-SkipBuild','-EvidenceDirectory',$o)}
   Suite 2 'safety-pit-ui' {param($o) Run 'tests/solution/run-safety-pit-ui.ps1' @('-CandidateBin',$Bin,'-SkipBuild','-EvidenceDirectory',$o)}
   Suite 2 'safety-pit-draft-ui' {param($o) Run 'tests/solution/run-safety-pit-draft-ui.ps1' @('-CandidateBin',$Bin,'-EvidenceDirectory',$o)}
   Suite 2 'ff-ui' {param($o) Run 'tests/solution/run-ff-ui.ps1' @('-CandidateBin',$Bin,'-EvidenceDirectory',$o)}
  }
  3{
   Suite 3 'integration' {param($o) Run 'tests/integration/run-tier3.ps1' @('-Bin',$Bin,'-OutDir',$o)}
   Suite 3 'presets-native' {param($o) Run 'tests/integration/run-native-test.ps1' @('-CandidateBin',$Bin,'-EvidenceDirectory',$o,'-Test','PresetsNativeTests')}
   Suite 3 'sheet-word-render' {param($o) Run 'tests/solution/run-calculation-sheet.ps1' @('-CandidateBin',$Bin,'-EvidenceDirectory',$o,'-WordRender')}
  }
  default{throw "Unknown tier $n (use 1, 2 or 3)."}
 }
 $times["tier$n"]=[int]((Get-Date)-$t).TotalSeconds;Write-Output ("Tier {0}: {1}s" -f $n,$times["tier$n"])
}
$failed=@($suites|Where-Object Status -ne 'PASS')
$summary=[pscustomobject]@{Status=$(if($failed.Count){'FAIL'}else{'PASS'});Started=$stamp;Bin=$Bin;Seconds=$times;Suites=$suites}
$summary|ConvertTo-Json -Depth 5|Set-Content -Encoding utf8 (Join-Path $run 'summary.json')
$md=@('| Tier | Suite | Result | Assertions | Time (s) |','|---|---|---|---|---|')+($suites|ForEach-Object{"| $($_.Tier) | $($_.Suite) | $($_.Status) | $($_.Assertions) | $($_.Seconds) |"})+@('')+($times.Keys|ForEach-Object{"- $($_): $($times[$_]) s"})
$md|Set-Content -Encoding utf8 (Join-Path $run 'summary.md')
Write-Output ("Results: $run  ("+$summary.Status+")")
if($failed.Count){throw ('Failed suites: '+(($failed|ForEach-Object{"tier$($_.Tier)/$($_.Suite)"}) -join ', '))}
