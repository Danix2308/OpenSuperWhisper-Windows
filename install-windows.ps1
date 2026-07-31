$ErrorActionPreference = 'Stop'

$projectDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$executable = Join-Path $projectDirectory 'dist\OpenSuperWhisper.Windows.exe'
$userShellFolders = Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders'
$desktop = [Environment]::ExpandEnvironmentVariables($userShellFolders.Desktop)
$startup = [Environment]::ExpandEnvironmentVariables($userShellFolders.Startup)
$desktopShortcut = Join-Path $desktop 'OpenSuperWhisper.lnk'
$startupShortcut = Join-Path $startup 'OpenSuperWhisper.lnk'

if (-not (Test-Path -LiteralPath $executable)) {
    throw "The application was not found at $executable. Run setup-windows.ps1 first."
}

New-Item -ItemType Directory -Path $desktop, $startup -Force | Out-Null

$shell = New-Object -ComObject WScript.Shell

$shortcut = $shell.CreateShortcut($desktopShortcut)
$shortcut.TargetPath = $executable
$shortcut.WorkingDirectory = Split-Path -Parent $executable
$shortcut.Description = 'OpenSuperWhisper for Windows - Shift+| dictation'
$shortcut.Save()

$shortcut = $shell.CreateShortcut($startupShortcut)
$shortcut.TargetPath = $executable
$shortcut.Arguments = '--background'
$shortcut.WorkingDirectory = Split-Path -Parent $executable
$shortcut.Description = 'Start OpenSuperWhisper in the background'
$shortcut.Save()

Write-Host "Desktop shortcut: $desktopShortcut"
Write-Host "Automatic startup: $startupShortcut"
