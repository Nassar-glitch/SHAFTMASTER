param([string]$AutoCADDirectory='C:/Program Files/Autodesk/AutoCAD 2023',[string]$SolidWorksDirectory='C:/Program Files/SOLIDWORKS Corp/SOLIDWORKS',[string]$BinDirectory='artifacts/bin',[switch]$Release)
$ErrorActionPreference='Stop'
# Builds go to artifacts/ by default. A release folder (bin-shaftmaster-v*) is written only with -Release.
if(((Split-Path $BinDirectory -Leaf) -like 'bin-shaftmaster-v*') -and -not $Release){throw ("Refusing to build into the release folder '"+$BinDirectory+"'. Release folders are written only with -Release; use the default artifacts/bin or another folder.")}
$solutionRoot=$PSScriptRoot
& (Join-Path $solutionRoot 'build.ps1') -AutoCADDirectory $AutoCADDirectory -BinDirectory $BinDirectory -Release:$Release
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$solutionBin=Join-Path $solutionRoot $BinDirectory
$baseRefs=@('/reference:System.Web.Extensions.dll',"/reference:$solutionBin/Elevator.Core.dll")
& $compiler /nologo /target:library "/out:$solutionBin/Elevator.Solution.Bridge.dll" @baseRefs @(Get-ChildItem "$solutionRoot/Elevator.Solution.Bridge/*.cs"|ForEach-Object FullName)
if($LASTEXITCODE){throw 'Bridge build failed'}
foreach($assembly in @('SolidWorks.Interop.sldworks.dll','SolidWorks.Interop.swconst.dll')){
    $dependency=Join-Path $SolidWorksDirectory $assembly
    if(!(Test-Path -LiteralPath $dependency)){throw "STEP build dependency missing: $dependency"}
    Copy-Item -LiteralPath $dependency -Destination (Join-Path $solutionBin $assembly) -Force
}
$stepRefs=$baseRefs+@("/reference:$solutionBin/Elevator.Solution.Bridge.dll","/reference:$solutionBin/SolidWorks.Interop.sldworks.dll","/reference:$solutionBin/SolidWorks.Interop.swconst.dll")
& $compiler /nologo /platform:x64 /target:exe "/out:$solutionBin/Elevator.Solution.StepExport.exe" @stepRefs @(Get-ChildItem "$solutionRoot/Elevator.Solution.StepExport/*.cs"|ForEach-Object FullName)
if($LASTEXITCODE){throw 'STEP worker build failed'}
Copy-Item "$solutionRoot/config/preliminary-3d-reference.json" "$solutionBin/preliminary-3d-reference.json" -Force
Copy-Item "$solutionRoot/docs/STEP-EXPORT.md" "$solutionBin/STEP-EXPORT.md" -Force
if(Test-Path -LiteralPath "$solutionRoot/docs/SHAFTMASTER-UI-RESULTS.md") { Copy-Item -LiteralPath "$solutionRoot/docs/SHAFTMASTER-UI-RESULTS.md" -Destination "$solutionBin/SHAFTMASTER-UI-RESULTS.md" -Force }
$baseRefs+="/reference:$solutionBin/Elevator.Solution.Bridge.dll"
$hostRefs=$baseRefs+@("/reference:$solutionBin/Elevator.AutoCAD.dll")
foreach($a in @('AcDbMgd','AcCoreMgd','AcMgd')){$hostRefs+="/reference:$AutoCADDirectory/$a.dll"}
& $compiler /nologo /target:library "/out:$solutionBin/Elevator.Solution.Host.dll" @hostRefs @(Get-ChildItem "$solutionRoot/Elevator.Solution.Host/*.cs"|ForEach-Object FullName)
if($LASTEXITCODE){throw 'Host build failed'}
$wpf=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/WPF'
$desktopRefs=$baseRefs+@("/reference:$wpf/PresentationCore.dll","/reference:$wpf/PresentationFramework.dll","/reference:$wpf/WindowsBase.dll",'/reference:System.Xaml.dll')
$assetDirectory=Join-Path $solutionBin 'brand-assets'
New-Item -ItemType Directory -Force -Path $assetDirectory | Out-Null
Get-ChildItem -LiteralPath (Join-Path $solutionRoot 'Elevator.Solution.Desktop/Assets') -File | Copy-Item -Destination $assetDirectory -Force
$iconSource=(Get-Item -LiteralPath (Join-Path $solutionRoot 'tools/BrandIconBuilder.cs')).FullName
& $compiler /nologo /target:exe "/out:$solutionBin/BrandIconBuilder.exe" @desktopRefs /reference:System.Xml.Linq.dll $iconSource
if($LASTEXITCODE){throw 'Icon builder failed'}
& "$solutionBin/BrandIconBuilder.exe" $assetDirectory
if($LASTEXITCODE){throw 'Icon generation failed'}
$brandArgs=@("/win32icon:$assetDirectory/ShaftMaster.ico","/win32manifest:$solutionRoot/Elevator.Solution.Desktop/app.manifest","/resource:$assetDirectory/ShaftMasterLogo.png,ShaftMaster.Assets.ShaftMasterLogo.png","/resource:$assetDirectory/ShaftMasterMark.png,ShaftMaster.Assets.ShaftMasterMark.png")
& $compiler /nologo /target:winexe /platform:x64 "/out:$solutionBin/ShaftMaster.exe" @desktopRefs @brandArgs @(Get-ChildItem "$solutionRoot/Elevator.Solution.Desktop/*.cs"|ForEach-Object FullName)
if($LASTEXITCODE){throw 'Desktop build failed'}
Copy-Item "$solutionRoot/examples/ElevatorProject.example.json" "$solutionBin/reviewed-example.elevator.json" -Force
Copy-Item "$solutionRoot/config/reviewed-context.json" "$solutionBin/reviewed-context.json" -Force
Copy-Item "$solutionRoot/config/reviewed-chassis-evidence.json" "$solutionBin/reviewed-chassis-evidence.json" -Force
Copy-Item "$solutionRoot/config/presets.json" "$solutionBin/presets.json" -Force
Copy-Item "$solutionRoot/Elevator.Solution.Desktop/App.config" "$solutionBin/ShaftMaster.exe.config" -Force
$stepTestSource=(Get-Item -LiteralPath (Join-Path $solutionRoot 'tests/step-export/StepExportTests.cs')).FullName
& $compiler /nologo /platform:x64 "/out:$solutionBin/StepExportTests.exe" @baseRefs $stepTestSource
if($LASTEXITCODE){throw 'STEP tests build failed'}
& "$solutionBin/StepExportTests.exe" $solutionBin "$solutionRoot/output/step-export-tests"
if($LASTEXITCODE){throw 'STEP input regression tests failed'}
Write-Output "Desktop built: $solutionBin/ShaftMaster.exe"
