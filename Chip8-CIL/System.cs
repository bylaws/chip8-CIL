using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;


namespace Chip8_CIL
{
    // Is called by the translated code to perform various system functions that rely on external APIs
    public class SystemCallbacks
    {
        private readonly byte[] _memory;
        private readonly Translator _translator;

        private readonly Random _rand = new();

        private bool _extendedMode = false;

        private int _resScale = 1;
        private byte[,] _frameBuffer = new byte[System.ScreenWidthBytes, System.ScreenHeight];


        private int _delayTimerThresh = 0;
        private readonly Stopwatch _delayTimer = new();


        // Map of callbacks to be called when specific keys are pressed, used to make a rudimentary TUI
        private static readonly Dictionary<char, Action<SystemCallbacks>> SystemKeyCallbacks = new()
        {
            {
                'L', (SystemCallbacks cb) => Logger.ShowLog()
            },
            {
                'O', (SystemCallbacks cb) =>
                {
                    Logger.HideLog();
                    Terminal.DrawBuffer(cb._frameBuffer);
                }
            },
            {
                'P', (SystemCallbacks cb) => {
                    while (GetAsyncKeyState('P') != 0);
                }
            },
            {
                'M',
                (SystemCallbacks cb) => {
                    cb._translator.DumpAssembly();
                }
            }
        };

        // Map C8 keycodes to ASCII chars and vice-versa
        private static readonly Dictionary<char, byte> KeyMapReverse = new();
        private static readonly Dictionary<byte, char> KeyMap = new()
        {
            { 1, '1' },
            { 2, '2' },
            { 3, '3' },
            { 0xc, '4' },
            { 4, 'Q' },
            { 5, 'W' },
            { 6, 'E' },
            { 0xd, 'R' },
            { 7, 'A' },
            { 8, 'S' },
            { 9, 'D' },
            { 0xe, 'F' },
            { 0xa, 'Z' },
            { 0, 'X' },
            { 0xb, 'C' },
            { 0xf, 'V' },
        };

        public SystemCallbacks(in byte[] memory, in Translator translator)
        {
            _memory = memory;
            _translator = translator;

            foreach (byte key in KeyMap.Keys)
                KeyMapReverse[KeyMap[key]] = key;
        }

        // Performs periodic tasks like timing and system key callbacks
        public void UpdateSystemState()
        {
            if (!Settings.BenchmarkEnabled && Console.KeyAvailable)
            {
                if (SystemKeyCallbacks.TryGetValue(char.ToUpper(Console.ReadKey(true).KeyChar), out Action<SystemCallbacks> cb)) {
                    cb(this);
                }
            }
        }

        public byte Random()
        {
            UpdateSystemState();
            return (byte)_rand.Next(0, 0x100);
        }

        public void Clear()
        {
            UpdateSystemState();
            Array.Clear(_frameBuffer, 0, _frameBuffer.Length);
        }

        private bool BlitByte(int bitX, int byte0X, int byte1X, int y, ushort data)
        {
            int spriteRowY = y % System.ScreenHeight;

            // Shifts the sprite to handle pixel-level granularity
            byte byte0SpriteData = (byte)(data >> bitX);

            // Masks in bits that were shifted out, then shifts them left to the other end of the byte
            byte byte1SpriteData = (byte)((data & ((1 << bitX) - 1)) << (8 - bitX));

            byte byte0FbDataOld = _frameBuffer[byte0X, spriteRowY];
            byte byte1FbDataOld = _frameBuffer[byte1X, spriteRowY];

            byte byte0FbDataNew = (byte)(byte0SpriteData ^ byte0FbDataOld);
            byte byte1FbDataNew = (byte)(byte1SpriteData ^ byte1FbDataOld);

            // Set flag if any bits have been flipped to a zero
            bool flag = (byte0FbDataOld & byte0FbDataNew) != byte0FbDataOld ||
                   (byte1FbDataOld & byte1FbDataNew) != byte1FbDataOld;

            _frameBuffer[byte0X, spriteRowY] = byte0FbDataNew;
            _frameBuffer[byte1X, spriteRowY] = byte1FbDataNew;

            return flag;
        }

