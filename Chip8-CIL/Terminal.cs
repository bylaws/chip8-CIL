using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Chip8_CIL
{
    // 
    static class Terminal
    {
        private const int STD_INPUT_HANDLE = -10;
        private const int STD_OUTPUT_HANDLE = -11;
        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        private const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;
        private const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200;

        private static int _charWidth;
        private static int _charHeight;

        private static int _width;
        private static int _widthBytes;
        private static int _height;

        private static StringBuilder _builder;

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();

        // Enables virtual terminal mode for our output console
        private static bool InitialiseVT()
        {
            var stdIn = GetStdHandle(STD_INPUT_HANDLE);
            var stdOut = GetStdHandle(STD_OUTPUT_HANDLE);

            if (!GetConsoleMode(stdIn, out uint inConsoleMode) ||  !GetConsoleMode(stdOut, out uint outConsoleMode))
            {
                return false;
            }

            inConsoleMode |= ENABLE_VIRTUAL_TERMINAL_INPUT;
            outConsoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;

            return SetConsoleMode(stdIn, inConsoleMode) && SetConsoleMode(stdOut, outConsoleMode);
        }

        // Initialises the terminal to buffer an image
        public static void InitialiseBuffering(int width, int height)
        {
            InitialiseVT();

            Console.CursorVisible = false;

            _charWidth = (width + 1) * 2;
            _charHeight = height + 1;

            Console.SetWindowSize(_charWidth, _charHeight);
            Console.SetBufferSize(_charWidth, _charHeight * 2); // Double buffer

            _builder = new(_charHeight * _charWidth);
            _builder.Append(' ', _charWidth * _charHeight);

            _width = width;
            _widthBytes = width / 8;
            _height = height;

        }

        // Draws a 1BPP monochrome framebuffer to the terminal
        public static void DrawBuffer(byte[,] buffer)
        {
            for (int row = 0; row < _height; row++)
            {
                for (int col = 0; col < _widthBytes; col++)
                {
                    byte b = buffer[col, row];
                    for (int bit = 7; bit >= 0; bit--)
                    {
                        int baseOffset = (row * _charWidth) + 2 * (col * 8 + bit);
                        if ((b & 1) == 1)
                        {
                            _builder[baseOffset] = '█';
                            _builder[baseOffset + 1] = '█';
                        }
                        else
                        {
                            _builder[baseOffset] = ' ';
                            _builder[baseOffset + 1] = ' ';
                        }

                        b >>= 1;
                    }
                }
            }

            Console.SetCursorPosition(0, 32);
            Console.Write(_builder.ToString());
        }

        public static void SwitchToAlternativeBuffer() => Console.Write("\x1b[?1049h");

        public static void SwitchToMainBuffer() => Console.Write("\x1b[?1049l");
    }
}
