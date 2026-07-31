# OpenSuperWhisper for Windows

Fast, private, system-wide voice dictation for Windows with a shortcut you
choose. Press your shortcut to record, press it again to stop, and the
transcription is copied and pasted into the app you were using.

- Local transcription with [whisper.cpp](https://github.com/ggml-org/whisper.cpp)
- No account, API key, subscription, or cloud upload
- Runs from the Windows notification area
- Starts automatically when you sign in
- Multilingual Whisper `base` model included in each release

## Install

Open **Command Prompt (CMD)**. Administrator access is not required. Paste this
command and press Enter:

```bat
powershell -NoProfile -ExecutionPolicy Bypass -Command "irm 'https://raw.githubusercontent.com/Danix2308/OpenSuperWhisper-Windows/main/install.ps1' | iex"
```

The installer downloads the latest release, verifies its SHA-256 checksum,
installs it under `%LOCALAPPDATA%\Programs\OpenSuperWhisper`, creates Desktop
and Startup shortcuts, then asks what keyboard command should activate the
microphone. Press Enter to keep the default `Shift+|`, or type a combination
such as `Ctrl+Alt+M`, `Alt+F8`, or `Ctrl+Shift+Space`.

Windows SmartScreen may warn that the executable has an unknown publisher. The
current builds are not code-signed. You can inspect this repository and the
public GitHub Actions build before running it.

## Use

1. Make sure OpenSuperWhisper is running in the notification area.
2. Press the shortcut selected during installation to start.
3. Speak, then press the same shortcut again.
4. The text is copied and pasted into the previously active app.

Recordings and text files are stored in
`%LOCALAPPDATA%\OpenSuperWhisper\Recordings`. Logs are written to
`%LOCALAPPDATA%\OpenSuperWhisper\OpenSuperWhisper.log`.

The shortcut is global while the app is running. Exit from the tray icon to
release it.

## Change the microphone shortcut

Open the **OpenSuperWhisper Settings** shortcut on the Desktop. It opens a CMD
menu where you can view or change the hotkey, start or stop the app, and open
the recordings folder.

You can open the same menu from any Command Prompt:

```bat
"%LOCALAPPDATA%\Programs\OpenSuperWhisper\OpenSuperWhisper.cmd"
```

Or jump directly to the shortcut prompt:

```bat
"%LOCALAPPDATA%\Programs\OpenSuperWhisper\OpenSuperWhisper.cmd" hotkey
```

Changing the shortcut restarts the tray app automatically. Supported modifiers
are `Ctrl`, `Alt`, `Shift`, and `Win`. Supported keys include `A-Z`, `0-9`,
`F1-F24`, `Space`, arrow/navigation keys, and common punctuation names such as
`Pipe`, `Backtick`, `Plus`, `Minus`, and `Slash`. At least one modifier is
required so a normal typing key cannot be captured globally by itself.

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
