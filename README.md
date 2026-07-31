# OpenSuperWhisper for Windows

Fast, private, system-wide voice dictation for Windows. Press **Shift+|** to
record, press it again to stop, and the transcription is copied and pasted into
the app you were using.

- Local transcription with [whisper.cpp](https://github.com/ggml-org/whisper.cpp)
- No account, API key, subscription, or cloud upload
- Runs from the Windows notification area
- Starts automatically when you sign in
- Multilingual Whisper `base` model included in each release

## Install

Open **PowerShell** (administrator access is not required) and run:

```powershell
irm https://raw.githubusercontent.com/Danix2308/OpenSuperWhisper-Windows/main/install.ps1 | iex
```

The installer downloads the latest release, verifies its SHA-256 checksum,
installs it under `%LOCALAPPDATA%\Programs\OpenSuperWhisper`, creates Desktop
and Startup shortcuts, and launches the app in the background.

Windows SmartScreen may warn that the executable has an unknown publisher. The
current builds are not code-signed. You can inspect this repository and the
public GitHub Actions build before running it.

## Use

1. Make sure OpenSuperWhisper is running in the notification area.
2. Press **Shift+|** (Shift plus the `\ |` key on a US keyboard) to start.
3. Speak, then press **Shift+|** again.
4. The text is copied and pasted into the previously active app.

Recordings and text files are stored in
`%LOCALAPPDATA%\OpenSuperWhisper\Recordings`. Logs are written to
`%LOCALAPPDATA%\OpenSuperWhisper\OpenSuperWhisper.log`.

The shortcut is global while the app is running. Exit from the tray icon to
release it. The current shortcut mapping targets a US keyboard layout.

## Uninstall

Run the included `uninstall.ps1` from the installation directory. It removes
the app and its shortcuts. Your recordings and logs are kept unless you pass
`-RemoveUserData`:

```powershell
& "$env:LOCALAPPDATA\Programs\OpenSuperWhisper\uninstall.ps1"
```

## Build from source

Requirements: 64-bit Windows 10/11 and .NET Framework 4.8.

```powershell
git clone https://github.com/Danix2308/OpenSuperWhisper-Windows.git
cd OpenSuperWhisper-Windows
.\setup-windows.ps1
.\dist\OpenSuperWhisper.Windows.exe
```

`setup-windows.ps1` downloads pinned whisper.cpp binaries and the multilingual
Whisper base model, verifies both hashes, and compiles the WinForms app with the
.NET Framework C# compiler included in Windows.

Run repository checks with:

```powershell
.\tests\Test.ps1
```

## Privacy and security

Microphone audio and transcription stay on your computer. The app does not use
an online transcription API. The installer only contacts GitHub to retrieve a
release and verifies the published checksum before installation.

## Credits and license

This is an independent Windows adaptation of
[OpenSuperWhisper](https://github.com/Starmel/OpenSuperWhisper), originally a
macOS application. It uses [whisper.cpp](https://github.com/ggml-org/whisper.cpp)
and OpenAI Whisper model weights. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

Released under the MIT License. This project is not affiliated with or endorsed
by OpenAI.
