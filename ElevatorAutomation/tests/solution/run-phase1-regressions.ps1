$ErrorActionPreference='Stop'
$phaseTwoTestDir=$PSScriptRoot
$phaseTwoRoot=Split-Path (Split-Path $phaseTwoTestDir -Parent) -Parent
foreach($stage in @('run-phase1','run-additional-regressions')){
    $script=(Get-Content (Join-Path $phaseTwoRoot "tests/project/$stage.ps1") -Raw).Replace('output/phase1/regression','output/solution-v01/regression')
    $runner=Join-Path $phaseTwoTestDir ("generated-$stage.ps1")
    Set-Content -LiteralPath $runner -Value $script -Encoding UTF8
    & $runner *> (Join-Path $phaseTwoRoot "output/solution-v01/$stage.log")
}
Write-Output 'All Phase 1 suites completed in the Phase 2 isolation directory.'
