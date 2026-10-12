param([string]$CandidateBin='artifacts/bin',[string]$EvidenceDirectory)
# Layout spike (L2b), tier 3 tooling, not part of a test tier: generates the four-view DWGs of three projects (2, 6 and 15 stops, shaft
# 1600 x 1800) with the packaged GenerationClient and the AutoCAD 2023 Core Console, reads every view's model extents back
# (tests/integration/view_extents.py) and writes them to docs/design/layouts/view-extents.json.
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
$refs=@('/reference:System.Web.Extensions.dll',('/reference:'+(Join-Path $bin 'Elevator.Core.dll')),('/reference:'+(Join-Path $bin 'Elevator.Solution.Bridge.dll')))
& $compiler /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $bin 'ViewExtentsSpike.exe')) @refs (Join-Path $root 'tests/solution/ViewExtentsSpike.cs')
if($LASTEXITCODE){throw 'ViewExtentsSpike build failed'}
& (Join-Path $bin 'ViewExtentsSpike.exe') $root $bin $out
if($LASTEXITCODE){throw 'ViewExtentsSpike failed'}
& $python -I (Join-Path $PSScriptRoot 'view_extents.py') $out (Join-Path $root 'docs/design/layouts/view-extents.json')
if($LASTEXITCODE){throw 'view_extents.py failed'}
