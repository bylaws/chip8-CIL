using System.Collections;

using Chip8_CIL.Chip8.Translation;

namespace Chip8_CIL.Chip8
{
    class Chip8System
    {
        public const double DelayTimerIntervalMs = 1000.0 / 120;
        public const double SoundTimerIntervalMs = 1000.0 / 60;
        public const double DisplayRefreshIntervalMs = 1000.0 / 30;
        public const ushort MemorySize = 0x1000;
        public const ushort RomBase = 0x200;

        public const int ScreenWidth = 64;
        public const int ScreenWidthBytes = 8;
        public const int ScreenHeight = 32;

        public const byte StackSize = 16;

        public bool Pause = false;

        private readonly Logger _logger;
        private readonly byte[] _memory;
        private readonly Translator _translator;
        private readonly IChip8SystemCallbacks _callbacks;

        public Chip8System(byte[] font, byte[] rom, IChip8SystemCallbacks callbacks, Logger logger)
        {
            _logger = logger;
            _callbacks = callbacks;

            _memory = new byte[MemorySize];
            _translator = new(_memory, _logger);
     
            font.CopyTo(_memory, 0);
            rom.CopyTo(_memory, RomBase);

            callbacks.InitialiseFramebuffer(ScreenWidth, ScreenHeight);
        }

        public void Run()
        {

            InstructionHelpers instrHelpers = new(_memory, _callbacks, _logger);

            Register.Context ctx = new();
            ushort[] stack = new ushort[StackSize];

            _translator.ExecuteProgram(RomBase, ref ctx, instrHelpers, stack);
        }
    }
}
