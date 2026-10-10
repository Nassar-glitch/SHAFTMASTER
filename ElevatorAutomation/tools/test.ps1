param(
 [string[]]$Tier=@(),
 [string[]]$Suite=@(),
 [switch]$List,
 [string[]]$Framework=@(),
 [string]$Bin='artifacts/bin',
 [switch]$NoBuild,
 [switch]$Serial,
 [string]$RunFolder='',
 [switch]$Child)
# Test entry point (ARCHITECTURE-PLAN section 3, P7). One command for every test:
#   ./tools/test.ps1 -List                   the suites, their tier and what they need
#   ./tools/test.ps1 -Tier 1                 tier 1 (also what CI runs: on Linux only the net8.0 xUnit suites exist)
#   ./tools/test.ps1 -Tier 1,2,3             end of a phase / before a release
#   ./tools/test.ps1 -Suite rope-handcheck   a single suite (any tier); -Suite takes several names
# Tier 1 (no AutoCAD, no Word): the xUnit projects tests/Elevator.Core.Tests and tests/Elevator.Reporting.Tests on net8.0 and net48 (the
# golden-master Core captures, presets and Q sweep, rope / traction, FF, safety / pit, application services, serializer parity, the DOCX
# content of the calculation sheet and the Design Notes), the Python hand check of the rope / traction engine, and the two console programs
# that need WPF (golden figures, calculation-sheet content). Tier-1 suites are independent and run as parallel processes.
# Tier 2: programmatic WPF suites. Tier 3: AutoCAD, Word and STEP suites.
# Every run writes artifacts/test-results/<timestamp>/ (one folder per suite, logs, summary.json and summary.md).
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$onWindows=($env:OS -eq 'Windows_NT')
# Python 3 (the rope / traction hand check), found when it is needed; the Windows Store stub is not Python.
function FindPython(){
 foreach($candidate in 'python','python3'){
  $c=Get-Command $candidate -ErrorAction SilentlyContinue|Where-Object{$_.CommandType -eq 'Application'}|Select-Object -First 1
  if(-not $c){continue}
  $saved=$ErrorActionPreference;$ErrorActionPreference='Continue'
  try{$answer=& $c.Source -c 'print(1)' 2>&1|Out-String}catch{$answer=''}
  $ErrorActionPreference=$saved
  if($answer.Trim() -eq '1'){return $c.Source}
 }
 return $null
}

