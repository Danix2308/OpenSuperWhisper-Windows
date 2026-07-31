$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

$requiredFiles = @(
    'OpenSuperWhisper.Windows.cs',
    'build-windows.ps1',
    'setup-windows.ps1',
    'install.ps1',
    'uninstall.ps1',
    'README.md',
    'LICENSE',
    'THIRD_PARTY_NOTICES.md'
)

foreach ($relativePath in $requiredFiles) {
    $path = Join-Path $repositoryRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required file is missing: $relativePath"
    }
}

$parseFailures = @()
Get-ChildItem -LiteralPath $repositoryRoot -Recurse -Filter '*.ps1' |
    Where-Object { $_.FullName -notlike '*\.downloads\*' -and $_.FullName -notlike '*\dist\*' } |
    ForEach-Object {
        $tokens = $null
        $errors = $null
        [Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref]$tokens, [ref]$errors) | Out-Null
        if ($errors.Count -gt 0) {
            $parseFailures += "$($_.FullName): $($errors -join '; ')"
        }
    }
if ($parseFailures.Count -gt 0) {
    throw ($parseFailures -join [Environment]::NewLine)
}

$trackedText = Get-ChildItem -LiteralPath $repositoryRoot -Recurse -File |
    Where-Object {
        $_.FullName -notlike '*\.git\*' -and
        $_.FullName -notlike '*\.downloads\*' -and
        $_.FullName -notlike '*\dist\*' -and
        $_.Extension -in '.cs', '.ps1', '.md', '.yml', '.yaml', '.txt'
    } |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
if (($trackedText -join "`n") -match 'C:\\Users\\') {
    throw 'A user-specific absolute path was found in publishable files.'
}

$source = Get-Content -LiteralPath (Join-Path $repositoryRoot 'OpenSuperWhisper.Windows.cs') -Raw
foreach ($expectedText in @('VirtualKeyPipe', 'ModShift | ModNoRepeat', 'Shift+| registered successfully.', 'whisper-cli.exe', 'ggml-base.bin')) {
    if (-not $source.Contains($expectedText)) {
        throw "Expected implementation marker is missing: $expectedText"
    }
}

$testBuildDirectory = Join-Path ([IO.Path]::GetTempPath()) ("OpenSuperWhisper-Test-" + [Guid]::NewGuid().ToString('N'))
try {
    & (Join-Path $repositoryRoot 'build-windows.ps1') -OutputDirectory $testBuildDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }

    $builtExecutable = Join-Path $testBuildDirectory 'OpenSuperWhisper.Windows.exe'
    if (-not (Test-Path -LiteralPath $builtExecutable -PathType Leaf)) {
        throw 'Build did not create OpenSuperWhisper.Windows.exe.'
    }
}
finally {
    if (Test-Path -LiteralPath $testBuildDirectory) {
        Remove-Item -LiteralPath $testBuildDirectory -Recurse -Force
    }
}

Write-Host 'All repository checks passed.' -ForegroundColor Green
