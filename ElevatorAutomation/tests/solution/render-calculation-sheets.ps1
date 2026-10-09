# Test-only rendering: Word (if installed) exports each .docx to PDF read-only, one isolated
# process per file with a timeout; PyMuPDF rasterises pages. The application never requires Word.
param([string]$SheetDirectory='output/calcsheet-20261007/core',[string]$RenderDirectory='output/calcsheet-20261007/rendered',[int]$TimeoutSeconds=120)
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$in=Join-Path $root $SheetDirectory;$out=Join-Path $root $RenderDirectory
New-Item -ItemType Directory -Force -Path $out | Out-Null
$results=@()
foreach($file in Get-ChildItem -LiteralPath $in -Filter *.docx){
 $hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
 $pdf=Join-Path $out ($file.BaseName+'.pdf')
 $before=@(Get-Process WINWORD -ErrorAction SilentlyContinue|ForEach-Object Id)
 $job=Start-Job -ArgumentList $file.FullName,$pdf -ScriptBlock{param($docx,$pdf)
  $w=New-Object -ComObject Word.Application;$w.Visible=$false;$w.DisplayAlerts=0
  try{$d=$w.Documents.Open($docx,$false,$true,$false);$p=$d.ComputeStatistics(2);$d.ExportAsFixedFormat($pdf,17);$d.Close(0);$p}finally{$w.Quit()}}
 if(Wait-Job $job -Timeout $TimeoutSeconds){$pages=[int](@(Receive-Job $job)[-1]);$state='OK'}else{Stop-Job $job;$pages=$null;$state='TIMEOUT'}
 Remove-Job $job -Force
 # Close only Word instances this render started (never a pre-existing user session).
 Get-Process WINWORD -ErrorAction SilentlyContinue|Where-Object{$before -notcontains $_.Id}|Stop-Process -Force -ErrorAction SilentlyContinue
 $after=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
 $results+=[pscustomobject]@{File=$file.Name;State=$state;WordPages=$pages;UnchangedByWord=($hash -eq $after)}
}
$results|ConvertTo-Json|Set-Content -Encoding utf8 (Join-Path $out 'word-render.json')
$results|Format-Table -AutoSize|Out-String -Width 200
if($results|Where-Object{$_.State -ne 'OK' -or -not $_.UnchangedByWord}){throw 'Rendering failed or a source document changed'}
