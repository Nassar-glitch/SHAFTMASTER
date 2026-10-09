param([string]$CandidateBin='bin-shaftmaster-calcsheet-candidate',[string]$EvidenceDirectory='output/calcsheet-20261007/core',[switch]$WordRender)
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bin=Join-Path $root $CandidateBin;$out=Join-Path $root $EvidenceDirectory
if($CandidateBin -in @('bin-shaftmaster-v01','bin-shaftmaster-basic-candidate','bin-shaftmaster-calculations-candidate','bin-shaftmaster-safety-pit-candidate','bin-shaftmaster-ff-candidate')){throw 'Use the separate calculation-sheet candidate, not the release or a previous candidate.'}
New-Item -ItemType Directory -Force -Path $out | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$wpf=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/WPF'
$refs=@('/reference:System.Web.Extensions.dll','/reference:System.Drawing.dll','/reference:System.Xaml.dll',('/reference:'+(Join-Path $wpf 'WindowsBase.dll')),('/reference:'+(Join-Path $wpf 'PresentationCore.dll')),('/reference:'+(Join-Path $wpf 'PresentationFramework.dll')),('/reference:'+(Join-Path $bin 'Elevator.Core.dll')),('/reference:'+(Join-Path $bin 'Elevator.Solution.Bridge.dll')),('/reference:'+(Join-Path $bin 'Elevator.Reporting.dll')))
$sources=@('tests/solution/CalculationSheetTests.cs','Elevator.Solution.Desktop/ProjectSession.cs','Elevator.Solution.Desktop/WpfFigureRasterizer.cs')|ForEach-Object{(Get-Item -LiteralPath (Join-Path $root $_)).FullName}
& $compiler /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $bin 'CalculationSheetTests.exe')) @refs @sources
if($LASTEXITCODE){throw 'Calculation sheet test build failed'}
& (Join-Path $bin 'CalculationSheetTests.exe') $root $bin $out
if($LASTEXITCODE){throw 'Calculation sheet tests failed; see calculation-sheet-error.json'}
# -WordRender (tier 3 only): render every sheet through Word to PDF, rasterise the pages and check the pagination.
if($WordRender){
 & (Join-Path $PSScriptRoot 'render-calculation-sheets.ps1') -SheetDirectory $EvidenceDirectory -RenderDirectory ($EvidenceDirectory+'-rendered')
 python (Join-Path $PSScriptRoot 'rasterise_calculation_sheets.py') $out ($out+'-rendered')
 if($LASTEXITCODE){throw 'Calculation sheet raster check failed'}
 python (Join-Path $PSScriptRoot 'check_pagination.py') ($out+'-rendered') 01-basic-complete 12-changed-geometry 14-rope-traction 15-rope-not-ok
 if($LASTEXITCODE){throw 'Calculation sheet pagination check failed'}
}
