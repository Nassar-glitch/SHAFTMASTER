param([string]$AutoCADDirectory='C:\Program Files\Autodesk\AutoCAD 2023')
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$out=Join-Path $root 'output\calculation-phase1';$bin=Join-Path $out 'bin'
$refs=@('/reference:System.Web.Extensions.dll',('/reference:'+(Join-Path $bin 'Elevator.Core.dll')),('/reference:'+(Join-Path $bin 'Elevator.AutoCAD.dll')))
foreach($assembly in @('AcDbMgd.dll','AcCoreMgd.dll','AcMgd.dll')){$refs+=('/reference:'+(Join-Path $AutoCADDirectory $assembly))}
& (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe') /nologo /target:library ('/out:'+(Join-Path $bin 'NativeGeometryReadTests.dll')) @refs (Join-Path $PSScriptRoot 'NativeGeometryReadTests.cs')
if($LASTEXITCODE -ne 0){throw 'Native reader test build failed'}
$hostFile=Join-Path $out 'native-readonly-host.dwg'
$source=Join-Path $root 'output\shaftmaster-ui\ShaftMaster_UI_FourViews.dwg'
$before=(Get-FileHash -LiteralPath $source).Hash
Copy-Item -LiteralPath $source -Destination $hostFile -Force
$script=@('SECURELOAD','0')
foreach($assembly in @('Elevator.Core.dll','Elevator.AutoCAD.dll','NativeGeometryReadTests.dll')){$script+=@('_NETLOAD',('"'+(Join-Path $bin $assembly).Replace('\','/')+'"'))}
$script+=@('CALCPHASE1READONLY','_QUIT','_N','')
$scriptFile=Join-Path $out 'native-readonly.scr';Set-Content -LiteralPath $scriptFile -Value $script -Encoding ascii
$resultFile=Join-Path $out 'native-readonly-result.json'
if(Test-Path -LiteralPath $resultFile){Remove-Item -LiteralPath $resultFile}
& (Join-Path $AutoCADDirectory 'accoreconsole.exe') /i $hostFile /s $scriptFile /l en-US *> (Join-Path $out 'native-readonly.log')
if($LASTEXITCODE -ne 0){throw 'Native host failed'}
$result=Get-Content -LiteralPath (Join-Path $out 'native-readonly-result.json') -Raw | ConvertFrom-Json
if($result.Status -ne 'PASS'){throw $result.Error}
if((Get-FileHash -LiteralPath $source).Hash -ne $before){throw 'Native source drawing changed'}
Write-Output 'PASS: AutoCAD native semantic reader; four contact seats/three members; missing project calculation inputs remain unavailable. Source DWG unchanged.'