function Run([string]$script,[string[]]$arguments){& powershell -ExecutionPolicy Bypass -File (Join-Path $root $script) @arguments;if($LASTEXITCODE){throw "$script failed"}}
function XunitBuild(){
 foreach($project in 'Elevator.Core.Tests','Elevator.Reporting.Tests'){
  $csproj=Join-Path $root "tests/$project/$project.csproj"
  $extra=@();if(-not $onWindows){$extra=@('-f','net8.0')}
  & dotnet build $csproj -c Release -nologo -v:minimal @extra
  if($LASTEXITCODE){throw "$project build failed"}
 }
}
# One xUnit project on one framework: the TRX, the checks the converted suites counted (checks-<framework>.json) and the totals.
function Xunit([string]$project,[string]$fw,[string]$out,[bool]$handCheck){
 $csproj=Join-Path $root "tests/$project/$project.csproj"
 New-Item -ItemType Directory -Force -Path $out|Out-Null
 $env:ELEVATOR_TEST_OUT=$out;$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_NOLOGO='1'
 & dotnet test $csproj -c Release -f $fw --no-build --results-directory $out --logger "trx;LogFileName=$fw.trx" --logger 'console;verbosity=minimal'
 $code=$LASTEXITCODE
 $tests=0;$checks=0
 $trxFile=Join-Path $out "$fw.trx"
 if(Test-Path -LiteralPath $trxFile){[xml]$trx=Get-Content -LiteralPath $trxFile -Raw;$tests=[int]$trx.TestRun.ResultSummary.Counters.passed}
 $checksFile=Join-Path $out "$fw/checks-$fw.json"
 if(Test-Path -LiteralPath $checksFile){$checks=[int](Get-Content -LiteralPath $checksFile -Raw|ConvertFrom-Json).Checks}
 Write-Output ("{0} tests and {1} checks passed on {2}" -f $tests,$checks,$fw)
 if($code){throw "$project failed on $fw"}
 if($handCheck){HandCheck (Join-Path $out "$fw/rope-core")}
 return ($tests+$checks)
}
# The independent Python re-computation of the rope / traction fixtures written by RopeTractionTests (xUnit).
function HandCheck([string]$folder){
 $python=FindPython
 if(-not $python){throw 'Python 3 is required for the rope / traction hand check (verify_rope_traction.py).'}
 & $python (Join-Path $root 'tests/solution/verify_rope_traction.py') $folder
 if($LASTEXITCODE){throw 'Independent rope / traction hand check failed'}
}
function Count([string]$dir,[string]$log){
 # Assertions: the result files (*tests.json with Status and Assertions), else the "PASS: N ..." lines of the log.
 $n=0;$found=$false
 if(Test-Path -LiteralPath $dir){foreach($f in Get-ChildItem -LiteralPath $dir -Recurse -Filter '*tests.json' -ErrorAction SilentlyContinue){try{$j=Get-Content -LiteralPath $f.FullName -Raw|ConvertFrom-Json;if($j.Assertions -ne $null -and $j.Status){$n+=[int]$j.Assertions;$found=$true}}catch{}}}
 if(-not $found -and (Test-Path -LiteralPath $log)){foreach($m in (Select-String -LiteralPath $log -Pattern 'PASS: (\d+)' -AllMatches)){foreach($x in $m.Matches){$n+=[int]$x.Groups[1].Value}};foreach($m in (Select-String -LiteralPath $log -Pattern 'HAND CHECK PASS (\d+) fixtures')){$n+=[int]$m.Matches[0].Groups[1].Value}}
 return $n
}

