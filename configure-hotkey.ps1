[CmdletBinding()]
param(
    [string]$Hotkey,
    [switch]$ShowCurrent,
    [switch]$ValidateOnly,
    [switch]$NoRestart,
    [string]$ApplicationDirectory
)

$ErrorActionPreference = 'Stop'
if (-not $ApplicationDirectory) {
    $ApplicationDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
}
$applicationDirectory = $ApplicationDirectory
$executable = Join-Path $applicationDirectory 'OpenSuperWhisper.Windows.exe'
$configurationDirectory = if ($env:OPENSUPERWHISPER_CONFIG_DIR) {
    $env:OPENSUPERWHISPER_CONFIG_DIR
}
else {
    Join-Path $env:LOCALAPPDATA 'OpenSuperWhisper'
}
$configurationPath = Join-Path $configurationDirectory 'hotkey.txt'
$defaultHotkey = 'Shift+|'

if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "OpenSuperWhisper.Windows.exe was not found at $executable"
}

function Get-CurrentHotkey {
    if (Test-Path -LiteralPath $configurationPath -PathType Leaf) {
        $configured = (Get-Content -LiteralPath $configurationPath -Raw).Trim()
        if ($configured) {
            return $configured
        }
    }
    return $defaultHotkey
}

function Test-Hotkey([string]$Candidate) {
    if ([string]::IsNullOrWhiteSpace($Candidate)) {
        return $false
    }

    $validation = Start-Process `
        -FilePath $executable `
        -ArgumentList ("--validate-hotkey=" + $Candidate.Trim()) `
        -WindowStyle Hidden `
        -Wait `
        -PassThru
    return $validation.ExitCode -eq 0
}

if ($ShowCurrent) {
    Write-Output (Get-CurrentHotkey)
    return
}

$interactive = -not $PSBoundParameters.ContainsKey('Hotkey')
$currentHotkey = Get-CurrentHotkey

if ($interactive) {
    Write-Host ''
    Write-Host 'OpenSuperWhisper microphone shortcut' -ForegroundColor Cyan
    Write-Host 'Enter the keyboard shortcut that will start and stop recording.'
    Write-Host 'Examples: Ctrl+Alt+M, Shift+|, Alt+F8, Ctrl+Shift+Space'
    Write-Host 'Supported modifiers: Ctrl, Alt, Shift, Win'
    Write-Host 'Supported keys: A-Z, 0-9, F1-F24, Space, arrows, and common punctuation names.'
    Write-Host ''
}

while ($true) {
    if ($interactive) {
        $candidate = Read-Host "Shortcut [$currentHotkey]"
        if ([string]::IsNullOrWhiteSpace($candidate)) {
            $candidate = $currentHotkey
        }
    }
    else {
        $candidate = $Hotkey
    }

    $candidate = $candidate.Trim()
    if (Test-Hotkey $candidate) {
        break
    }

    if (-not $interactive) {
        throw "Unsupported shortcut '$candidate'. Example: Ctrl+Alt+M"
    }

    Write-Host "'$candidate' is not supported. Try a combination such as Ctrl+Alt+M." -ForegroundColor Yellow
}

if ($ValidateOnly) {
    Write-Output $candidate
    return
}

New-Item -ItemType Directory -Path $configurationDirectory -Force | Out-Null
$utf8WithoutBom = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText($configurationPath, $candidate, $utf8WithoutBom)
Write-Host "Shortcut saved: $candidate" -ForegroundColor Green

if (-not $NoRestart) {
    & taskkill.exe /IM 'OpenSuperWhisper.Windows.exe' /F 2>$null | Out-Null
    Start-Sleep -Milliseconds 400
    Start-Process -FilePath $executable -ArgumentList '--background' -WindowStyle Hidden
    Write-Host 'OpenSuperWhisper restarted. The new shortcut is active.' -ForegroundColor Green
}
