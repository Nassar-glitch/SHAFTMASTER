param([Parameter(Mandatory=$true)][string]$UiEvidenceDirectory)
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bin=Join-Path $sourceRoot 'bin-solution-v01'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$refs=@('/reference:System.Web.Extensions.dll',"/reference:$bin/Elevator.Core.dll","/reference:$bin/Elevator.AutoCAD.dll","/reference:$bin/Elevator.Solution.Bridge.dll","/reference:$bin/Elevator.Solution.Host.dll")
foreach($name in @('AcDbMgd','AcCoreMgd','AcMgd')){$refs+="/reference:C:/Program Files/Autodesk/AutoCAD 2023/$name.dll"}
& $compiler /nologo /target:library "/out:$bin/SolutionVisualQA.dll" @refs (Join-Path $PSScriptRoot 'SolutionVisualQA.cs')
if($LASTEXITCODE){throw 'Visual QA compile failed'}
foreach($case in @(@('baseline','AllFour_1450_1600.dwg','baseline.elevator.json'),@('unequal','Unequal_NO_STOP.dwg','unequal-no-stop.elevator.json'))){
    $out=Join-Path $UiEvidenceDirectory ('review-'+$case[0]);New-Item -ItemType Directory -Force $out|Out-Null
    Copy-Item -LiteralPath (Join-Path $UiEvidenceDirectory $case[2]) -Destination (Join-Path $out 'project.elevator.json') -Force
    $generated=Join-Path $UiEvidenceDirectory $case[1];Set-Content -LiteralPath (Join-Path $out 'generated-path.txt') -Value $generated -NoNewline
    foreach($stage in @('compare','plot')){
        $inputFile=Join-Path $out ($stage+'-input.dwg')
        $source=if($stage -eq 'compare'){Join-Path $sourceRoot 'output/section-aa/SectionAA_Review.dwg'}else{$generated}
        Copy-Item -LiteralPath $source -Destination $inputFile -Force
        $command=if($stage -eq 'compare'){'SOLUTIONVISUALQA'}else{'SOLUTIONPLOT'}
        $script=Join-Path $out ($stage+'.scr')
        @('FILEDIA','0','SECURELOAD','0','_.NETLOAD',('"'+(Join-Path $bin 'SolutionVisualQA.dll').Replace('\','/')+'"'),$command,$out,'_.QUIT','_N')|Set-Content $script -Encoding ascii
        & 'C:/Program Files/Autodesk/AutoCAD 2023/accoreconsole.exe' /i $inputFile /s $script /l en-US *> (Join-Path $out ($stage+'.log'))
        if($LASTEXITCODE){throw "$stage process failed"}
    }
    foreach($report in @('geometry-comparison.json','native-plots.json')){if(@(Get-Content (Join-Path $out $report) -Raw|ConvertFrom-Json|Where-Object Status -ne PASS).Count){throw "Visual QA failure in $out/$report"}}
}