# ---------------- the suites ----------------
# Name, tier, what it needs, what it checks, the body (param($o) = the suite's result folder relative to the root; may return an assertion count).
$suites=New-Object System.Collections.ArrayList
function AddSuite([int]$tier,[string]$name,[string]$needs,[string]$what,[scriptblock]$body,[string]$arg=''){[void]$suites.Add([pscustomobject]@{Tier=$tier;Name=$name;Needs=$needs;What=$what;Body=$body;Arg=$arg})}
$frameworks=@('net8.0');if($onWindows){$frameworks=@('net8.0','net48')}
foreach($fw in $frameworks){
 $f=$fw
 AddSuite 1 "core-$f" ".NET SDK$(if($f -eq 'net48'){', .NET Framework 4.8'}), Python 3" "tests/Elevator.Core.Tests on ${f}: golden Core captures, presets and Q sweep, rope / traction (+ hand check), FF, safety / pit, application services$(if($f -eq 'net48'){', serializer parity'})" {param($o,$s) Xunit 'Elevator.Core.Tests' $s.Arg (Join-Path $root $o) $true} $f
 AddSuite 1 "reporting-$f" ".NET SDK$(if($f -eq 'net48'){', .NET Framework 4.8'})" "tests/Elevator.Reporting.Tests on ${f}: calculation sheet and Design Notes content without Word" {param($o,$s) Xunit 'Elevator.Reporting.Tests' $s.Arg (Join-Path $root $o) $false} $f
}
if($onWindows){
 AddSuite 1 'golden' '.NET Framework, WPF (figure text)' 'tests/golden console: every approved file incl. figures (SVG), omitted calculations and figure errors' {param($o) Run 'tools/run-golden.ps1' @('-Bin',$Bin,'-OutDir',$o)}
 AddSuite 1 'sheet-wpf' '.NET Framework, WPF (figure text), no Word' 'tests/solution/CalculationSheetTests console: calculation-sheet content and layout rules (8285 assertions)' {param($o) Run 'tests/solution/run-calculation-sheet.ps1' @('-CandidateBin',$Bin,'-EvidenceDirectory',$o)}
 AddSuite 2 'presets-ui' 'desktop session' 'Presets in the real window' {param($o) Run 'tests/solution/run-presets-ui.ps1' @('-CandidateBin',$Bin,'-EvidenceDirectory',$o)}
 AddSuite 2 'rope-ui' 'desktop session' 'Rope / traction page in the real window' {param($o) Run 'tests/solution/run-rope-traction-ui.ps1' @('-CandidateBin',$Bin,'-EvidenceDirectory',$o)}
 AddSuite 2 'sheet-ui' 'desktop session' 'Calculation-sheet page' {param($o) Run 'tests/solution/run-calculation-sheet-ui.ps1' @('-CandidateBin',$Bin,'-EvidenceDirectory',$o)}
 AddSuite 2 'calculation-ui' 'desktop session, AutoCAD 2023' 'Calculation pages and native drawings' {param($o) Run 'tests/solution/run-calculation-ui.ps1' @('-CandidateBin',$Bin,'-SkipBuild','-EvidenceDirectory',$o)}
 AddSuite 2 'basic' 'desktop session, AutoCAD 2023' 'Basic UI and native drawings' {param($o) Run 'tests/solution/run-basic-ui.ps1' @('-CandidateBin',$Bin,'-SkipBuild','-EvidenceDirectory',$o)}
 AddSuite 2 'safety-pit-ui' 'desktop session' 'Safety gear / pit page (the Core part is in core-<framework>) and the project schema' {param($o) Run 'tests/solution/run-safety-pit-ui.ps1' @('-CandidateBin',$Bin,'-SkipBuild','-EvidenceDirectory',$o)}
 AddSuite 2 'safety-pit-draft-ui' 'desktop session' 'Safety gear / pit draft errors' {param($o) Run 'tests/solution/run-safety-pit-draft-ui.ps1' @('-CandidateBin',$Bin,'-EvidenceDirectory',$o)}
 AddSuite 2 'ff-ui' 'desktop session' 'Car guide force page' {param($o) Run 'tests/solution/run-ff-ui.ps1' @('-CandidateBin',$Bin,'-EvidenceDirectory',$o)}
 AddSuite 2 'doors-ui' 'desktop session' 'Car & Doors page: automatic door type, opening, handing, clearances' {param($o) Run 'tests/solution/run-doors-ui.ps1' @('-CandidateBin',$Bin,'-EvidenceDirectory',$o)}
 AddSuite 3 'integration' 'AutoCAD 2023, Python (PyMuPDF), local evidence folder' 'Affected regressions, FF native / STEP, STEP input, native golden' {param($o) Run 'tests/integration/run-tier3.ps1' @('-Bin',$Bin,'-OutDir',$o)}
 AddSuite 3 'presets-native' 'AutoCAD 2023' 'Presets through the native drawings' {param($o) Run 'tests/integration/run-native-test.ps1' @('-CandidateBin',$Bin,'-EvidenceDirectory',$o,'-Test','PresetsNativeTests')}
 AddSuite 3 'doors-native' 'AutoCAD 2023, Python (ezdxf)' 'Automatic doors in the four native views: DWG read back against the door layout, plan PNGs' {param($o) Run 'tests/integration/run-doors-native.ps1' @('-CandidateBin',$Bin,'-EvidenceDirectory',$o)}
 AddSuite 3 'doors-step' 'SOLIDWORKS' 'Automatic doors in the STEP model (real export of the 1720 mm cases)' {param($o) Run 'tests/integration/run-native-test.ps1' @('-CandidateBin',$Bin,'-EvidenceDirectory',$o,'-Test','DoorsStepTests','-TestArguments','--export')}
 AddSuite 3 'sheet-word-render' 'Microsoft Word, Python (PyMuPDF, python-docx)' 'Every calculation sheet rendered through Word to PDF' {param($o) Run 'tests/solution/run-calculation-sheet.ps1' @('-CandidateBin',$Bin,'-EvidenceDirectory',$o,'-WordRender')}
}

