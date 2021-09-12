namespace Chip8_CIL.Chip8
{
    interface IChip8SystemCallbacks
    {
        byte DelayTimer { get; set; }

        void InitialiseFramebuffer(int width, int height);

        void BlitFramebuffer(byte[,] framebuffer);

        byte GenerateRandomByte();

        bool IsKeyPressed(KeyCode key);

        KeyCode WaitKey();

        void UpdateState();
    }
}
