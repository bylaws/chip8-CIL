namespace Chip8_CIL.Chip8
{
    namespace Instruction
    {
        // Holds the parameters extracted from an instruction
        public struct Parameters
        {
            public readonly ushort NNN; // Lower 12 bits of instruction
            public readonly byte N; // Lower 4 bits of low byte
            public readonly Register.Id X; // Lower 4 bits of high byte
            public readonly Register.Id Y; // Upper 4 bits of low byte
            public readonly byte KK; // Lower 8 bits of instruction

            public Parameters(ushort instr)
            {
                NNN = (ushort)(instr & 0xfff);
                N = (byte)(instr & 0xf);
                X = (Register.Id)((instr >> 8) & 0xf);
                Y = (Register.Id)((instr >> 4) & 0xf);
                KK = (byte)(instr & 0xff);
            }
        }

        // Holds an intermediate representation of a chip8 instruction
        public class Instruction
        {
            public ushort Raw;
            public OpCode.Primary Primary;
            public Parameters Param;
            public readonly bool Generated;
            public ushort Size {
                get => GetSize();
            }

            // Decode BE instruction from memory
            public Instruction(byte[] memory, ushort instrAddr) :
                this((ushort)(memory[instrAddr] << 8 | memory[instrAddr + 1])) { }

            public Instruction(ushort raw)
            {
                Raw = raw;

                // Decode the primary OpCode
                Primary = (OpCode.Primary)(raw & (ushort)OpCode.Primary.Mask);

                // Unpack the parameters from the instruction into a struct for easy access
                Param = new(raw);

            }

            public Instruction(OpCode.Primary primary)
            {
                Raw = (ushort)((ushort)primary << (ushort)OpCode.Primary.Shift);

                Generated = true;

                Primary = primary;

                Param = new();
            }

            // If this instruction will terminate a subroutine
            public bool IsTerminating(ushort instrAddr)
            {
                switch (Primary)
                {
                    case OpCode.Primary.Jump:
                        return instrAddr == Param.NNN; // Infloop
                    case OpCode.Primary.Jumpi:
                        return true;
                    case OpCode.Primary.Secondary0:
                        {
                            OpCode.Secondary0 secondary = (OpCode.Secondary0)(Raw & (ushort)OpCode.Secondary0.Mask);

                            switch (secondary)
                            {
                                case OpCode.Secondary0.Tertiary0E:
                                    {
                                        OpCode.Tertiary0E tertiary = (OpCode.Tertiary0E)(Raw & (ushort)OpCode.Tertiary0E.Mask);

                                        switch (tertiary)
                                        {
                                            case OpCode.Tertiary0E.Rts:
                                                return true;
                                            default:
                                                return false;
                                        }
                                    }
                                default:
                                    return false;
                            }
                        }
                    default:
                        return false;
                }
            }

            // If the instruction following this instruction can potentially skipped
            public bool IsSkipping()
            {
                switch (Primary)
                {
                    case OpCode.Primary.Ske:
                    case OpCode.Primary.Skne:
                    case OpCode.Primary.Skre:
                    case OpCode.Primary.Skrne:
                        return true;
                    case OpCode.Primary.SecondaryE:
                        {
                            OpCode.SecondaryE secondary = (OpCode.SecondaryE)(Raw & (ushort)OpCode.SecondaryE.Mask);

                            switch (secondary)
                            {
                                case OpCode.SecondaryE.Skpr:
                                case OpCode.SecondaryE.Skup:
                                    return true;
                                default:
                                    return false;
                            }
                        }
                    default:
                        return false;
                }
            }

            private ushort GetSize()
            {
                if (Generated)
                    return 0;


                return 2;
            }
        }
    }
}
