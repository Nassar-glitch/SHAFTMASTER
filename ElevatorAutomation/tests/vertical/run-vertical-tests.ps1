param([string]$AutoCADDirectory='C:\Program Files\Autodesk\AutoCAD 2023')
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output=Join-Path $root 'output/experimental-vertical'
$verification=Join-Path $output 'verification'
New-Item -ItemType Directory -Force -Path $verification | Out-Null
& (Join-Path $PSScriptRoot 'build-vertical.ps1')
$started=Get-Date
foreach($stage in @('generate-plot','resize','interactive','reopen-plot')) {
    $source=if($stage -eq 'reopen-plot'){Join-Path $output 'FullVertical_V1_Review.dwg'}else{Join-Path $root 'output/SectionBB_Baseline_Verified.dwg'}
    $inputCopy=Join-Path $verification ($stage+'-input.dwg')
    Copy-Item -LiteralPath $source -Destination $inputCopy -Force
    $script=(Get-Content -LiteralPath (Join-Path $PSScriptRoot ($stage+'.scr')) -Raw).Replace('{ROOT}',$root.Replace('\','/'))
    $scriptPath=Join-Path $verification ($stage+'.scr')
    Set-Content -LiteralPath $scriptPath -Value $script -Encoding ascii
    & (Join-Path $AutoCADDirectory 'accoreconsole.exe') /i $inputCopy /s $scriptPath /l en-US > (Join-Path $verification ($stage+'.log')) 2>&1
    if($LASTEXITCODE -ne 0){throw ('AutoCAD exited unsuccessfully: '+$stage)}
}
& (Join-Path $root 'bin-vertical-experiment/VerticalCoreTests.exe') (Join-Path $output 'review-native-geometry.json') (Join-Path $output 'vertical-core-results.json')
if($LASTEXITCODE -ne 0){throw 'Vertical Core regression failed'}
foreach($spec in @(@('vertical-native-results.json',19),@('vertical-resize-results.json',3),@('vertical-interactive-results.json',3),@('vertical-fresh-process-results.json',2),@('native-plot-results.json',6))) {
    $file=Get-Item -LiteralPath (Join-Path $output $spec[0])
    if($file.LastWriteTime -lt $started){throw ('Stale results: '+$file.Name)}
    $rows=@(Get-Content -LiteralPath $file.FullName -Raw|ConvertFrom-Json)
    if($rows.Count -ne $spec[1] -or @($rows|Where-Object status -ne 'PASS').Count){throw ('Incomplete/failed results: '+$file.Name)}
    Write-Output ($file.Name+': '+$rows.Count+' PASS')
}
