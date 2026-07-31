[CmdletBinding()]
param(
    [switch]$RemoveUserData
)

$ErrorActionPreference = 'Stop'
$installDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$userShellFolders = Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders'
$desktop = [Environment]::ExpandEnvironmentVariables($userShellFolders.Desktop)
$startup = [Environment]::ExpandEnvironmentVariables($userShellFolders.Startup)

Get-Process -Name 'OpenSuperWhisper.Windows' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($installDirectory, [StringComparison]::OrdinalIgnoreCase) } |
    Stop-Process -Force

foreach ($shortcutPath in @(
    (Join-Path $desktop 'OpenSuperWhisper.lnk'),
    (Join-Path $desktop 'OpenSuperWhisper Settings.lnk'),
    (Join-Path $startup 'OpenSuperWhisper.lnk')
)) {
    if (Test-Path -LiteralPath $shortcutPath) {
        Remove-Item -LiteralPath $shortcutPath -Force
    }
}

if ($RemoveUserData) {
    $userData = Join-Path $env:LOCALAPPDATA 'OpenSuperWhisper'
    if (Test-Path -LiteralPath $userData) {
        Remove-Item -LiteralPath $userData -Recurse -Force
    }
}

$cleanupScript = Join-Path ([IO.Path]::GetTempPath()) ("Uninstall-OpenSuperWhisper-" + [Guid]::NewGuid().ToString('N') + '.cmd')
$commands = @(
    '@echo off',
    'timeout /t 2 /nobreak >nul',
    ('rmdir /s /q "{0}"' -f $installDirectory),
    'del /q "%~f0"'
)
[IO.File]::WriteAllLines($cleanupScript, $commands, [Text.Encoding]::ASCII)
Start-Process -FilePath $env:ComSpec -ArgumentList "/c `"$cleanupScript`"" -WindowStyle Hidden

Write-Host 'OpenSuperWhisper was uninstalled.' -ForegroundColor Green
if (-not $RemoveUserData) {
    Write-Host 'Recordings and logs were kept. Use -RemoveUserData to remove them too.'
}
