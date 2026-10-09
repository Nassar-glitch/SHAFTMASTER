param([string]$AutoCADDirectory='C:\Program Files\Autodesk\AutoCAD 2023',[string]$BinDirectory='artifacts/bin',[switch]$Release)
# Thin wrapper kept for existing callers: the build is tools/build.ps1 (dotnet build of ElevatorAutomation.sln).
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'tools\build.ps1') -Bin $BinDirectory -AutoCADDir $AutoCADDirectory -Release:$Release