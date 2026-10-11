param([string]$CandidateBin='artifacts/bin',[string]$EvidenceDirectory)
# Tier 3 (D2 / D3): automatic doors in the native drawings. Generates and reopens four-view DWGs (2S 800 and 2C 800 on 1720 x 2800, 2S 700 on a
# 1300 shaft with the opening shifted, the user's own 2S 700 on 1320 x 1800, a left-handed 2S and a 2C 900 on 1900), then reads the door
# geometry back from the DWGs and compares it with the D1 DoorLayout numbers (tests/integration/verify_doors.py), renders the plans to PNG
# and checks the user's case against the user's own correction of the drawing (tests/integration/verify_user_edit.py).
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
& $compiler /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $bin 'DoorsNativeTests.exe')) @refs (Join-Path $root 'tests/solution/DoorsNativeTests.cs')
if($LASTEXITCODE){throw 'DoorsNativeTests build failed'}
& (Join-Path $bin 'DoorsNativeTests.exe') $root $bin $out
if($LASTEXITCODE){throw 'DoorsNativeTests failed'}
& $python -I (Join-Path $PSScriptRoot 'verify_doors.py') $out
if($LASTEXITCODE){throw 'Door geometry read back from the DWGs does not match the layout; see verify-doors.json'}
$userEdit=Join-Path $root 'docs/design/doors-d3/user-edit-2S-700-1320.dwg'
& $python -I (Join-Path $PSScriptRoot 'verify_user_edit.py') $userEdit (Join-Path $out '2S-700-1320.dwg') (Join-Path $out 'user-edit')
if($LASTEXITCODE){throw 'The generated drawing of the user case differs from the user edit; see user-edit/verify-user-edit.json'}
