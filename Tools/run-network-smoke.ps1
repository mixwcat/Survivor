# Run NetworkSmokeTest in batch mode. The Editor MUST be closed.
# NOTE: intentionally NO -quit: the editor has to stay alive to run Play mode assertions.
# Keep ASCII-only (Windows PowerShell 5.1 reads BOM-less UTF-8 as ANSI).
param([string]$LogName = "smoke.log")

$exe  = "D:\unity\unitydownload\6000.0.44f1\Editor\Unity.exe"
$proj = "D:\unity\proj\Survivor"
$log  = Join-Path $proj "Logs\$LogName"

New-Item -ItemType Directory -Force -Path (Join-Path $proj "Logs") | Out-Null
if (Test-Path $log) { Remove-Item $log -Force }

$unityArgs = @("-batchmode", "-nographics", "-projectPath", $proj,
               "-executeMethod", "NetworkSmokeTest.RunFromCommandLine",
               "-logFile", $log)

$p = Start-Process -FilePath $exe -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow
Write-Output "Unity ExitCode = $($p.ExitCode)"

if (-not (Test-Path $log)) { Write-Output "LOG NOT CREATED"; exit 1 }
$content = Get-Content $log

function Show([string]$title, $matches, [string]$emptyText) {
    Write-Output ""
    Write-Output "--- $title ---"
    if ($matches) { $matches | ForEach-Object { $_.Line.Trim() } } else { Write-Output $emptyText }
}

Show "SMOKE result" ($content | Select-String -Pattern "SMOKE_OK|SMOKE_FAIL") "NONE"
Show "smoke steps"  ($content | Select-String -Pattern "\[Smoke\]") "NONE"
Show "error CS"     ($content | Select-String -Pattern "error CS\d+") "NONE"
Show "net logs"     ($content | Select-String -Pattern "\[Net\]|\[NetworkBootstrap\]|\[PlayerSpawner\]") "NONE"
Show "exceptions"   ($content | Select-String -Pattern "NullReferenceException|MissingReferenceException|UnassignedReference|InvalidOperationException") "NONE"
