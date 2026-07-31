$ErrorActionPreference = 'Stop'

$projectDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$distDirectory = Join-Path $projectDirectory 'dist'
$downloadsDirectory = Join-Path $projectDirectory '.downloads'
$whisperZip = Join-Path $downloadsDirectory 'whisper-bin-x64-v1.9.1.zip'
$whisperExtract = Join-Path $downloadsDirectory 'whisper-bin-x64-v1.9.1'
$whisperDestination = Join-Path $distDirectory 'whisper'
$modelDirectory = Join-Path $distDirectory 'models'
$modelPath = Join-Path $modelDirectory 'ggml-base.bin'

$whisperUrl = 'https://github.com/ggml-org/whisper.cpp/releases/download/v1.9.1/whisper-bin-x64.zip'
$modelUrl = 'https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin?download=true'
$expectedWhisperSha256 = '7D8BE46ECD31828E1EB7A2ECDD0D6B314FEAFD82163038AB6092594B0A063539'
$expectedModelSha256 = '60ED5BC3DD14EEA856493D334349B405782DDCAF0028D4B5DF4088345FBA2EFE'

New-Item -ItemType Directory -Path $downloadsDirectory, $distDirectory, $modelDirectory -Force | Out-Null

if (-not (Test-Path -LiteralPath $whisperZip)) {
    Write-Host 'Downloading whisper.cpp for Windows...'
    & curl.exe -L --fail --retry 3 --retry-delay 2 -o $whisperZip $whisperUrl
    if ($LASTEXITCODE -ne 0) {
        throw "whisper.cpp download failed with exit code $LASTEXITCODE"
    }
}

$actualWhisperSha256 = (Get-FileHash -LiteralPath $whisperZip -Algorithm SHA256).Hash
if ($actualWhisperSha256 -ne $expectedWhisperSha256) {
    throw "whisper.cpp archive hash mismatch. Expected $expectedWhisperSha256, got $actualWhisperSha256"
}

if (Test-Path -LiteralPath $whisperExtract) {
    Remove-Item -LiteralPath $whisperExtract -Recurse -Force
}
New-Item -ItemType Directory -Path $whisperExtract -Force | Out-Null
Expand-Archive -LiteralPath $whisperZip -DestinationPath $whisperExtract -Force

$releaseDirectory = Join-Path $whisperExtract 'Release'
if (-not (Test-Path -LiteralPath (Join-Path $releaseDirectory 'whisper-cli.exe'))) {
    throw 'whisper-cli.exe was not found in the verified release archive.'
}

if (Test-Path -LiteralPath $whisperDestination) {
    Remove-Item -LiteralPath $whisperDestination -Recurse -Force
}
Copy-Item -LiteralPath $releaseDirectory -Destination $whisperDestination -Recurse

if (-not (Test-Path -LiteralPath $modelPath)) {
    Write-Host 'Downloading the multilingual Whisper base model (about 142 MiB)...'
    & curl.exe -L --fail --retry 3 --retry-delay 2 -o $modelPath $modelUrl
    if ($LASTEXITCODE -ne 0) {
        throw "Whisper model download failed with exit code $LASTEXITCODE"
    }
}

$actualModelSha256 = (Get-FileHash -LiteralPath $modelPath -Algorithm SHA256).Hash
if ($actualModelSha256 -ne $expectedModelSha256) {
    throw "Whisper model hash mismatch. Expected $expectedModelSha256, got $actualModelSha256"
}

& (Join-Path $projectDirectory 'build-windows.ps1')
Write-Host ''
Write-Host "Windows app ready: $(Join-Path $distDirectory 'OpenSuperWhisper.Windows.exe')"