if($List){
 Write-Output ('{0,-5} {1,-22} {2}' -f 'Tier','Suite','Needs / what it checks')
 foreach($s in $suites){Write-Output ('{0,-5} {1,-22} {2}' -f $s.Tier,$s.Name,$s.Needs);Write-Output ('{0,-5} {1,-22}   {2}' -f '','',$s.What)}
 return
}
if((Split-Path $Bin -Leaf) -like 'bin-shaftmaster-v*'){throw 'Tests never run in a release folder.'}
# -Tier 1,2,3 arrives as one string through powershell -File: split on commas.
$tiers=@($Tier|ForEach-Object{$_ -split ','}|Where-Object{$_.Trim()}|ForEach-Object{[int]$_.Trim()}|Sort-Object -Unique)
$names=@($Suite|ForEach-Object{$_ -split ','}|Where-Object{$_.Trim()}|ForEach-Object{$_.Trim()})
if($Framework.Count){$wanted=@($Framework|ForEach-Object{$_ -split ','}|ForEach-Object{$_.Trim()});$suites=@($suites|Where-Object{($_.Name -notmatch '-(net8\.0|net48)$') -or ($wanted -contains ($_.Name -replace '^.*-(net8\.0|net48)$','$1'))})}
if(-not $tiers.Count -and -not $names.Count){$tiers=@(1)}
$selected=@($suites|Where-Object{($names.Count -and ($names -contains $_.Name)) -or (-not $names.Count -and ($tiers -contains $_.Tier))})
foreach($n in $names){if(-not ($suites|Where-Object{$_.Name -eq $n})){throw "Unknown suite '$n'. ./tools/test.ps1 -List shows the suites."}}
foreach($n in $tiers){if($n -lt 1 -or $n -gt 3){throw "Unknown tier $n (use 1, 2 or 3)."}}

$stamp=Get-Date -Format 'yyyyMMdd-HHmmss'
if($RunFolder){$runRel=$RunFolder}else{$runRel="artifacts/test-results/$stamp"}
$run=Join-Path $root $runRel
if(-not $Child){
 if(Test-Path -LiteralPath $run){throw "Run folder exists: $run"}
}
New-Item -ItemType Directory -Force -Path $run|Out-Null

# A child process runs one suite and writes its result file.
function RunSuite($s){
 $rel="$runRel/tier$($s.Tier)/$($s.Name)";$dir=Join-Path $root $rel;$log=Join-Path $run "tier$($s.Tier)-$($s.Name).log"
 $t=Get-Date;$ok=$true;$count=0;$global:LASTEXITCODE=0;$saved=$ErrorActionPreference;$ErrorActionPreference='Continue'
 New-Item -ItemType Directory -Force -Path $dir|Out-Null
 try{
  $out=& $s.Body $rel $s *>&1
  $out|Out-File -Encoding utf8 $log
  $number=@($out|Where-Object{$_ -is [int]})|Select-Object -Last 1
  if($number -ne $null){$count=[int]$number}
  if($LASTEXITCODE){$ok=$false}
 }catch{$ok=$false;$_|Out-File -Append -Encoding utf8 $log}
 $ErrorActionPreference=$saved
 if($count -eq 0){$count=Count $dir $log}
 return [pscustomobject]@{Tier=$s.Tier;Suite=$s.Name;Status=$(if($ok){'PASS'}else{'FAIL'});Assertions=$count;Seconds=[int]((Get-Date)-$t).TotalSeconds;Folder=$rel}
}
if($Child){
 $s=$suites|Where-Object{$_.Name -eq $names[0]}|Select-Object -First 1
 $r=RunSuite $s
 $r|ConvertTo-Json|Set-Content -Encoding utf8 (Join-Path $run "result-$($s.Name).json")
 if($r.Status -ne 'PASS'){exit 1}
 exit 0
}

