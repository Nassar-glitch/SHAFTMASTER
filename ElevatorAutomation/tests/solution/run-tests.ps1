param([switch]$UnitOnly)
$ErrorActionPreference='Stop'
$testSource=$PSScriptRoot
$solutionRoot=Split-Path (Split-Path $testSource -Parent) -Parent
$bin=Join-Path $solutionRoot 'bin-solution-v01'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /target:exe "/out:$bin/SolutionTests.exe" /reference:System.Web.Extensions.dll "/reference:$bin/Elevator.Core.dll" "/reference:$bin/Elevator.Solution.Bridge.dll" (Join-Path $testSource 'SolutionTests.cs') (Join-Path $solutionRoot 'Elevator.Solution.Desktop/ProjectSession.cs')
if($LASTEXITCODE){throw 'Solution tests compile failed'}
if($UnitOnly){& "$bin/SolutionTests.exe" $solutionRoot --unit-only}else{& "$bin/SolutionTests.exe" $solutionRoot}
if($LASTEXITCODE){throw 'Solution tests failed'}
