# Batch-mode compile check. The Unity Editor MUST be closed first.
# NOTE: keep this file ASCII-only -- Windows PowerShell 5.1 reads BOM-less UTF-8 as ANSI
#       and non-ASCII characters break parsing (see .claude/skills/unity-batch-autoconfig).
# Usage: & <this file> -LogName "compile.log"
param([string]$LogName = "compile.log")

$exe  = "D:\unity\unitydownload\6000.0.44f1\Editor\Unity.exe"
$proj = "D:\unity\proj\Survivor"
$log  = Join-Path $proj "Logs\$LogName"

New-Item -ItemType Directory -Force -Path (Join-Path $proj "Logs") | Out-Null
if (Test-Path $log) { Remove-Item $log -Force }

$unityArgs = @("-batchmode", "-nographics", "-quit", "-projectPath", $proj, "-logFile", $log)
$p = Start-Process -FilePath $exe -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow

Write-Output "Unity ExitCode = $($p.ExitCode)"
if (-not (Test-Path $log)) { Write-Output "LOG NOT CREATED"; exit 1 }

$content = Get-Content $log

function Show([string]$title, $matches, [string]$emptyText) {
    Write-Output ""
    Write-Output "--- $title ---"
    if ($matches) { $matches | ForEach-Object { $_.Line.Trim() } } else { Write-Output $emptyText }
}

Show "error CS"        ($content | Select-String -Pattern "error CS\d+") "NONE"
Show "Tundra"          ($content | Select-String -Pattern "Tundra build") "NONE"
Show "Weaver banner"   ($content | Select-String -Pattern "^Mirror \| mirror-networking") "NONE"
Show "compile failure" ($content | Select-String -Pattern "Scripts have compiler errors|Compilation failed") "NONE"
Show "C# exceptions"   ($content | Select-String -Pattern "error CS|NullReferenceException|MissingReferenceException") "NONE"

$errCount = ($content | Select-String -Pattern "error CS\d+").Count
Write-Output ""
if ($errCount -eq 0) { Write-Output "RESULT: PASS (no error CS)" } else { Write-Output "RESULT: FAIL ($errCount error CS)" }
