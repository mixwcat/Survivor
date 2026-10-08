# Run a Unity -executeMethod in batch mode. The Editor MUST be closed first.
# Keep ASCII-only (Windows PowerShell 5.1 reads BOM-less UTF-8 as ANSI).
# Usage: & <this file> -Method "NetworkSetup.SetupFromCommandLine" -LogName "net-setup.log"
param(
    [Parameter(Mandatory = $true)][string]$Method,
    [string]$LogName = "run-method.log"
)

$exe  = "D:\unity\unitydownload\6000.0.44f1\Editor\Unity.exe"
$proj = "D:\unity\proj\Survivor"
$log  = Join-Path $proj "Logs\$LogName"

New-Item -ItemType Directory -Force -Path (Join-Path $proj "Logs") | Out-Null
if (Test-Path $log) { Remove-Item $log -Force }

$unityArgs = @("-batchmode", "-nographics", "-quit", "-projectPath", $proj,
               "-executeMethod", $Method, "-logFile", $log)
$p = Start-Process -FilePath $exe -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow

Write-Output "Unity ExitCode = $($p.ExitCode)"
if (-not (Test-Path $log)) { Write-Output "LOG NOT CREATED"; exit 1 }

$content = Get-Content $log

function Show([string]$title, $matches, [string]$emptyText) {
    Write-Output ""
    Write-Output "--- $title ---"
    if ($matches) { $matches | ForEach-Object { $_.Line.Trim() } } else { Write-Output $emptyText }
}

Show "CLI_OK / CLI_FAIL" ($content | Select-String -Pattern "CLI_OK|CLI_FAIL") "NONE"
Show "error CS"         ($content | Select-String -Pattern "error CS\d+") "NONE"
Show "exceptions"       ($content | Select-String -Pattern "Exception|error CS|Error:") "NONE"
Show "setup logs"       ($content | Select-String -Pattern "\[NetworkSetup\]|\[AddressablesSetup\]") "NONE"

$ok = ($content | Select-String -Pattern "CLI_OK").Count
Write-Output ""
if ($ok -gt 0) { Write-Output "RESULT: PASS" } else { Write-Output "RESULT: FAIL" }
