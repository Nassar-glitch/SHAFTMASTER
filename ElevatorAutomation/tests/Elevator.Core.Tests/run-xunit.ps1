param([string]$OutDir='',[string[]]$Framework=@('net8.0','net48'),[switch]$NoBuild,[string]$Project='Elevator.Core.Tests')
# Tier 1: an xUnit test project (-Project, default Elevator.Core.Tests; also Elevator.Reporting.Tests) on net8.0 and net48. Results (TRX per framework and
# xunit-tests.json with the totals) go to -OutDir (default artifacts/test-results/<timestamp>/xunit-core).
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if(-not $OutDir){$OutDir=Join-Path 'artifacts/test-results' ((Get-Date -Format 'yyyyMMdd-HHmmss')+'/xunit-core')}
$out=Join-Path $root $OutDir;New-Item -ItemType Directory -Force -Path $out | Out-Null
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_NOLOGO='1'
$project=Join-Path (Split-Path $PSScriptRoot -Parent) ($Project+'/'+$Project+'.csproj')
if(-not $NoBuild){& dotnet build $project -c Release -nologo -v:minimal;if($LASTEXITCODE){throw 'xUnit test build failed'}}
$totals=[ordered]@{};$failed=$false
foreach($f in $Framework){
 & dotnet test $project -c Release -f $f --no-build --results-directory $out --logger "trx;LogFileName=$f.trx" --logger 'console;verbosity=minimal'
 if($LASTEXITCODE){$failed=$true}
 [xml]$trx=Get-Content -LiteralPath (Join-Path $out "$f.trx") -Raw
 $c=$trx.TestRun.ResultSummary.Counters
 $totals[$f]=[ordered]@{Total=[int]$c.total;Passed=[int]$c.passed;Failed=[int]$c.failed;Outcome=$trx.TestRun.ResultSummary.outcome}
 Write-Output ("PASS: {0} xUnit tests on {1} ({2} failed)" -f $c.passed,$f,$c.failed)
}
$status=if($failed){'FAIL'}else{'PASS'}
[ordered]@{Status=$status;Assertions=($totals.Values|ForEach-Object{$_.Passed}|Measure-Object -Sum).Sum;Frameworks=$totals}|ConvertTo-Json -Depth 4|Set-Content -Encoding utf8 (Join-Path $out 'xunit-tests.json')
if($failed){throw "$Project failed"}
