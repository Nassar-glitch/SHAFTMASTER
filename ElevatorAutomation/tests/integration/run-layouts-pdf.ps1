param([string]$CandidateBin='artifacts/bin',[string]$EvidenceDirectory,[string]$PngDirectory='docs/design/layouts/l3')
# Tier 3 (L3, suite layouts-pdf): the output set of one Generate Drawings run (DWG with the sheet, PDF of the sheet, project file; one base
# name) through the packaged OutputSetWriter and the AutoCAD 2023 Core Console. Two projects: 6 stops with a logo and an Arabic company,
# Automatic 2S without a logo. tests/integration/verify_pdf.py reads the PDFs (PyMuPDF): one page of 1189 x 841 mm, the project name and
# "P1 =" in the text, the logo image object only when a logo is set; page 1 is rendered to PNG in docs/design/layouts/l3.
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bin=Join-Path $root $CandidateBin;$out=Join-Path $root $EvidenceDirectory;$png=Join-Path $root $PngDirectory
if((Split-Path $CandidateBin -Leaf) -like 'bin-shaftmaster-v*'){throw 'Do not use a release folder.'}
if((Test-Path $out) -and @(Get-ChildItem $out -Force).Count){throw "Evidence folder not empty: $out"}
$python=$null
foreach($candidate in @((Join-Path $env:LOCALAPPDATA 'Python\pythoncore-3.14-64\python.exe'),(Join-Path $env:LOCALAPPDATA 'Python\bin\python.exe'),'python')){
 $c=Get-Command $candidate -ErrorAction SilentlyContinue|Where-Object{$_.CommandType -eq 'Application'}|Select-Object -First 1
 if(-not $c){continue}
 & $c.Source -c 'import pymupdf' 2>$null
 if($LASTEXITCODE -eq 0){$python=$c.Source;break}
}
if(-not $python){throw 'No Python with PyMuPDF found.'}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$refs=@('/reference:System.Web.Extensions.dll','/reference:System.Drawing.dll',('/reference:'+(Join-Path $bin 'Elevator.Core.dll')),('/reference:'+(Join-Path $bin 'Elevator.Solution.Bridge.dll')))
& $compiler /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $bin 'LayoutsPdfTests.exe')) @refs (Join-Path $root 'tests/solution/LayoutsPdfTests.cs')
if($LASTEXITCODE){throw 'LayoutsPdfTests build failed'}
& (Join-Path $bin 'LayoutsPdfTests.exe') $root $bin $out
if($LASTEXITCODE){throw 'LayoutsPdfTests failed'}
New-Item -ItemType Directory -Force -Path $png|Out-Null
& $python -I (Join-Path $PSScriptRoot 'verify_pdf.py') $out $png (Join-Path $out 'verify-pdf.json')
if($LASTEXITCODE){throw 'The PDFs do not match; see verify-pdf.json'}