$results=New-Object System.Collections.ArrayList
$times=[ordered]@{}
function Show($r){Write-Output ("  {0,-24} {1}  {2,6} assertions  {3,4}s" -f $r.Suite,$r.Status,$r.Assertions,$r.Seconds)}
$needBin=@($selected|Where-Object{$_.Name -notmatch '^(core|reporting)-'}).Count -gt 0
$needXunit=@($selected|Where-Object{$_.Name -match '^(core|reporting)-'}).Count -gt 0
if(-not $NoBuild){
 $t=Get-Date
 if($needBin){
  Write-Output "Build into $Bin"
  if(Get-Process ShaftMaster -ErrorAction SilentlyContinue|Where-Object{$_.Path -and $_.Path.StartsWith((Join-Path $root $Bin),[StringComparison]::OrdinalIgnoreCase)}){throw "ShaftMaster is running from $Bin; close it before the build."}
  & powershell -ExecutionPolicy Bypass -File (Join-Path $root 'tools/build.ps1') -Bin $Bin *> (Join-Path $run 'build.log')
  if($LASTEXITCODE){throw "Build failed; see $run/build.log"}
 }
 if($needXunit){Write-Output 'Build the xUnit projects';XunitBuild *> (Join-Path $run 'build-xunit.log');if($LASTEXITCODE){throw "xUnit build failed; see $run/build-xunit.log"}}
 $times['build']=[int]((Get-Date)-$t).TotalSeconds;Write-Output ("  build {0}s" -f $times['build'])
}
foreach($n in ($selected|ForEach-Object{$_.Tier}|Sort-Object -Unique)){
 $t=Get-Date;Write-Output "Tier $n"
 $group=@($selected|Where-Object{$_.Tier -eq $n})
 if($n -eq 1 -and -not $Serial -and $group.Count -gt 1){
  # Independent suites as parallel processes (each writes its own log and result file).
  $psExe=(Get-Process -Id $PID).Path
  $processes=@()
  foreach($s in $group){
   $argumentList=@('-NoProfile','-ExecutionPolicy','Bypass','-File',('"'+$PSCommandPath+'"'),'-Child','-Suite',$s.Name,'-NoBuild','-Bin',('"'+$Bin+'"'),'-RunFolder',('"'+$runRel+'"'))
   $processes+=[pscustomobject]@{Suite=$s;Process=(Start-Process -FilePath $psExe -ArgumentList $argumentList -PassThru -NoNewWindow -RedirectStandardOutput (Join-Path $run "child-$($s.Name).out") -RedirectStandardError (Join-Path $run "child-$($s.Name).err"))}
  }
  foreach($p in $processes){$p.Process.WaitForExit()}
  foreach($p in $processes){
   $file=Join-Path $run "result-$($p.Suite.Name).json"
   if(Test-Path -LiteralPath $file){$r=Get-Content -LiteralPath $file -Raw|ConvertFrom-Json}
   else{$r=[pscustomobject]@{Tier=$n;Suite=$p.Suite.Name;Status='FAIL';Assertions=0;Seconds=0;Folder="$runRel/tier$n/$($p.Suite.Name)"}}
   [void]$results.Add($r);Show $r
  }
 }else{
  foreach($s in $group){$r=RunSuite $s;[void]$results.Add($r);Show $r}
 }
 $times["tier$n"]=[int]((Get-Date)-$t).TotalSeconds;Write-Output ("Tier {0}: {1}s" -f $n,$times["tier$n"])
}
$failed=@($results|Where-Object Status -ne 'PASS')
$summary=[pscustomobject]@{Status=$(if($failed.Count){'FAIL'}else{'PASS'});Started=$stamp;Bin=$Bin;Seconds=$times;Suites=$results}
$summary|ConvertTo-Json -Depth 5|Set-Content -Encoding utf8 (Join-Path $run 'summary.json')
$md=@('| Tier | Suite | Result | Assertions | Time (s) |','|---|---|---|---|---|')+($results|ForEach-Object{"| $($_.Tier) | $($_.Suite) | $($_.Status) | $($_.Assertions) | $($_.Seconds) |"})+@('')+($times.Keys|ForEach-Object{"- $($_): $($times[$_]) s"})
$md|Set-Content -Encoding utf8 (Join-Path $run 'summary.md')
Write-Output ''
Write-Output ('{0,-5} {1,-22} {2,-6} {3,10} {4,8}' -f 'Tier','Suite','Result','Assertions','Time (s)')
foreach($r in $results){Write-Output ('{0,-5} {1,-22} {2,-6} {3,10} {4,8}' -f $r.Tier,$r.Suite,$r.Status,$r.Assertions,$r.Seconds)}
Write-Output ("Results: $run  ("+$summary.Status+")")
if($failed.Count){throw ('Failed suites: '+(($failed|ForEach-Object{"tier$($_.Tier)/$($_.Suite)"}) -join ', '))}