        public int Draw(byte x, byte y, ushort addr, byte size)
        {
            UpdateSystemState();

            x %= (byte)(System.ScreenWidth * _resScale);
            y %= (byte)(System.ScreenHeight * _resScale);

            // Stores if any bits have been flipped from 1->0 during sprite rendering
            bool flag = false;
            int spriteBitX = x & 0b111;
            int spriteByte0X = x / 8;
            int spriteByte1X = (spriteByte0X + 1) % System.ScreenWidthBytes;

            // Render 16x16 sprite
            if (size == 0 && _extendedMode)
            {
                int spriteByte2X = (spriteByte1X + 1) % System.ScreenWidthBytes;
                flag = BlitByte(spriteBitX, spriteByte0X, spriteByte1X, y, _memory[addr]) ||
                       BlitByte(spriteBitX, spriteByte1X, spriteByte2X, y, _memory[addr + 1]) ||
                       BlitByte(spriteBitX, spriteByte0X, spriteByte1X, y + 1, _memory[addr + 2]) ||
                       BlitByte(spriteBitX, spriteByte1X, spriteByte2X, y + 1, _memory[addr + 3]);

            }
            else
            {
                for (byte row = 0; row < size; row++)
                    flag = flag || BlitByte(spriteBitX, spriteByte0X, spriteByte1X, y + row, _memory[addr + row]);
            }


            // There's overhead to even creating a format string here so avoid if possible
            if (Logger.IsLogging(Logger.Level.Verbose))
                Logger.LogVerbose("Draw {0:x}->{1:x} at ({2}, {3})", addr, addr + size, x, y);
            
            if (!Logger.Shown)
                Terminal.DrawBuffer(_frameBuffer);


            return flag ? 1 : 0;
        }

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int key);

        // Checks if the key corresponding to the chip8 keycode 'key' is pressed
        public bool IsKeyPressed(byte key)
        {
            UpdateSystemState();

            // There's overhead to even creating a format string here so avoid if possible
            if (Logger.IsLogging(Logger.Level.Verbose))
                Logger.LogVerbose("Check key state: {0:x} {1:x}", key, GetAsyncKeyState(KeyMap[key]));

            return (GetAsyncKeyState(KeyMap[key]) & 0x8000) != 0;
        }

        // Waits until a key is pressed and returns its chip8 keycode
        public byte WaitKey()
        {
            UpdateSystemState();

            Logger.LogVerbose("Wait for key");

            char keyChar;
            byte keyCode;

            do
            {
                keyChar = char.ToUpper(Console.ReadKey(true).KeyChar);

                if (SystemKeyCallbacks.TryGetValue(keyChar, out Action<SystemCallbacks> cb))
                {
                    cb(this);
                }
            } while (!KeyMapReverse.TryGetValue(keyChar, out keyCode));

            return keyCode;
        }

        public void SetDelayTimer(byte newTicks)
        {
            _delayTimerThresh = newTicks;
            
            if (Logger.IsLogging(Logger.Level.Verbose))
                Logger.LogVerbose("Set delay timer: {0:x}", newTicks);

            _delayTimer.Restart();
        }

        public byte GetDelayTimer()
        {
            UpdateSystemState();

            _delayTimer.Stop();

            int ticks = _delayTimerThresh - (int)(_delayTimer.Elapsed.TotalMilliseconds / System.DelayTimerIntervalMs);
            if (ticks > 0) // Resume the timer if it isn't finished yet
                _delayTimer.Start();

            return (ticks > 0) ? (byte)ticks : (byte)0;
        }

        // Prints a single byte for debugging translated code (much easier than doing string stuff for the Console API)
        public void PrintDebugByte(byte b)
        {
            Logger.LogDebug("Debug Byte: {0:x}", b);
        }
    }

    class System
    {
        public const double DelayTimerIntervalMs = 1000.0 / 60;
        public const ushort MemorySize = 0x1000;
        public const ushort RomBase = 0x200;

        public const int ScreenWidth = 64;
        public const int ScreenWidthBytes = 8;
        public const int ScreenHeight = 32;

        public const byte StackSize = 16;

        private readonly byte[] _memory;
        private readonly Translator _translator;
        private readonly SystemCallbacks _systemCallbacks;

        public System(byte[] font, byte[] rom)
        {
            _memory = new byte[MemorySize];
            _translator = new(_memory);
            _systemCallbacks = new(_memory, _translator);

            Terminal.InitialiseBuffering(ScreenWidth, ScreenHeight);

            Logger.Initialise(Settings.GetDataPathFor("out.log"), Logger.Level.Verbose);

            font.CopyTo(_memory, 0);
            rom.CopyTo(_memory, RomBase);

        }

        public void Run()
        {
            //  _translator.AddPretranslatedSubroutine(Pretranslated.SUB_200, RomBase);

            Logger.HideLog();

            var a = _translator.TranslateProgram(RomBase);
            Register.Context ctx = new();
            ushort[] stack = new ushort[16];
            _translator.DumpAssembly();

            a.Invoke(ref ctx, _systemCallbacks, _memory, stack);


            Console.WriteLine("PROGRAM FINISHED EXECUTION");
        }
    }
}
