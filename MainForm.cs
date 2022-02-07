using Chip8_CIL.Chip8;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Chip8_CIL
{
    public partial class MainForm : Form
    {
        private bool _paused;
        private Chip8System _system;
        private readonly Logger _logger;
        private bool _instantDelayTimer;
        private Bitmap _outputBuffer;
        private object _renderLock = new();

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;  // Turn on WS_EX_COMPOSITED
                return cp;
            }
        }

        class GuiChip8SystemCallbacks : IChip8SystemCallbacks
        {
            private MainForm _parent;

            private int _delayTimerThresh = 0;
            private int _soundTimer = 0;
            private readonly Stopwatch _delayTimer = new();
            private readonly Random _rand = new();
            private Bitmap _framebuffer;
            private SoundPlayer _sound = new(@"c:\Windows\Media\chimes.wav");
            private object _initLock = new();


            private Rectangle _framebufferRect;

            // Map C8 keycodes to ASCII chars and vice-versa
            // https://camo.githubusercontent.com/72f35a020d46b945f194ad81f4214c56f9025cd9eceb739e0e3420ddaa5c50e0/687474703a2f2f7777772e72616475616e67656c657363752e636f6d2f696d616765732f6b65796d617070696e672e706e67
            private static readonly Dictionary<char, KeyCode> KeyMapReverse = new();
            private static readonly Dictionary<KeyCode, char> KeyMap = new()
            {
                { KeyCode.K1, '1' },
                { KeyCode.K2, '2' },
                { KeyCode.K3, '3' },
                { KeyCode.KC, '4' },
                { KeyCode.K4, 'Q' },
                { KeyCode.K5, 'W' },
                { KeyCode.K6, 'E' },
                { KeyCode.KD, 'R' },
                { KeyCode.K7, 'A' },
                { KeyCode.K8, 'S' },
                { KeyCode.K9, 'D' },
                { KeyCode.KE, 'F' },
                { KeyCode.KA, 'Z' },
                { KeyCode.K0, 'X' },
                { KeyCode.KB, 'C' },
                { KeyCode.KF, 'V' },
            };

            public GuiChip8SystemCallbacks(MainForm parent)
            {
                _parent = parent;

                Task.Run(SoundTimer);

                if (KeyMapReverse.Count == 0)
                    foreach (KeyCode key in KeyMap.Keys)
                        KeyMapReverse[KeyMap[key]] = key;
            }

            byte IChip8SystemCallbacks.DelayTimer
            {
                get
                {
                    if (_parent._instantDelayTimer)
                        return 0;

                    _delayTimer.Stop();

                    int ticks = _delayTimerThresh - (int)(_delayTimer.Elapsed.TotalMilliseconds / Chip8System.DelayTimerIntervalMs);
                    if (ticks > 0) // Resume the timer if it isn't finished yet
                        _delayTimer.Start();

                    return (ticks > 0) ? (byte)ticks : (byte)0;
                }
                set
                {
                    if (_parent._instantDelayTimer)
                        return;

                    _delayTimerThresh = value;
                    _delayTimer.Restart();
                }
            }

            byte IChip8SystemCallbacks.SoundTimer
            {
                get => (byte)_soundTimer;
                set => _soundTimer = value;
            }

            void IChip8SystemCallbacks.BlitFramebuffer(byte[,] framebuffer)
            {
                lock (_initLock)
                {
                    BitmapData imgData = _framebuffer.LockBits(_framebufferRect, ImageLockMode.WriteOnly, PixelFormat.Format16bppRgb565);


                    const int pixelFormatBytesPerColour = 2;

                    byte[] linearFramebuffer = new byte[_framebufferRect.Width * _framebufferRect.Height * pixelFormatBytesPerColour];
                    for (int x = 0; x < framebuffer.GetLength(0); x++)
                    {
                        for (int y = 0; y < framebuffer.GetLength(1); y++)
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                if (((framebuffer[x, y] >> i) & 1) == 1)
                                {
                                    for (int k = 0; k < pixelFormatBytesPerColour; k++)
                                    {
                                        linearFramebuffer[pixelFormatBytesPerColour * (x * 8 + (7 - i) + (y * _framebufferRect.Width)) + k] = 0xff;
                                    }
                                }
                            }
                        }
                    }
                    Marshal.Copy(linearFramebuffer, 0, imgData.Scan0, linearFramebuffer.Length);

                    _framebuffer.UnlockBits(imgData);

                    lock (_parent._renderLock)
                    {
                        Bitmap tmp = _parent._outputBuffer;

                        _parent._outputBuffer = _framebuffer;

                        _framebuffer = tmp;
                    }
                }
            }

            void IChip8SystemCallbacks.InitialiseFramebuffer(int width, int height)
            {
                lock (_initLock)
                {
                    _framebuffer = new(width, height);
                    _parent._outputBuffer = new(width, height);

                    _framebufferRect = new(0, 0, width, height);
                }
            }

            byte IChip8SystemCallbacks.GenerateRandomByte() => (byte)_rand.Next(0, 0x100);

            [DllImport("user32.dll")]
            static extern short GetAsyncKeyState(int key);

            bool IChip8SystemCallbacks.IsKeyPressed(KeyCode key)
            {
                return (GetAsyncKeyState(KeyMap[key]) & 0x8000) != 0;
            }

            KeyCode IChip8SystemCallbacks.WaitKey()
            {
                while (true)
                { 
                    foreach (KeyCode key in KeyMap.Keys)
                    {
                        if ((GetAsyncKeyState(KeyMap[key]) & 0x8000) != 0)
                            return key;
                    }
                }
            }

            Task SoundTimer()
            {
                while (true)
                {
                    System.Threading.Thread.Sleep((int)Chip8System.DelayTimerIntervalMs);
                    if (_soundTimer > 0)
                    {
                        _soundTimer--;

                        if (_soundTimer == 0)
                            SystemSounds.Beep.Play();
                    } 
                }
            }


            void IChip8SystemCallbacks.UpdateState()
            {
                // Block until unpaused
                while (_parent._paused) ;
            }
        }

        public MainForm()
        {
            _logger = new(Logger.Level.Trace, OutputLogMessage);

            InitializeComponent();

            logLevelComboBox.DataSource = Enum.GetValues(typeof(Logger.Level));

            _paused = pauseButton.Checked;

            _instantDelayTimer = toggleSpeedLimitButton.Checked;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);



        }

        Task PictureBoxRefresher()
        {
            while (true)
            {
                System.Threading.Thread.Sleep((int)Chip8System.DisplayRefreshIntervalMs);
                RefreshPictureBox();
            }
        }


        private void RefreshPictureBox()
        {
            if (framebufferPictureBox.InvokeRequired)
            {

                framebufferPictureBox.Invoke(new Action(RefreshPictureBox));
                return;
            }

            framebufferPictureBox.Invalidate();
        }

        private void OutputLogMessage(Logger.Level level, int indent, string msg)
        {
            static char LogLevelPrefix(Logger.Level level)
            {
                return level switch
                {
                    Logger.Level.Trace => 'T',
                    Logger.Level.Verbose => 'V',
                    Logger.Level.Debug => 'T',
                    Logger.Level.Warning => 'V',
                    Logger.Level.Fatal => 'F',
                    _ => '?',
                };
            }


            // Logger may be called on a non-UI thread
            if (logTextBox.InvokeRequired)
            {
                logTextBox.Invoke(new Logger.LoggerOutputAction(OutputLogMessage), new object[] { level, indent, msg });
                return;
            }
            
            logTextBox.AppendText(LogLevelPrefix(level).ToString() + ":" +
                                  new string('\t', indent) + msg + Environment.NewLine);
            logTextBox.Update();
        }

        private void logLevelComboBox_SelectedIndexChanged(object sender, EventArgs e) =>
            _logger.CurrentLevel = (Logger.Level)logLevelComboBox.SelectedValue;

        private void openButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog fileDialog = new();
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                string rom = fileDialog.FileName;

                _system = new(File.ReadAllBytes("FONT"), File.ReadAllBytes(rom), new GuiChip8SystemCallbacks(this), _logger);


                Task.Run(PictureBoxRefresher);
                Task.Run(() =>
                {

                    _system.Run();
                });
            }

            //framebufferPictureBox.ik
        }

        private void pauseButton_Click(object sender, EventArgs e)
        {
            _paused = pauseButton.Checked;
        }

        private void toggleSpeedLimitButton_Click(object sender, EventArgs e)
        {
            _instantDelayTimer = toggleSpeedLimitButton.Checked;
        }

        private void framebufferPictureBox_Click(object sender, EventArgs e)
        {

        }

        private void framebufferPictureBox_Paint(object sender, PaintEventArgs e)
        {
            if (_outputBuffer != null)
            {

                lock (_renderLock)
                {

                        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
                        e.Graphics.DrawImage(_outputBuffer,
                                             new Rectangle(0, 0, framebufferPictureBox.Width, framebufferPictureBox.Height),
                                             0, 0, _outputBuffer.Width, _outputBuffer.Height, GraphicsUnit.Pixel);
                }
            }
        }

        private void toolStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }
    }
}
