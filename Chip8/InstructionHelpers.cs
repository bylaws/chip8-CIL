using System;

namespace Chip8_CIL.Chip8
{
    // This will be accessed from the translated assembly so needs to be public
    public class InstructionHelpers
    {
        private readonly byte[] _memory;
        private readonly IChip8SystemCallbacks _callbacks;
        private readonly Logger _logger;

        private int _resScale = 1;
        private byte[,] _frameBuffer = new byte[Chip8System.ScreenWidthBytes, Chip8System.ScreenHeight];

        internal InstructionHelpers(byte[] memory, IChip8SystemCallbacks callbacks, Logger logger)
        {
            _memory = memory;
            _callbacks = callbacks;
            _logger = logger;
        }


        public byte Random() => _callbacks.GenerateRandomByte();
        
        public void Clear()
        {
            Array.Clear(_frameBuffer, 0, _frameBuffer.Length);
            _callbacks.BlitFramebuffer(_frameBuffer);
        }

        private bool BlitByte(int bitX, int byte0X, int byte1X, int y, ushort data)
        {
            int spriteRowY = y % (Chip8System.ScreenHeight * _resScale);

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
            _callbacks.UpdateState();

            x %= (byte)(Chip8System.ScreenWidth * _resScale);
            y %= (byte)(Chip8System.ScreenHeight * _resScale);

            // Stores if any bits have been flipped from 1->0 during sprite rendering
            bool flag = false;
            int spriteBitX = x & 0b111;
            int spriteByte0X = x / 8;
            int spriteByte1X = (spriteByte0X + 1) % (Chip8System.ScreenWidthBytes * _resScale);

            // Render 16x16 sprite
            if (size == 0)
            {
                int spriteByte2X = (spriteByte1X + 1) % (Chip8System.ScreenWidthBytes * _resScale);
                for (byte row = 0; row < 16; row++)
                    flag = BlitByte(spriteBitX, spriteByte0X, spriteByte1X, y + row, _memory[addr + row * 2]) ||
                           BlitByte(spriteBitX, spriteByte1X, spriteByte2X, y + row, _memory[addr + 1 + row * 2]) || flag;

            }
            else
            {
                for (byte row = 0; row < size; row++)
                    flag = BlitByte(spriteBitX, spriteByte0X, spriteByte1X, y + row, _memory[addr + row]) || flag;
            }


            _logger.LogVerbose("Draw {0:x}->{1:x} at ({2}, {3})", addr, addr + size, x, y);

            _callbacks.BlitFramebuffer(_frameBuffer);


            return flag ? 1 : 0;
        }

        public void EnterHiRes()
        {
            _resScale = 2;
            _frameBuffer = new byte[Chip8System.ScreenWidthBytes * _resScale, Chip8System.ScreenHeight * _resScale];
            _callbacks.InitialiseFramebuffer(Chip8System.ScreenWidth * _resScale, Chip8System.ScreenHeight * _resScale);
        }
        public void EnterLoRes()
        {
            _resScale = 1;
            _frameBuffer = new byte[Chip8System.ScreenWidthBytes * _resScale, Chip8System.ScreenHeight * _resScale];
            _callbacks.InitialiseFramebuffer(Chip8System.ScreenWidth * _resScale, Chip8System.ScreenHeight * _resScale);
        }
        public void ScrollDown()
        {
            int lines = 4;
            lines /= 2;
            for (int line = 0; line < lines; line++)
            {
                for (int i = (Chip8System.ScreenHeight * _resScale) - 1; i > 0; i--)
                {
                    for (int j = 0; j < (Chip8System.ScreenWidthBytes * _resScale); j++)
                    {
                        _frameBuffer[j, i] = _frameBuffer[j, i - 1];
                    }
                }

                for (int j = 0; j < (Chip8System.ScreenWidthBytes * _resScale); j++)
                {
                    _frameBuffer[j, 0] = 0;
                }
            }

            _callbacks.BlitFramebuffer(_frameBuffer);
        }

        // Checks if the key corresponding to the chip8 keycode 'key' is pressed
        public bool IsKeyPressed(byte key)
        {
            _callbacks.UpdateState();

            bool pressed = _callbacks.IsKeyPressed((KeyCode)key);
            _logger.LogVerbose("Check key: {0:x}, pressed: {1}", key, pressed);

            return pressed;
        }

        // Waits until a key is pressed and returns its chip8 keycode
        public byte WaitKey()
        {
            _callbacks.UpdateState();

            _logger.LogVerbose("Wait for key");

            return (byte)_callbacks.WaitKey();
        }

        public void SetDelayTimer(byte newTicks) => _callbacks.DelayTimer = newTicks;

        public byte GetDelayTimer() => _callbacks.DelayTimer;

        public void SetSoundTimer(byte newTicks) => _callbacks.SoundTimer = newTicks;

        // Prints a single byte for debugging translated code (much easier than doing string stuff for the Console API)
        public void PrintDebugByte(byte b)
        {
            _logger.LogDebug("Debug Byte: {0:x}", b);
        }
    }
}
