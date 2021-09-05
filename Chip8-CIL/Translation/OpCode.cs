namespace Chip8_CIL
{
    namespace OpCode
    {
        public enum Primary : ushort
        {
            Mask = 0xf000,
            Shift = 0xC,

            Secondary0 = 0x0000,
            Jump = 0x1000, // JumP to immediate address
            Call = 0x2000, // Call subroutine at immediate address and push current pc to stack
            Ske = 0x3000, // SKip next instruction if immediate Equals register
            Skne = 0x4000, // SKip next instruction if immediate Not Equals register
            Skre = 0x5000, // SKip next instruction if Register Equals register
            Load = 0x6000, // Load an immdiate value into a register
            Add = 0x7000, // Add an immediate value to a register
            Secondary8 = 0x8000,
            Skrne = 0x9000, // Skip next instruction if Register Not Equals register
            LoadI = 0xa000, // Load an immdiate value into the index register
            Jumpi = 0xb000, // Jump Indirectly to v0+immediate
            Rand = 0xc000, // generate a Random number and apply given immediate mask
            Draw = 0xd000, // Draw a sprite at the given address to the screen at the given position
            SecondaryE = 0xe000,
            SecondaryF = 0xf000,
        }

        // These have no params at all
        public enum Secondary0 : ushort
        {
            Mask = 0x00ff,
            Clr = 0xe0, // CLeaR screen
            Rts = 0xee, // ReTurn from Subroutine
        }

        // All of these take two registers as params
        public enum Secondary8 : ushort
        {
            Mask = 0x000f,
            Move = 0x0, // Move value from one reg to another
            Or = 0x1, // Or two regs
            And = 0x2, // And two regs
            Xor = 0x3, // Xor two regs
            Add = 0x4, // Add two regs
            Sub = 0x5, // Subtract two regs
            Shr = 0x6, // Shift bits in reg right by one then store in other reg
            SubN = 0x7,  // Subtract two regs
            Shl = 0xe, // Shift bits in reg left by one then store in other reg
        }

        public enum SecondaryE : ushort
        {
            Mask = 0x00ff,
            Skpr = 0x9e, // Skip next instruction if Key in Register is pressed
            Skup = 0xa1, // Skip next instruction if Key in Register is not pressed
        }

        public enum SecondaryF : ushort
        {
            Mask = 0x00ff,
            MoveD = 0x07, // Move delay timer value to register
            KeyD = 0x0a, // Wait for keypress then store in register
            LoadD = 0x15, // Load delay timer with register value
            LoadS = 0x18, // Load sound timer with register value
            AddI = 0x1e, // Add register value to index register
            Ldspr = 0x29, // Load index register with the position of the given sprite id
            Bcd = 0x33, // Store the binary coded decimal value of the given register at the memory location pointed to
                        // by the index register
            Stor = 0x55, // Store registers V0 - Vx at the memory location pointed to by the index register
            Read = 0x65, // Read registers V0 - Vx from a memory location pointed to by the index register
        }
    }
}
