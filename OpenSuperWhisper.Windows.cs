using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenSuperWhisperWindows
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (Mutex singleInstance = new Mutex(true, "Local\\OpenSuperWhisper.Windows", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        "OpenSuperWhisper is already running in the notification area.",
                        "OpenSuperWhisper",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs eventArgs)
                {
                    AppLog.Write("UI exception: " + eventArgs.Exception);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs eventArgs)
                {
                    AppLog.Write("Unhandled exception: " + eventArgs.ExceptionObject);
                };

                try
                {
                    AppLog.Write("Application starting.");
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    bool startInBackground = Array.Exists(
                        Environment.GetCommandLineArgs(),
                        delegate(string argument)
                        {
                            return string.Equals(argument, "--background", StringComparison.OrdinalIgnoreCase);
                        });
                    Application.Run(new MainForm(startInBackground));
                }
                catch (Exception exception)
                {
                    AppLog.Write("Startup failed: " + exception);
                    MessageBox.Show(
                        exception.Message,
                        "OpenSuperWhisper could not start",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }
    }

    internal static class AppLog
    {
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenSuperWhisper");

        internal static readonly string LogPath = Path.Combine(LogDirectory, "OpenSuperWhisper.log");

        internal static void Write(string message)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(
                    LogPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch
            {
                // Logging must never prevent dictation from working.
            }
        }
    }

    internal sealed class MainForm : Form
    {
        private const int HotkeyId = 0x5357;
        private const int WmHotkey = 0x0312;
        private const uint ModShift = 0x0004;
        private const uint ModNoRepeat = 0x4000;
        // VK_OEM_5 is the \ | key on the active US keyboard layout.
        private const uint VirtualKeyPipe = 0xDC;
        private const string RecordingAlias = "opensuperwhisper_recording";

        private readonly string appDirectory;
        private readonly string whisperPath;
        private readonly string modelPath;
        private readonly string recordingsDirectory;
        private readonly Label statusLabel;
        private readonly Label detailLabel;
        private readonly Button recordButton;
        private readonly TextBox transcriptionBox;
        private readonly CheckBox autoPasteCheckBox;
        private readonly NotifyIcon trayIcon;

        private AppState state = AppState.Idle;
        private string currentRecordingPath;
        private IntPtr targetWindow;
        private bool allowExit;
        private bool hotkeyRegistered;
        private readonly bool startInBackground;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        private static extern int mciSendString(string command, StringBuilder returnValue, int returnLength, IntPtr callback);

        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        private static extern bool mciGetErrorString(int errorCode, StringBuilder errorText, int errorTextSize);

        public MainForm(bool startInBackground)
        {
            this.startInBackground = startInBackground;
            appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            whisperPath = Path.Combine(appDirectory, "whisper", "whisper-cli.exe");
            modelPath = Path.Combine(appDirectory, "models", "ggml-base.bin");
            recordingsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenSuperWhisper",
                "Recordings");

            Directory.CreateDirectory(recordingsDirectory);

            Text = "OpenSuperWhisper for Windows";
            ClientSize = new Size(620, 470);
            MinimumSize = new Size(560, 420);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(248, 247, 244);
            Font = new Font("Segoe UI", 10F);

            Label titleLabel = new Label();
            titleLabel.Text = "OpenSuperWhisper";
            titleLabel.Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold);
            titleLabel.ForeColor = Color.FromArgb(28, 28, 28);
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(28, 22);
            Controls.Add(titleLabel);

            Label platformLabel = new Label();
            platformLabel.Text = "Native Windows dictation";
            platformLabel.ForeColor = Color.FromArgb(100, 100, 100);
            platformLabel.AutoSize = true;
            platformLabel.Location = new Point(32, 66);
            Controls.Add(platformLabel);

            Panel statusPanel = new Panel();
            statusPanel.BackColor = Color.White;
            statusPanel.BorderStyle = BorderStyle.FixedSingle;
            statusPanel.Location = new Point(30, 101);
            statusPanel.Size = new Size(560, 112);
            statusPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(statusPanel);

            statusLabel = new Label();
            statusLabel.Text = "Ready";
            statusLabel.Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold);
            statusLabel.AutoSize = true;
            statusLabel.Location = new Point(20, 16);
            statusPanel.Controls.Add(statusLabel);

            detailLabel = new Label();
            detailLabel.Text = "Press Shift+| to start recording. Press it again to stop.";
            detailLabel.ForeColor = Color.FromArgb(85, 85, 85);
            detailLabel.AutoSize = true;
            detailLabel.Location = new Point(23, 52);
            statusPanel.Controls.Add(detailLabel);

            recordButton = new Button();
            recordButton.Text = "Start recording  (Shift+|)";
            recordButton.FlatStyle = FlatStyle.Flat;
            recordButton.FlatAppearance.BorderSize = 0;
            recordButton.BackColor = Color.FromArgb(33, 33, 33);
            recordButton.ForeColor = Color.White;
            recordButton.Size = new Size(210, 38);
            recordButton.Location = new Point(350, 64);
            recordButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            recordButton.Click += delegate { ToggleRecording(); };
            statusPanel.Controls.Add(recordButton);

            autoPasteCheckBox = new CheckBox();
            autoPasteCheckBox.Text = "Paste transcription into the active app automatically";
            autoPasteCheckBox.Checked = true;
            autoPasteCheckBox.AutoSize = true;
            autoPasteCheckBox.Location = new Point(32, 231);
            Controls.Add(autoPasteCheckBox);

            Label transcriptionLabel = new Label();
            transcriptionLabel.Text = "Latest transcription";
            transcriptionLabel.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            transcriptionLabel.AutoSize = true;
            transcriptionLabel.Location = new Point(30, 271);
            Controls.Add(transcriptionLabel);

            transcriptionBox = new TextBox();
            transcriptionBox.Multiline = true;
            transcriptionBox.ReadOnly = true;
            transcriptionBox.ScrollBars = ScrollBars.Vertical;
            transcriptionBox.BackColor = Color.White;
            transcriptionBox.BorderStyle = BorderStyle.FixedSingle;
            transcriptionBox.Location = new Point(30, 298);
            transcriptionBox.Size = new Size(560, 120);
            transcriptionBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(transcriptionBox);

            Button recordingsButton = new Button();
            recordingsButton.Text = "Open recordings folder";
            recordingsButton.FlatStyle = FlatStyle.Flat;
            recordingsButton.Location = new Point(30, 430);
            recordingsButton.Size = new Size(165, 30);
            recordingsButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            recordingsButton.Click += delegate
            {
                Process.Start("explorer.exe", "\"" + recordingsDirectory + "\"");
            };
            Controls.Add(recordingsButton);

            ContextMenuStrip trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Show", null, delegate { ShowWindow(); });
            trayMenu.Items.Add("Exit", null, delegate
            {
                allowExit = true;
                Close();
            });

            trayIcon = new NotifyIcon();
            trayIcon.Icon = SystemIcons.Application;
            trayIcon.Text = "OpenSuperWhisper - Shift+|";
            trayIcon.Visible = true;
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.DoubleClick += delegate { ShowWindow(); };

            Resize += delegate
            {
                if (WindowState == FormWindowState.Minimized)
                {
                    Hide();
                }
            };

            FormClosing += OnFormClosing;
            Shown += OnShown;
        }

        private void OnShown(object sender, EventArgs eventArgs)
        {
            hotkeyRegistered = RegisterHotKey(Handle, HotkeyId, ModShift | ModNoRepeat, VirtualKeyPipe);
            if (!hotkeyRegistered)
            {
                int error = Marshal.GetLastWin32Error();
                AppLog.Write("Shift+| registration failed. Win32 error: " + error);
                SetStatus(
                    "Shortcut unavailable",
                    "Shift+| is already reserved by another program. Close that program and restart this app.",
                    Color.FromArgb(160, 65, 45));
            }
            else
            {
                AppLog.Write("Shift+| registered successfully.");
            }

            if (!File.Exists(whisperPath) || !File.Exists(modelPath))
            {
                AppLog.Write("Runtime files missing. Engine: " + File.Exists(whisperPath) + ", model: " + File.Exists(modelPath));
                SetStatus(
                    "Setup incomplete",
                    "The Whisper engine or model is missing. Run setup-windows.ps1.",
                    Color.FromArgb(160, 65, 45));
                recordButton.Enabled = false;
            }

            if (startInBackground)
            {
                BeginInvoke((MethodInvoker)delegate { Hide(); });
            }
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WmHotkey && message.WParam.ToInt32() == HotkeyId)
            {
                AppLog.Write("Shift+| received. Current state: " + state);
                ToggleRecording();
                return;
            }

            base.WndProc(ref message);
        }

        private void ToggleRecording()
        {
            if (state == AppState.Transcribing)
            {
                trayIcon.ShowBalloonTip(1200, "OpenSuperWhisper", "Please wait for transcription to finish.", ToolTipIcon.Info);
                return;
            }

            if (state == AppState.Recording)
            {
                StopAndTranscribe();
            }
            else
            {
                StartRecording();
            }
        }

        private void StartRecording()
        {
            try
            {
                AppLog.Write("Starting microphone recording.");
                targetWindow = GetForegroundWindow();
                currentRecordingPath = Path.Combine(
                    recordingsDirectory,
                    "recording-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".wav");

                SendMci("close " + RecordingAlias, true);
                SendMci("open new type waveaudio alias " + RecordingAlias, false);
                SendMci(
                    "set " + RecordingAlias +
                    " time format milliseconds format tag pcm bitspersample 16 channels 1 samplespersec 16000 bytespersec 32000 alignment 2",
                    false);
                SendMci("record " + RecordingAlias, false);

                state = AppState.Recording;
                SetStatus(
                    "Recording...",
                    "Speak now. Press Shift+| again to stop and transcribe.",
                    Color.FromArgb(182, 45, 45));
                recordButton.Text = "Stop and transcribe  (Shift+|)";
                recordButton.BackColor = Color.FromArgb(182, 45, 45);
                trayIcon.Text = "OpenSuperWhisper - Recording";
                AppLog.Write("Microphone recording started: " + currentRecordingPath);
            }
            catch (Exception exception)
            {
                AppLog.Write("Microphone start failed: " + exception);
                state = AppState.Idle;
                SendMci("close " + RecordingAlias, true);
                ShowError("Could not start the microphone", exception);
            }
        }

        private async void StopAndTranscribe()
        {
            try
            {
                AppLog.Write("Stopping microphone recording.");
                SendMci("stop " + RecordingAlias, false);
                SendMci("save " + RecordingAlias + " \"" + currentRecordingPath + "\"", false);
                SendMci("close " + RecordingAlias, true);

                state = AppState.Transcribing;
                SetStatus(
                    "Transcribing...",
                    "Local Whisper is converting your speech to text.",
                    Color.FromArgb(45, 91, 150));
                recordButton.Enabled = false;
                recordButton.Text = "Transcribing...";
                trayIcon.Text = "OpenSuperWhisper - Transcribing";

                string text = await Task.Run(delegate { return Transcribe(currentRecordingPath); });
                text = text.Trim();
                AppLog.Write("Transcription completed. Characters: " + text.Length);

                transcriptionBox.Text = text;
                if (text.Length == 0)
                {
                    SetStatus(
                        "No speech detected",
                        "Try again and speak closer to the microphone.",
                        Color.FromArgb(160, 100, 30));
                }
                else
                {
                    Clipboard.SetText(text);
                    SetStatus(
                        "Transcription ready",
                        autoPasteCheckBox.Checked
                            ? "The text was copied and pasted into the active app."
                            : "The text was copied to the clipboard.",
                        Color.FromArgb(33, 120, 74));

                    if (autoPasteCheckBox.Checked)
                    {
                        PasteIntoTargetWindow();
                    }
                }
            }
            catch (Exception exception)
            {
                AppLog.Write("Transcription failed: " + exception);
                ShowError("Transcription failed", exception);
            }
            finally
            {
                state = AppState.Idle;
                recordButton.Enabled = true;
                recordButton.Text = "Start recording  (Shift+|)";
                recordButton.BackColor = Color.FromArgb(33, 33, 33);
                trayIcon.Text = "OpenSuperWhisper - Shift+|";
            }
        }

        private string Transcribe(string audioPath)
        {
            string outputBase = Path.Combine(
                Path.GetDirectoryName(audioPath),
                Path.GetFileNameWithoutExtension(audioPath) + "-transcription");
            string outputTextPath = outputBase + ".txt";

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = whisperPath;
            startInfo.Arguments =
                "-m \"" + modelPath + "\" " +
                "-f \"" + audioPath + "\" " +
                "-l auto -nt -otxt -of \"" + outputBase + "\"";
            startInfo.WorkingDirectory = Path.GetDirectoryName(whisperPath);
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            using (Process process = Process.Start(startInfo))
            {
                Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                Task.WaitAll(standardOutputTask, standardErrorTask);

                string standardOutput = standardOutputTask.Result;
                string standardError = standardErrorTask.Result;

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "Whisper exited with code " + process.ExitCode + "." +
                        Environment.NewLine + standardError + Environment.NewLine + standardOutput);
                }
            }

            if (!File.Exists(outputTextPath))
            {
                throw new FileNotFoundException("Whisper did not create a transcription file.", outputTextPath);
            }

            return File.ReadAllText(outputTextPath, Encoding.UTF8);
        }

        private void PasteIntoTargetWindow()
        {
            if (targetWindow == IntPtr.Zero)
            {
                return;
            }

            SetForegroundWindow(targetWindow);
            System.Threading.Thread.Sleep(100);

            const byte VirtualKeyControl = 0x11;
            const byte VirtualKeyV = 0x56;
            const uint KeyUp = 0x0002;

            keybd_event(VirtualKeyControl, 0, 0, UIntPtr.Zero);
            keybd_event(VirtualKeyV, 0, 0, UIntPtr.Zero);
            keybd_event(VirtualKeyV, 0, KeyUp, UIntPtr.Zero);
            keybd_event(VirtualKeyControl, 0, KeyUp, UIntPtr.Zero);
        }

        private static void SendMci(string command, bool ignoreErrors)
        {
            int result = mciSendString(command, null, 0, IntPtr.Zero);
            if (result == 0 || ignoreErrors)
            {
                return;
            }

            StringBuilder errorText = new StringBuilder(256);
            mciGetErrorString(result, errorText, errorText.Capacity);
            throw new InvalidOperationException(
                "Windows audio error " + result + ": " + errorText + Environment.NewLine +
                "Command: " + command);
        }

        private void SetStatus(string status, string detail, Color color)
        {
            statusLabel.Text = status;
            statusLabel.ForeColor = color;
            detailLabel.Text = detail;
        }

        private void ShowError(string title, Exception exception)
        {
            state = AppState.Idle;
            SetStatus(title, exception.Message, Color.FromArgb(160, 65, 45));
            recordButton.Enabled = true;
            recordButton.Text = "Start recording  (Shift+|)";
            recordButton.BackColor = Color.FromArgb(33, 33, 33);
            trayIcon.Text = "OpenSuperWhisper - Shift+|";
            MessageBox.Show(this, exception.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void ShowWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs eventArgs)
        {
            if (!allowExit && eventArgs.CloseReason == CloseReason.UserClosing)
            {
                eventArgs.Cancel = true;
                Hide();
                trayIcon.ShowBalloonTip(
                    1200,
                    "OpenSuperWhisper is still running",
                    "Press Shift+| to dictate, or use the tray icon to exit.",
                    ToolTipIcon.Info);
                return;
            }

            if (state == AppState.Recording)
            {
                SendMci("stop " + RecordingAlias, true);
                SendMci("close " + RecordingAlias, true);
            }

            if (hotkeyRegistered)
            {
                UnregisterHotKey(Handle, HotkeyId);
                AppLog.Write("Shift+| unregistered.");
            }

            trayIcon.Visible = false;
            trayIcon.Dispose();
            AppLog.Write("Application exiting.");
        }

        private enum AppState
        {
            Idle,
            Recording,
            Transcribing
        }
    }
}
