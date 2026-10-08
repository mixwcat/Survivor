# Run a Unity -executeMethod in batch mode. The Unity Editor MUST be closed first.
#
# Portable: works in any Unity project, no edits needed.
#   - Project path: walks up from this script until it finds ProjectSettings/ProjectVersion.txt
#   - Unity.exe:    -UnityExe  ->  registry (Unity Hub)  ->  common install roots
#
# Keep this file ASCII-only. Windows PowerShell 5.1 reads BOM-less UTF-8 as ANSI and
# non-ASCII characters break parsing (see the parent skill).
#
# Usage:
#   & .\run-unity-method.ps1 -Method "MySetup.SetupFromCommandLine"
#   & .\run-unity-method.ps1 -Method "MySetup.SetupFromCommandLine" -LogName "setup.log"
#   & .\run-unity-method.ps1 -Method "MySetup.Setup" -AlsoMatch "\[MySetup\]"

param(
    [Parameter(Mandatory = $true)][string]$Method,
    [string]$LogName = "run-method.log",
    [string]$ProjectPath,
    [string]$UnityExe,
    # Extra log pattern to surface (e.g. your setup script's own log prefix).
    [string]$AlsoMatch
)

$ErrorActionPreference = "Stop"

function Resolve-ProjectPath {
    param([string]$Explicit)

    if ($Explicit) {
        if (-not (Test-Path (Join-Path $Explicit "ProjectSettings\ProjectVersion.txt"))) {
            throw "-ProjectPath '$Explicit' does not look like a Unity project (no ProjectSettings/ProjectVersion.txt)."
        }
        return (Resolve-Path $Explicit).Path
    }

    # Walk up: works whether this script sits in <proj>/Tools/ or in a skill folder.
    $dir = $PSScriptRoot
    while ($dir) {
        if (Test-Path (Join-Path $dir "ProjectSettings\ProjectVersion.txt")) { return $dir }
        $parent = Split-Path -Parent $dir
        if (-not $parent -or $parent -eq $dir) { break }
        $dir = $parent
    }

    throw "Could not find a Unity project above '$PSScriptRoot'. Pass -ProjectPath."
}

function Resolve-UnityExe {
    param([string]$Project, [string]$Explicit)

    if ($Explicit) {
        if (Test-Path $Explicit) { return $Explicit }
        throw "-UnityExe '$Explicit' does not exist."
    }

    $version = $null
    $versionFile = Join-Path $Project "ProjectSettings\ProjectVersion.txt"
    $m = Select-String -Path $versionFile -Pattern "m_EditorVersion:\s*(\S+)" | Select-Object -First 1
    if ($m) { $version = $m.Matches[0].Groups[1].Value }

    if ($version) {
        $keys = @(
            "HKLM:\SOFTWARE\Unity Technologies\Installer\Unity $version",
            "HKCU:\SOFTWARE\Unity Technologies\Installer\Unity $version"
        )
        foreach ($k in $keys) {
            $loc = (Get-ItemProperty -Path $k -Name "Location x64" -ErrorAction SilentlyContinue)."Location x64"
            if ($loc) {
                $candidate = Join-Path $loc "Editor\Unity.exe"
                if (Test-Path $candidate) { return $candidate }
            }
        }
    }

    $roots = @(
        "C:\Program Files\Unity\Hub\Editor",
        "C:\Program Files\Unity\Editor",
        "D:\unity",
        "D:\Unity"
    )
    foreach ($root in $roots) {
        if (-not (Test-Path $root)) { continue }
        $found = Get-ChildItem -Path $root -Filter "Unity.exe" -Recurse -Depth 3 -ErrorAction SilentlyContinue |
                 Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($found) { return $found.FullName }
    }

    throw "Could not locate Unity.exe. Pass -UnityExe explicitly. (Project wants version '$version'.)"
}

$proj = Resolve-ProjectPath -Explicit $ProjectPath
$exe  = Resolve-UnityExe -Project $proj -Explicit $UnityExe

$logDir = Join-Path $proj "Logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$log = Join-Path $logDir $LogName
if (Test-Path $log) { Remove-Item $log -Force }

Write-Output "Unity   = $exe"
Write-Output "Project = $proj"
Write-Output "Method  = $Method"

# Unity.exe is a GUI-subsystem app: '& <exe> args' returns immediately and yields no log,
# so Start-Process -Wait is required.
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

Show "error CS"   ($content | Select-String -Pattern "error CS\d+") "NONE"
Show "exceptions" ($content | Select-String -Pattern "Exception|Error:") "NONE"

# Convention for setup scripts: print CLI_OK on success, CLI_FAIL on failure.
$okHits = $content | Select-String -Pattern "CLI_OK"
Show "CLI_OK / CLI_FAIL" ($content | Select-String -Pattern "CLI_OK|CLI_FAIL") "NONE (does your setup print CLI_OK?)"

if ($AlsoMatch) { Show "custom" ($content | Select-String -Pattern $AlsoMatch) "NONE" }

Write-Output ""
if ($okHits.Count -gt 0) { Write-Output "RESULT: PASS" } else { Write-Output "RESULT: FAIL" }
