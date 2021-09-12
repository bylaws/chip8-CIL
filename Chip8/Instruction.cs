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
                    case OpCode.Primary.Secondary0:
                        {
                            OpCode.Secondary0 secondary = (OpCode.Secondary0)(Raw & (ushort)OpCode.Secondary0.Mask);

                            switch (secondary)
                            {
                                case OpCode.Secondary0.Rts:
                                    return true;
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

            public Register.UsageInfo GetRegisterUsageInfo()
            {
                Register.UsageInfo usage = new();

                switch (Primary)
                {
                    case OpCode.Primary.Ske:
                    case OpCode.Primary.Skne:
                        usage.Read.SetVRegister(Param.X);
                        break;
                    case OpCode.Primary.Skre:
                    case OpCode.Primary.Skrne:
                        usage.Read.SetVRegister(Param.X);
                        usage.Read.SetVRegister(Param.Y);
                        break;
                    case OpCode.Primary.Load:
                        usage.Clobbered.SetVRegister(Param.X);
                        break;
                    case OpCode.Primary.Add:
                        if (Param.KK != 0)
                            usage.Changed.SetVRegister(Param.X);
                        break;
                    case OpCode.Primary.Secondary8:
                        {
                            OpCode.Secondary8 secondary = (OpCode.Secondary8)(Raw & (ushort)OpCode.Secondary8.Mask);
                            switch (secondary)
                            {
                                case OpCode.Secondary8.Move:
                                    if (Param.X != Param.Y)
                                    {
                                        usage.Read.SetVRegister(Param.Y);
                                        usage.Clobbered.SetVRegister(Param.X);
                                    }
                                    break;
                                case OpCode.Secondary8.Or:
                                case OpCode.Secondary8.And:
                                    if (Param.X != Param.Y)
                                    {
                                        usage.Read.SetVRegister(Param.Y);
                                        usage.Changed.SetVRegister(Param.X);
                                    }
                                    break;
                                case OpCode.Secondary8.Xor:
                                    if (Param.X == Param.Y)
                                    {
                                        usage.Clobbered.SetVRegister(Param.X);
                                    }
                                    else
                                    {
                                        usage.Read.SetVRegister(Param.Y);
                                        usage.Changed.SetVRegister(Param.X);
                                    }

                                    break;
                                case OpCode.Secondary8.Add:
                                    if (Param.Y == Param.X)
                                    {
                                        usage.Changed.SetVRegister(Param.X);

                                        if (Param.X != Register.Id.VFRegister)
                                            usage.Clobbered.SetVRegister(Register.Id.VFRegister);
                                    }
                                    else if (Param.Y == Register.Id.VFRegister)
                                    {
                                        usage.Changed.SetVRegister(Param.X);
                                        usage.Changed.SetVRegister(Register.Id.VFRegister);
                                    }
                                    else if (Param.X == Register.Id.VFRegister)
                                    {
                                        usage.Read.SetVRegister(Param.Y);
                                        usage.Changed.SetVRegister(Register.Id.VFRegister);
                                    }
                                    else
                                    {
                                        usage.Read.SetVRegister(Param.Y);
                                        usage.Changed.SetVRegister(Param.X);
                                        usage.Clobbered.SetVRegister(Register.Id.VFRegister);
                                    }
                                    break;
                                case OpCode.Secondary8.Sub:
                                case OpCode.Secondary8.SubN:
                                    if (Param.X == Param.Y)
                                    {
                                        usage.Clobbered.SetVRegister(Param.X);
                                        usage.Clobbered.SetVRegister(Register.Id.VFRegister);
                                    }
                                    else if (Param.X == Register.Id.VFRegister)
                                    {
                                        usage.Read.SetVRegister(Param.Y);
                                        usage.Changed.SetVRegister(Register.Id.VFRegister);
                                    }
                                    else if (Param.Y == Register.Id.VFRegister)
                                    {
                                        usage.Changed.SetVRegister(Param.X);
                                        usage.Changed.SetVRegister(Register.Id.VFRegister);
                                    }
                                    else
                                    {
                                        usage.Read.SetVRegister(Param.Y);
                                        usage.Changed.SetVRegister(Param.X);
                                        usage.Clobbered.SetVRegister(Register.Id.VFRegister);
                                    }
                                    break;

                                case OpCode.Secondary8.Shl:
                                case OpCode.Secondary8.Shr:
                                    if (Param.Y == Param.X)
                                    {
                                        usage.Changed.SetVRegister(Param.X);

                                        if (Param.X != Register.Id.VFRegister)
                                            usage.Clobbered.SetVRegister(Register.Id.VFRegister);
                                    }
                                    else if (Param.Y == Register.Id.VFRegister)
                                    {
                                        usage.Clobbered.SetVRegister(Param.X);
                                        usage.Changed.SetVRegister(Register.Id.VFRegister);
                                    }
                                    else
                                    {
                                        usage.Read.SetVRegister(Param.Y);
                                        usage.Clobbered.SetVRegister(Param.X);
                                        usage.Clobbered.SetVRegister(Register.Id.VFRegister);
                                    }
                                    break;
                                default:
                                    break;
                            }
                        }
                        break;
                    case OpCode.Primary.LoadI:
                        usage.Clobbered.SetIRegister();
                        break;
                    case OpCode.Primary.Jumpi:
                        usage.Read.SetIRegister();
                        break;
                    case OpCode.Primary.Rand:
                        usage.Clobbered.SetVRegister(Param.X);
                        break;
                    case OpCode.Primary.Draw:
                        if (Param.X == Register.Id.VFRegister && Param.Y == Register.Id.VFRegister)
                        {
                            usage.Changed.SetVRegister(Register.Id.VFRegister);
                        }
                        else if (Param.X == Register.Id.VFRegister)
                        {
                            usage.Changed.SetVRegister(Register.Id.VFRegister);
                            usage.Read.SetVRegister(Param.Y);
                        }
                        else if (Param.Y == Register.Id.VFRegister)
                        {
                            usage.Changed.SetVRegister(Register.Id.VFRegister);
                            usage.Read.SetVRegister(Param.X);
                        }
                        else
                        {
                            usage.Read.SetVRegister(Param.X);
                            usage.Read.SetVRegister(Param.Y);
                            usage.Clobbered.SetVRegister(Register.Id.VFRegister);
                        }

                        usage.Read.SetIRegister();
                        break;
                    case OpCode.Primary.SecondaryE:
                        {
                            OpCode.SecondaryE secondary = (OpCode.SecondaryE)(Raw & (ushort)OpCode.SecondaryE.Mask);
                            switch (secondary)
                            {
                                case OpCode.SecondaryE.Skpr:
                                case OpCode.SecondaryE.Skup:
                                    usage.Read.SetVRegister(Param.X);
                                    break;
                                default:
                                    break;
                            }
                        }
                        break;
                    case OpCode.Primary.SecondaryF:
                        {
                            OpCode.SecondaryF secondary = (OpCode.SecondaryF)(Raw & (ushort)OpCode.SecondaryF.Mask);
                            switch (secondary)
                            {
                                case OpCode.SecondaryF.MoveD:
                                case OpCode.SecondaryF.KeyD:
                                    usage.Clobbered.SetVRegister(Param.X);
                                    break;
                                case OpCode.SecondaryF.LoadS:
                                case OpCode.SecondaryF.LoadD:
                                    usage.Read.SetVRegister(Param.X);
                                    break;
                                case OpCode.SecondaryF.AddI:
                                    usage.Read.SetVRegister(Param.X);
                                    usage.Changed.SetIRegister();
                                    break;
                                case OpCode.SecondaryF.Ldspr:
                                    usage.Read.SetVRegister(Param.X);
                                    usage.Clobbered.SetIRegister();
                                    break;
                                case OpCode.SecondaryF.Bcd:
                                    usage.Read.SetVRegister(Param.X);
                                    usage.Read.SetIRegister();
                                    break;
                                case OpCode.SecondaryF.Stor:
                                    for (Register.Id i = 0; i <= Param.X; i++)
                                        usage.Read.SetVRegister(i);

                                    usage.Read.SetIRegister();
                                    break;
                                case OpCode.SecondaryF.Read:
                                    for (Register.Id i = 0; i <= Param.X; i++)
                                        usage.Clobbered.SetVRegister(i);

                                    usage.Changed.SetIRegister();
                                    break;
                                default:
                                    break;
                            }
                        }
                        break;
                    default:
                        break;
                }

                return usage;
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
