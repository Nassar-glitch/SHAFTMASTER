param([string]$Bin='artifacts/bin',[string]$OutDir='',[switch]$Approve,[switch]$MakeFixtures)
# Tier-1 golden-master suite (tests/golden). Compiles tests/golden/GoldenTests.cs with Elevator.Reporting and the WPF figure rasterizer of the desktop
# application against the Core and Bridge of -Bin (default artifacts/bin; build it first with tools/build.ps1), runs it
# into a fresh folder and compares every captured file with tests/golden/approved. On a mismatch the received file and a
# readable diff are written under <OutDir>/mismatch and the run fails.
# -Approve writes the initial baseline only (the approved folder must be empty). -MakeFixtures writes the fixture set once.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$binPath=Join-Path $root $Bin
if(-not (Test-Path -LiteralPath (Join-Path $binPath 'Elevator.Core.dll'))){throw "No build in $binPath. Run tools/build.ps1 first."}
if(-not $OutDir){$OutDir=Join-Path 'artifacts/test-results' ((Get-Date -Format 'yyyyMMdd-HHmmss')+'/golden')}
$out=Join-Path $root $OutDir
if((Test-Path -LiteralPath $out) -and @(Get-ChildItem -LiteralPath $out -Force).Count){throw "Output folder is not empty: $out"}
New-Item -ItemType Directory -Force -Path $out | Out-Null
$golden=Join-Path $root 'tests/golden'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$wpf=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/WPF'
$refs=@('/reference:System.Web.Extensions.dll','/reference:System.Drawing.dll','/reference:System.Xaml.dll',('/reference:'+(Join-Path $wpf 'WindowsBase.dll')),('/reference:'+(Join-Path $wpf 'PresentationCore.dll')),('/reference:'+(Join-Path $wpf 'PresentationFramework.dll')),('/reference:'+(Join-Path $binPath 'Elevator.Core.dll')),('/reference:'+(Join-Path $binPath 'Elevator.Solution.Bridge.dll')),('/reference:'+(Join-Path $binPath 'Elevator.Reporting.dll')))
$sources=@('tests/golden/GoldenTests.cs','tests/golden/JsonMembers.cs','Elevator.Solution.Desktop/WpfFigureRasterizer.cs')|ForEach-Object{(Get-Item -LiteralPath (Join-Path $root $_)).FullName}
# The test program runs from its own folder beside copies of the Core and Bridge it was compiled against.
$exeDir=Join-Path $out 'bin';New-Item -ItemType Directory -Force -Path $exeDir | Out-Null
foreach($dll in 'Elevator.Core.dll','Elevator.Solution.Bridge.dll','Elevator.Reporting.dll'){Copy-Item -LiteralPath (Join-Path $binPath $dll) -Destination $exeDir}
& $compiler /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $exeDir 'GoldenTests.exe')) @refs @sources
if($LASTEXITCODE){throw 'Golden tests build failed'}
$mode=if($MakeFixtures){'fixtures'}elseif($Approve){'approve'}else{'compare'}
& (Join-Path $exeDir 'GoldenTests.exe') $root $binPath (Join-Path $golden 'fixtures') (Join-Path $golden 'approved') $out $mode
if($LASTEXITCODE){throw "Golden tests failed; see $out"}
