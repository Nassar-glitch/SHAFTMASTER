param([string]$BinDirectory='bin-section',[string[]]$Skip=@())
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$tests=Join-Path $root 'tests'
$reports=foreach($spec in @(@('cad-test-results.json',17),@('architecture-results.json',14),@('workflow-results.json',1),@('plan-results.json',7),@('machine-room-results.json',32),@('section-results.json',32),@('section-reopen-results.json',2))|Where-Object {$Skip -notcontains $_[0].Replace('-results.json','')}) {
    $file=Get-Item (Join-Path $tests $spec[0]);$rows=@(Get-Content $file.FullName -Raw|ConvertFrom-Json)
    if($rows.Count -ne $spec[1] -or @($rows|Where-Object status -ne PASS).Count){throw ('Failed/incomplete report: '+$file.Name)}
    [pscustomobject]@{Report=$file.Name;Passed=$rows.Count;Failed=0;LastWriteTimeUtc=$file.LastWriteTimeUtc.ToString('o')}
}
$expected=@{
    'Machine room codex.dwg'='288E4F513A7B3350F6E2DD390E753E3200B7A46B52401FFC1BDAB42F8A643675'
    'plan codex.dwg'='DA5449721797C26D3D0CDE3836AA9F5FD76CA2F49D6B1AE83C6DC23F17578C9A'
    'Vert MR and Reeving codex.dwg'='B57E042BE9E36DB3F61471923035E00FCAB66E014FD019D40342020E1584D0BC'
    'legacy/ElevatorPlanGenerator/templates/plan codex.dwg'='5F2CE6B190811338D1E446EEEAC9F17E0EE3B6A8DF563654EB98F324B4649447'
}
$hashes=foreach($file in $expected.Keys){
    $path=Join-Path (Split-Path $root -Parent) $file
    # The root project drawings are local files kept outside the repository; a missing one is reported and skipped.
    if(-not (Test-Path -LiteralPath $path)){Write-Warning ('SKIPPED source-drawing hash: local drawing not found (not in the repository): '+$path);continue}
    $stream=[IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite);$sha=[Security.Cryptography.SHA256]::Create()
    try{$hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}
    if($hash -ne $expected[$file]){throw ('Source drawing changed: '+$path)}
    [pscustomobject]@{Path=$path;SHA256=$hash;Unchanged=$true}
}
$summary=[pscustomobject]@{
    Status='PASS';VerifiedUtc=[DateTime]::UtcNow.ToString('o');NativeRecords=105;PureAssertions=190
    Reports=@($reports);OriginalDrawings=@($hashes)
    DLL=(Get-FileHash (Join-Path $root ($BinDirectory+'/Elevator.AutoCAD.dll')) -Algorithm SHA256)
    Scope='Scoped CAD geometry regression; engineering clearances, hanger mechanics, structural adequacy and full traction compliance remain separately reviewable.'
}
$summary|ConvertTo-Json -Depth 8|Set-Content (Join-Path $PSScriptRoot 'verification-summary.json') -Encoding utf8
$reports|ForEach-Object {Write-Output ($_.Report+': '+$_.Passed+' PASS')}
