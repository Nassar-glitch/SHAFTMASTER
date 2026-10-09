param([string]$AutoCADDirectory='C:\Program Files\Autodesk\AutoCAD 2023',[string]$BinDirectory='bin')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
& (Join-Path $root 'build.ps1') -AutoCADDirectory $AutoCADDirectory -BinDirectory $BinDirectory
$generated=Join-Path $PSScriptRoot 'generated'
New-Item -ItemType Directory -Path $generated -Force|Out-Null
$started=Get-Date
# The machine-room suite reads a local reference drawing kept outside the repository; without it the suite is skipped.
$machineRoomReference=Join-Path (Split-Path $root -Parent) 'Machine room codex.dwg'
$skip=@();if(-not (Test-Path -LiteralPath $machineRoomReference)){$skip+='machine-room';Write-Warning ('SKIPPED machine-room suite: local reference drawing not found (not in the repository): '+$machineRoomReference)}
foreach($test in @('regression','workflow','plan','machine-room','section','section-reopen')|Where-Object {$skip -notcontains $_}) {
    $source=if($test -eq 'regression'){Join-Path (Split-Path $root -Parent) 'legacy\ElevatorPlanGenerator\templates\plan codex.dwg'}elseif($test -eq 'section-reopen'){(Get-Content (Join-Path $PSScriptRoot 'section/case-16.json') -Raw|ConvertFrom-Json).DWG}else{Join-Path $root 'Templates\ExistingPlan_Master.dwg'}
    $inputDrawing=Join-Path $generated ($test+'-input.dwg')
    Copy-Item -LiteralPath $source -Destination $inputDrawing -Force
    $script=(Get-Content -LiteralPath (Join-Path $PSScriptRoot ($test+'.scr')) -Raw).Replace('{ROOT}',$root.Replace('\','/')).Replace('/bin/','/'+$BinDirectory+'/').Replace('/bin-section/','/'+$BinDirectory+'/')
    $scriptFile=Join-Path $generated ($test+'.scr')
    Set-Content -LiteralPath $scriptFile -Value $script -Encoding ascii
    & (Join-Path $AutoCADDirectory 'accoreconsole.exe') /i $inputDrawing /s $scriptFile /l en-US > (Join-Path $generated ($test+'.log')) 2>&1
    if($LASTEXITCODE -ne 0){throw "AutoCAD $test exited with $LASTEXITCODE"}
}
foreach($report in @(@('cad-test-results.json',17),@('architecture-results.json',14),@('workflow-results.json',1),@('plan-results.json',7),@('machine-room-results.json',32),@('section-results.json',32),@('section-reopen-results.json',2))|Where-Object {$skip -notcontains $_[0].Replace('-results.json','')}) {
    $file=Get-Item -LiteralPath (Join-Path $PSScriptRoot $report[0])
    if($file.LastWriteTime -lt $started){throw ('Stale report: '+$file.Name)}
    $rows=@(Get-Content -LiteralPath $file.FullName -Raw|ConvertFrom-Json)
    if($rows.Count -ne $report[1] -or @($rows|Where-Object status -ne 'PASS').Count -gt 0){throw ('Failed or incomplete report: '+$file.FullName)}
    Write-Output ($file.Name+': '+$rows.Count+' PASS, 0 FAIL')
}
& (Join-Path $PSScriptRoot 'section/summarize-results.ps1') -BinDirectory $BinDirectory -Skip $skip
