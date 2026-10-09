$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$workspaceRoot=Split-Path (Split-Path $projectRoot -Parent) -Parent
# The DWG inspection audit is a local folder kept outside the repository.
$auditFile=Join-Path $workspaceRoot '.dwg-inspection\structure-audit.json'
if(-not (Test-Path -LiteralPath $auditFile)){Write-Warning ('SKIPPED: local DWG inspection audit not found (not in the repository): '+$auditFile);return}
$audit=Get-Content -LiteralPath $auditFile -Raw | ConvertFrom-Json
$entities=($audit.blocks | Where-Object Name -eq '*Model_Space').entities
$roles=@{}
function Bind-Range([string]$start,[string]$end,[string]$role) {
    $first=[Convert]::ToInt32($start,16);$last=[Convert]::ToInt32($end,16)
    foreach($e in $entities) { $number=[Convert]::ToInt32($e.id.handle,16);if($number -ge $first -and $number -le $last) {$roles[$e.id.handle]=$role} }
}
function Bind-Keys([string]$keys,[string]$role) {foreach($key in $keys.Split(' ')) {$roles[$key]=$role}}
Bind-Range '46D' '4C2' 'cwLeft'
Bind-Range '4C3' '516' 'cwRight'
Bind-Range '537' '594' 'carRight'
Bind-Range '596' '5F1' 'carLeft'
Bind-Range '63F' '662' 'governor'
Bind-Range '521' '529' 'cabin'
Bind-Range '531' '536' 'carSpan'
Bind-Range '5F5' '5FB' 'entrance'
Bind-Range '5FE' '609' 'centerFront'
Bind-Range '665' '66A' 'centerFront'
Bind-Range '66B' '670' 'centerRear'
Bind-Keys '468 625 626 627 628 62F 51F 520' 'wall'
Bind-Keys '51C 51D 680' 'cwRight'
Bind-Keys '681 682' 'cwLeft'
Bind-Keys '51E' 'cwOutline'
Bind-Keys '5F3 5F4' 'cabin'
Bind-Keys '52B 52C 52D 52E 530' 'carMark'
Bind-Keys '469 46A 46B 46C 472' 'cwMark'
Bind-Keys '52F 595 673' 'carRight'
Bind-Keys '5F2' 'carLeft'
Bind-Keys '591 594' 'carSupportRight'
Bind-Keys '5EE 5F1' 'carSupportLeft'
Bind-Keys '527' 'mainCL'
Bind-Keys '52A' 'carCL'
Bind-Keys '675' 'cwCL'
Bind-Keys '5FC 5FD 60F 610 619 61A 61C' 'doorRight'
Bind-Keys '60A 60B 611 61D' 'doorLeft'
Bind-Keys '60C 60D 60E' 'doorSpan'
Bind-Keys '618' 'doorLeader'
Bind-Keys '663 672 674' 'governor'
Bind-Keys '664' 'centerFront'
Bind-Keys '67E 67F' 'cwRight'
$dimRules=@{
 '517'=@('x','cwRailLeft','center','rear'); '518'=@('x','cwRailRight','center','rear');
 '519'=@('x','cwRailLeft','cwRailRight','rear'); '51A'=@('x','cwRailLeft','left','rear'); '51B'=@('x','cwRailRight','right','rear');
 '612'=@('x','doorLeft','center','front'); '615'=@('x','center','doorRight','front');
 '61B'=@('x','doorLeft','doorRight','front'); '61E'=@('x','carLeft','doorLeft','carFront');
 '61F'=@('x','doorLeft','doorRight','carFront'); '620'=@('x','doorRight','carRight','carFront');
 '623'=@('x','doorRight','right','front'); '624'=@('x','left','doorLeft','front');
 '629'=@('x','center','left','front'); '62A'=@('x','center','right','front'); '62B'=@('x','right','left','front');
 '62E'=@('y','cwRow','rear','right'); '630'=@('y','carRow','front','right'); '631'=@('y','carRow','cwRow','right');
 '633'=@('y','carFront','carRear','left'); '634'=@('y','carFront','carRow','left'); '637'=@('y','carRow','carRear','left');
 '638'=@('y','cwRow','rear','left'); '639'=@('x','railLeft','railRight','carRow'); '63C'=@('x','carLeft','carRight','carRear');
 '671'=@('y','front','rear','right'); '676'=@('y','carRear','cwRow','left');
 '677'=@('y','cwRear','cwFront','left'); '67A'=@('y','cwFront','carRear','left');
 '67D'=@('y','cwRear','rear','left'); '683'=@('y','carFront','front','left')
}
$unbound=@($entities|Where-Object {$_.id.dxf -ne 'DIMENSION' -and -not $roles.ContainsKey($_.id.handle)})
if($unbound.Count) {$unbound|Select-Object @{n='Handle';e={$_.id.handle}},@{n='Type';e={$_.id.dxf}},StartPoint,EndPoint|ConvertTo-Json -Compress;throw 'Incomplete entity binding.'}
$bindings=@(foreach($e in $entities) {
 $h=$e.id.handle
 if($e.id.dxf -eq 'DIMENSION') {
   if(-not $dimRules.ContainsKey($h)) {throw "Unbound dimension $h"}
   $r=$dimRules[$h];[ordered]@{Key=$h;Handle=$h;Type=$e.id.dxf;Layer=$e.Layer;Role='dimension';Axis=$r[0];A=$r[1];B=$r[2];Band=$r[3]}
 } else {
   if(-not $roles.ContainsKey($h)) {throw "Unbound entity $h $($e.id.dxf) $($e.Layer)"}
   [ordered]@{Key=$h;Handle=$h;Type=$e.id.dxf;Layer=$e.Layer;Role=$roles[$h]}
 }
})
[ordered]@{TemplateSha256=$audit.sha256Before;ExpectedEntityCount=$entities.Count;Entities=$bindings}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $projectRoot 'config\template-bindings.json') -Encoding utf8
$bindings|Group-Object Role|Select-Object Name,Count|ConvertTo-Json -Compress
