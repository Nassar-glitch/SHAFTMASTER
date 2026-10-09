param([string]$BinDirectory='bin-shaftmaster-v01')
$ErrorActionPreference='Stop'
$testRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$testBin=Join-Path $testRoot $BinDirectory
$testWpf=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/WPF'
$testSource=(Get-Item -LiteralPath (Join-Path $PSScriptRoot 'UiRegression.cs')).FullName
$testRefs=@("/reference:$testBin/ShaftMaster.exe","/reference:$testBin/Elevator.Core.dll","/reference:$testBin/Elevator.Solution.Bridge.dll","/reference:$testBin/Elevator.Reporting.dll","/reference:$testWpf/PresentationCore.dll","/reference:$testWpf/PresentationFramework.dll","/reference:$testWpf/WindowsBase.dll",'/reference:System.Xaml.dll','/reference:System.Web.Extensions.dll')
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:exe /platform:x64 "/out:$testBin/UiRegression.exe" @testRefs $testSource
if($LASTEXITCODE){throw 'UI test compile failed'}
& "$testBin/UiRegression.exe" "$testRoot/output/shaftmaster-ui/layout-tests"
if($LASTEXITCODE){throw 'UI regression failed: see output/shaftmaster-ui/layout-tests/ui-regression.json'}
Get-Content -Raw -LiteralPath "$testRoot/output/shaftmaster-ui/layout-tests/ui-regression.json" | ConvertFrom-Json | Select-Object Status,Application,Display,Dpi,Note
