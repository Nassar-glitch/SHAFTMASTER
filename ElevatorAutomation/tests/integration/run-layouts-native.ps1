param([string]$CandidateBin='artifacts/bin',[string]$EvidenceDirectory)
# Tier 3 (L2b, suite layouts-native): the A0 geared sheet in generated drawings. Four projects are generated with a SheetRequest through the
# packaged GenerationClient and the AutoCAD 2023 Core Console; tests/integration/verify_layouts.py reopens the DWGs (DXFOUT + ezdxf) and checks
# the layout, every viewport (inside the drawing area, standard scale, locked, no overlap), every attribute against TitleBlockData and that
# the views are untouched.
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bin=Join-Path $root $CandidateBin;$out=Join-Path $root $EvidenceDirectory
if((Split-Path $CandidateBin -Leaf) -like 'bin-shaftmaster-v*'){throw 'Do not use a release folder.'}
if((Test-Path $out) -and @(Get-ChildItem $out -Force).Count){throw "Evidence folder not empty: $out"}
$python=$null
foreach($candidate in @((Join-Path $env:LOCALAPPDATA 'Python\pythoncore-3.14-64\python.exe'),(Join-Path $env:LOCALAPPDATA 'Python\bin\python.exe'),'python')){
 $c=Get-Command $candidate -ErrorAction SilentlyContinue|Where-Object{$_.CommandType -eq 'Application'}|Select-Object -First 1
 if(-not $c){continue}
 & $c.Source -c 'import ezdxf' 2>$null
 if($LASTEXITCODE -eq 0){$python=$c.Source;break}
}
if(-not $python){throw 'No Python with ezdxf found.'}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$refs=@('/reference:System.Web.Extensions.dll','/reference:System.Drawing.dll',('/reference:'+(Join-Path $bin 'Elevator.Core.dll')),('/reference:'+(Join-Path $bin 'Elevator.Solution.Bridge.dll')))
& $compiler /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $bin 'LayoutsNativeTests.exe')) @refs (Join-Path $root 'tests/solution/LayoutsNativeTests.cs')
if($LASTEXITCODE){throw 'LayoutsNativeTests build failed'}
& (Join-Path $bin 'LayoutsNativeTests.exe') $root $bin $out
if($LASTEXITCODE){throw 'LayoutsNativeTests failed'}
& $python -I (Join-Path $PSScriptRoot 'verify_layouts.py') $out (Join-Path $out 'verify-layouts.json')
if($LASTEXITCODE){throw 'The generated sheets read back from the DWGs do not match; see verify-layouts.json'}
