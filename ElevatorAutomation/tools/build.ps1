param([string]$Bin='artifacts/bin',[string]$AutoCADDir='C:\Program Files\Autodesk\AutoCAD 2023',[string]$SolidWorksDir='C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS',[string]$Configuration='Release',[switch]$Release,[switch]$SkipTests)
# Build (ARCHITECTURE-PLAN P1): dotnet build of ElevatorAutomation.sln (SDK-style projects, Directory.Build.props) into one
# output folder, incremental. Then the checks the csc scripts ran after a build: the pure rule tests (RuleTests,
# MachineRoomRuleTests, ReevingTests) and the STEP-input tests (StepExportTests); -SkipTests leaves them out.
# The output is artifacts/bin unless -Bin is given; a release folder (bin-shaftmaster-v*) is written only with -Release.
$ErrorActionPreference='Stop'
if(((Split-Path $Bin -Leaf) -like 'bin-shaftmaster-v*') -and -not $Release){throw ("Refusing to build into the release folder '"+$Bin+"'. Release folders are written only with -Release; use the default artifacts/bin or another folder.")}
$root=Split-Path $PSScriptRoot -Parent
$out=if([IO.Path]::IsPathRooted($Bin)){$Bin}else{Join-Path $root $Bin}
$out=[IO.Path]::GetFullPath($out).TrimEnd('\','/')
foreach($dll in 'AcDbMgd.dll','AcCoreMgd.dll','AcMgd.dll'){if(-not (Test-Path -LiteralPath (Join-Path $AutoCADDir $dll))){throw "AutoCAD build dependency missing: $(Join-Path $AutoCADDir $dll). Pass -AutoCADDir."}}
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_NOLOGO='1'
# The folder is passed with a trailing '/', never '\': a backslash before the closing quote would escape it.
& dotnet build (Join-Path $root 'ElevatorAutomation.sln') -c $Configuration "-p:BinDir=$out/" "-p:AutoCADDir=$AutoCADDir" "-p:SolidWorksDir=$SolidWorksDir" -nologo -v:minimal
if($LASTEXITCODE){throw 'Build failed'}
if(-not $SkipTests){
 foreach($test in 'RuleTests','MachineRoomRuleTests'){& (Join-Path $out "$test.exe") (Join-Path $root 'config\equipment-profile.json');if($LASTEXITCODE){throw "$test failed"}}
 & (Join-Path $out 'ReevingTests.exe');if($LASTEXITCODE){throw 'ReevingTests failed'}
 Write-Output 'Build and pure design-rule tests passed.'
 & (Join-Path $out 'StepExportTests.exe') $out (Join-Path $root 'output/step-export-tests')
 if($LASTEXITCODE){throw 'STEP input regression tests failed'}
}
Write-Output "Desktop built: $out\ShaftMaster.exe"
