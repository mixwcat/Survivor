# Two-process Mirror smoke test.
#
# WHY THIS EXISTS: a single-process Host test cannot see client-side bugs. In Host mode
# the server and the client are the SAME object, so every ApplyNetwork* method returns
# early through its authority guard. That already hid a real bug once (NetworkAuthority
# reported "true" for scene objects, so a whole batch of client guards did nothing while
# the Host test stayed green).
#
# Two Unity instances cannot open the same project (project lock), so this script keeps a
# mirrored copy of the project and runs the client from there:
#   server  -> <project>          (this repo)
#   client  -> <CopyPath>         (mirror, synced from Assets/Packages/ProjectSettings)
#
# The copy keeps its own Library, so only changed assets get reimported -- that is what
# makes this cheap enough to run as a regression tool.
#
# ONLY ASCII in this file: PowerShell 5.1 reads BOM-less UTF-8 as ANSI, and Chinese
# characters break the parser. See Tools/README.md.

param(
    [string]$LogName = "2p.log",
    [string]$CopyPath = "D:\unity\proj\SurvivorClient",
    [int]$ClientTimeoutSec = 480,
    [int]$ServerReadyTimeoutSec = 240,
    [switch]$SkipSync
)

$ErrorActionPreference = "Stop"

$proj    = Split-Path -Parent $PSScriptRoot
$unity   = "D:\unity\unitydownload\6000.0.44f1\Editor\Unity.exe"
$logs    = Join-Path $proj "Logs"
$serverLog = Join-Path $logs "2p-server.log"
$clientLog = Join-Path $logs "2p-client.log"

New-Item -ItemType Directory -Force -Path $logs | Out-Null

if (-not (Test-Path $unity))  { Write-Host "Unity not found: $unity";        exit 2 }
if (-not (Test-Path $CopyPath)) { Write-Host "Client copy not found: $CopyPath"; exit 2 }

# --- guard: no Unity may be running on either project --------------------------
$running = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -eq "Unity" }
if ($running) { Write-Host "Unity is already running (pid $($running.Id -join ',')). Close it first."; exit 2 }

# --- 1. sync the mirror --------------------------------------------------------
if (-not $SkipSync) {
    Write-Host "Syncing client copy..."
    foreach ($sub in @("Assets", "Packages", "ProjectSettings")) {
        $s = Join-Path $proj $sub
        $d = Join-Path $CopyPath $sub
        if (-not (Test-Path $s)) { continue }
        robocopy $s $d /MIR /MT:16 /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { Write-Host "robocopy failed for $sub (exit $LASTEXITCODE)"; exit 2 }
    }
    Write-Host "Sync done."
}

# --- 2. start the server (this repo) -------------------------------------------
Remove-Item $serverLog, $clientLog -ErrorAction SilentlyContinue

$serverArgs = @(
    "-batchmode", "-nographics",
    "-projectPath", $proj,
    "-executeMethod", "NetworkSmokeTest.RunServerFromCommandLine",
    "-logFile", $serverLog
)

Write-Host "Starting server (this repo)..."
$server = Start-Process -FilePath $unity -ArgumentList $serverArgs -PassThru -NoNewWindow

function Wait-ForPattern {
    param([string]$Path, [string]$Pattern, [int]$TimeoutSec)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path $Path) {
            $hit = Select-String -Path $Path -Pattern $Pattern -Quiet -ErrorAction SilentlyContinue
            if ($hit) { return $true }
        }
        Start-Sleep -Seconds 3
    }
    return $false
}

Write-Host "Waiting for the server to be listening..."
if (-not (Wait-ForPattern -Path $serverLog -Pattern "SERVER_READY|SMOKE_FAIL" -TimeoutSec $ServerReadyTimeoutSec)) {
    Write-Host "Server never became ready; see $serverLog"
    Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
    exit 1
}

if (Select-String -Path $serverLog -Pattern "SMOKE_FAIL" -Quiet -ErrorAction SilentlyContinue) {
    Write-Host "Server failed before the client even started; see $serverLog"
    Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
    exit 1
}

# --- 3. start the client (the mirror) ------------------------------------------
$clientArgs = @(
    "-batchmode", "-nographics",
    "-projectPath", $CopyPath,
    "-executeMethod", "NetworkSmokeTest.RunClientFromCommandLine",
    "-logFile", $clientLog
)

Write-Host "Starting client (mirror at $CopyPath)..."
$client = Start-Process -FilePath $unity -ArgumentList $clientArgs -PassThru -NoNewWindow

if (-not $client.WaitForExit($ClientTimeoutSec * 1000)) {
    Write-Host "Client timed out after $ClientTimeoutSec s; killing it."
    Stop-Process -Id $client.Id -Force -ErrorAction SilentlyContinue
    $clientExit = -1
} else {
    $client.Refresh()
    $clientExit = $client.ExitCode
}

# --- 4. stop the server --------------------------------------------------------
Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# --- 5. report -----------------------------------------------------------------
function Show {
    param([string]$Title, [string]$Path)
    Write-Host ""
    Write-Host "--- $Title ---"
    if (-not (Test-Path $Path)) { Write-Host "(no log)"; return }
    $lines = Select-String -Path $Path -Pattern "\[Smoke\]|\[Net\]|\[NetworkBootstrap\]|\[PlayerSpawner\]|error CS" -ErrorAction SilentlyContinue
    if (-not $lines) { Write-Host "(nothing matched)"; return }
    $lines | ForEach-Object { $_.Line.Trim() }
}

Show "server" $serverLog
Show "client" $clientLog

$serverOk = (Test-Path $serverLog) -and (Select-String -Path $serverLog -Pattern "SMOKE_OK" -Quiet -ErrorAction SilentlyContinue)
$clientOk = (Test-Path $clientLog) -and (Select-String -Path $clientLog -Pattern "SMOKE_OK" -Quiet -ErrorAction SilentlyContinue)

Write-Host ""
Write-Host "server SMOKE_OK = $serverOk"
Write-Host "client SMOKE_OK = $clientOk"
Write-Host "(judged by the SMOKE_OK markers in the logs, not by exit codes -- the server is"
Write-Host " killed on purpose, and the client exits from inside Play mode)"

if ($serverOk -and $clientOk) { Write-Host "RESULT: PASS"; exit 0 }

Write-Host "RESULT: FAIL"
exit 1
