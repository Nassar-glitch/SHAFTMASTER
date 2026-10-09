param([string]$CandidateBin='bin-shaftmaster-calcsheet-candidate',[string]$EvidenceDirectory='output/calcsheet-20261007/core')
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bin=Join-Path $root $CandidateBin;$out=Join-Path $root $EvidenceDirectory
if($CandidateBin -in @('bin-shaftmaster-v01','bin-shaftmaster-basic-candidate','bin-shaftmaster-calculations-candidate','bin-shaftmaster-safety-pit-candidate','bin-shaftmaster-ff-candidate')){throw 'Use the separate calculation-sheet candidate, not the release or a previous candidate.'}
New-Item -ItemType Directory -Force -Path $out | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$wpf=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/WPF'
$refs=@('/reference:System.Web.Extensions.dll','/reference:System.Drawing.dll','/reference:System.Xaml.dll',('/reference:'+(Join-Path $wpf 'WindowsBase.dll')),('/reference:'+(Join-Path $wpf 'PresentationCore.dll')),('/reference:'+(Join-Path $wpf 'PresentationFramework.dll')),('/reference:'+(Join-Path $bin 'Elevator.Core.dll')),('/reference:'+(Join-Path $bin 'Elevator.Solution.Bridge.dll')))
$sources=@('tests/solution/CalculationSheetTests.cs','Elevator.Solution.Desktop/CalculationSheet.cs','Elevator.Solution.Desktop/CalculationSheetRopes.cs','Elevator.Solution.Desktop/CalculationFiguresRopes.cs','Elevator.Solution.Desktop/DesignNotes.cs','Elevator.Solution.Desktop/DocxWriter.cs','Elevator.Solution.Desktop/CompanyReportSettings.cs','Elevator.Solution.Desktop/FigureModel.cs','Elevator.Solution.Desktop/FigureRender.cs','Elevator.Solution.Desktop/CalculationFigures.cs','Elevator.Solution.Desktop/CalculationWorkspace.cs','Elevator.Solution.Desktop/ProjectSession.cs')|ForEach-Object{(Get-Item -LiteralPath (Join-Path $root $_)).FullName}
& $compiler /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $bin 'CalculationSheetTests.exe')) @refs @sources
if($LASTEXITCODE){throw 'Calculation sheet test build failed'}
& (Join-Path $bin 'CalculationSheetTests.exe') $root $bin $out
if($LASTEXITCODE){throw 'Calculation sheet tests failed; see calculation-sheet-error.json'}
