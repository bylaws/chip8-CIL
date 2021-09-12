using Chip8_CIL.Chip8;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;

namespace Chip8_CIL
{
    public partial class MainForm : Form
    {
        private bool _paused;
        private Chip8System _system;
        private Logger _logger;

        class GuiChip8SystemCallbacks : IChip8SystemCallbacks
        {
            private MainForm _parent;

            private int _delayTimerThresh = 0;
            private readonly Stopwatch _delayTimer = new();
            private readonly Random _rand = new();

            // Map C8 keycodes to ASCII chars and vice-versa
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

                if (KeyMapReverse.Count == 0)
                    foreach (KeyCode key in KeyMap.Keys)
                        KeyMapReverse[KeyMap[key]] = key;
            }

            byte IChip8SystemCallbacks.DelayTimer
            {
                get
                {
                    _delayTimer.Stop();

                    int ticks = _delayTimerThresh - (int)(_delayTimer.Elapsed.TotalMilliseconds / Chip8System.DelayTimerIntervalMs);
                    if (ticks > 0) // Resume the timer if it isn't finished yet
                        _delayTimer.Start();

                    return (ticks > 0) ? (byte)ticks : (byte)0;
                }
                set
                {
                    _delayTimerThresh = value;
                    _delayTimer.Restart();
                }
            }

            void IChip8SystemCallbacks.BlitFramebuffer(byte[,] framebuffer)
            {
                Terminal.DrawBuffer(framebuffer);
            }

            void IChip8SystemCallbacks.InitialiseFramebuffer(int width, int height)
            {
                Terminal.InitialiseBuffering(width, height);
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
                throw new NotImplementedException();
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
                                  new string('\t', indent) + msg +Environment.NewLine);
            logTextBox.Update();
        }

        private void logLevelComboBox_SelectedIndexChanged(object sender, EventArgs e) =>
            _logger.CurrentLevel = (Logger.Level)logLevelComboBox.SelectedValue;


        [System.Runtime.InteropServices.DllImport("kernel32")]
        static extern int AllocConsole();

        private void openButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog fileDialog = new();
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                string rom = fileDialog.FileName;
                AllocConsole();

                _system = new(File.ReadAllBytes("FONT"), File.ReadAllBytes(rom), new GuiChip8SystemCallbacks(this), _logger);


                Task.Run(() =>
                {

                    _system.Run();
                });
            }

            //framebufferPictureBox.ik
        }

        private void pauseButton_Click(object sender, EventArgs e)
        {
            _paused = !_paused;
            //pauseButton.Image
         //   pauseButton.
            // if (_system != null)
            //     if (_paused
        }


    }
}
