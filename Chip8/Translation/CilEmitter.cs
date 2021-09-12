using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using CIL = System.Reflection.Emit;

namespace Chip8_CIL.Chip8.Translation
{
    static class CilEmitter
    {
        private class Context
        {
            private readonly BlockList _blocks;

            private readonly CIL.TypeBuilder _typeBuilder;
            private readonly CIL.MethodBuilder _method;
            private readonly CIL.ILGenerator _generator;
            private readonly CIL.Label _returnDispatcher;
            private readonly CIL.Label _errorHandler;

            private readonly int[] _registerLocalIds;

            // Branch targets and their corresponding CIL labels 
            private readonly Dictionary<Block, CIL.Label> _labelMap = new();

            public Context(string name, BlockList blocks, CIL.ModuleBuilder moduleBuilder)
            {
                _blocks = blocks;

                _typeBuilder = moduleBuilder.DefineType(name, TypeAttributes.Public);
                _method = _typeBuilder.DefineMethod(name, MethodAttributes.Public | MethodAttributes.Static,
                    typeof(void), new Type[] { typeof(Register.Context).MakeByRefType(), typeof(InstructionHelpers),
                                               typeof(byte[]), typeof(ushort[]) });
                _generator = _method.GetILGenerator();
                _returnDispatcher = _generator.DefineLabel();
                _errorHandler = _generator.DefineLabel();

                _registerLocalIds = new int[(int)Register.Id.RegisterCount]; // Index corresponds to Register.Id number

                // Create CIL locals for each register and pull in reg from context
                for (Register.Id i = 0; i < Register.Id.RegisterCount; i++)
                {
                    _registerLocalIds[(int)i] = _generator.DeclareLocal(Register.Context.GetStorageType(i)).LocalIndex;

                    EmitReadRegisterFromCtx(i);
                }
            }

            // Returns the CIL local ID for the given register ID
            private int GetRegisterLocalId(Register.Id id)
            {
                Debug.Assert(id < Register.Id.RegisterCount);

                return _registerLocalIds[(int)id];
            }

            // Loads a single register onto the stack
            // Stack state:
            // X < Top
            private void EmitLoadRegister(Register.Id id) =>
                _generator.Emit(CIL.OpCodes.Ldloc, GetRegisterLocalId(id));

            // Loads a single register onto the stack
            // Stack state:
            // X < Top
            private void EmitStoreRegister(Register.Id id) =>
                _generator.Emit(CIL.OpCodes.Stloc, GetRegisterLocalId(id));

            // Reads from the context struct into the given register local
            private void EmitReadRegisterFromCtx(Register.Id id)
            {
                _generator.Emit(CIL.OpCodes.Ldarg_0); // Load context argument
                _generator.Emit(CIL.OpCodes.Ldfld, typeof(Register.Context).GetFields()[(int)id]); // Load value of the register's field onto stack
                EmitStoreRegister(id); // Store into register
            }

            // Writes the value of the register local into the context struct
            private void EmitWriteRegisterToCtx(Register.Id id)
            {
                _generator.Emit(CIL.OpCodes.Ldarg_0); // Load context argument
                EmitLoadRegister(id); // Load register onto stack
                _generator.Emit(CIL.OpCodes.Stfld, typeof(Register.Context).GetFields()[(int)id]); // Store value into the register's struct field 
            }

            // Stack state:
            // X < Top
            private void EmitLoadInstructionHelpers() =>
                _generator.Emit(CIL.OpCodes.Ldarg_1); // Load helpers argument

            // Stack state:
            // X < Top
            private void EmitLoadMemoryArray() =>
                _generator.Emit(CIL.OpCodes.Ldarg_2); // Load memory argument

            // Stack state:
            // X < Top
            private void EmitLoadReturnAddressStack() =>
                _generator.Emit(CIL.OpCodes.Ldarg_3); // Load memory argument

            // Sets a register to a constant value
            private void EmitSetRegisterValue(Register.Id id, ushort constant)
            {
                _generator.Emit(CIL.OpCodes.Ldc_I4, constant); // Load constant
                EmitStoreRegister(id); // Store into register
            }

            // Loads a pair of registers onto the stack
            // Stack state:
            // X
            // Y < Top
            private void EmitLoadRegisterPair(Register.Id x, Register.Id y)
            {
                EmitLoadRegister(x);
                EmitLoadRegister(y);
            }

            // Loads a pair of registers onto the stack, runs an op on both of them, then stores the result in the first them
            private void EmitRegisterPairOp(Register.Id x, Register.Id y, CIL.OpCode op) // VX = VX <OP> VY
            {
                EmitLoadRegisterPair(x, y);

                _generator.Emit(op);

                EmitStoreRegister(x);
            }

            void InstructionVisitor(Instruction.Instruction instr)
            {
                switch (instr.Primary)
                {
                    case OpCode.Primary.Secondary0:
                        {
                            OpCode.Secondary0 secondary = (OpCode.Secondary0)(instr.Raw & (ushort)OpCode.Secondary0.Mask);

                            switch (secondary)
                            {
                                case OpCode.Secondary0.Clr: // clear()
                                    EmitLoadInstructionHelpers();
                                    _generator.Emit(CIL.OpCodes.Call, typeof(InstructionHelpers).GetMethod("Clear"));
                                    break;
                                default:
                                    break;
                            }
                        }
                        break;
                    case OpCode.Primary.Call: // (*NNN)(...)
                        Debug.Assert(false);
                        break;
                    case OpCode.Primary.Load: // VX = KK
                        EmitSetRegisterValue(instr.Param.X, instr.Param.KK);
                        break;
                    case OpCode.Primary.Add: // VX += KK
                        EmitLoadRegister(instr.Param.X);
                        _generator.Emit(CIL.OpCodes.Ldc_I4, (int)instr.Param.KK);

                        // Add then truncate result to a uint8
                        _generator.Emit(CIL.OpCodes.Add);
                        _generator.Emit(CIL.OpCodes.Conv_U1);

                        // Store result to register
                        EmitStoreRegister(instr.Param.X);
                        break;
                    case OpCode.Primary.Secondary8:
                        {
                            OpCode.Secondary8 secondary = (OpCode.Secondary8)(instr.Raw & (ushort)OpCode.Secondary8.Mask);

                            switch (secondary)
                            {
                                case OpCode.Secondary8.Move: // VX = VY
                                    EmitLoadRegister(instr.Param.Y);
                                    EmitStoreRegister(instr.Param.X);
                                    break;
                                case OpCode.Secondary8.Or: // VX = VX | VY
                                    EmitRegisterPairOp(instr.Param.X, instr.Param.Y, CIL.OpCodes.Or);
                                    break;
                                case OpCode.Secondary8.And: // VX = VX & VY
                                    EmitRegisterPairOp(instr.Param.X, instr.Param.Y, CIL.OpCodes.And);
                                    break;
                                case OpCode.Secondary8.Xor: // VX = VX ^ VY
                                    EmitRegisterPairOp(instr.Param.X, instr.Param.Y, CIL.OpCodes.Xor);
                                    break;
                                case OpCode.Secondary8.Add: // VF = (VX + VY) > 0xff; VX = VX + VY
                                    EmitLoadRegisterPair(instr.Param.X, instr.Param.Y);

                                    _generator.Emit(CIL.OpCodes.Add);

                                    if (instr.Param.X != Register.Id.VFRegister)
                                    {
                                        _generator.Emit(CIL.OpCodes.Dup); // Duplicate result so we can check for overflow

                                        // Truncate result to byte then store in VX
                                        _generator.Emit(CIL.OpCodes.Conv_U1);
                                        EmitStoreRegister(instr.Param.X);
                                    }

                                    // Set flags
                                    _generator.Emit(CIL.OpCodes.Ldc_I4, 0xff); // Load 0xff onto stack
                                    _generator.Emit(CIL.OpCodes.Cgt);
                                    EmitStoreRegister(Register.Id.VFRegister); // VF = result > 0xff
                                    break;
                                case OpCode.Secondary8.Sub: // VF = (VX - VY) <= 0; VX = VX - VY
                                    EmitLoadRegisterPair(instr.Param.X, instr.Param.Y);

                                    _generator.Emit(CIL.OpCodes.Sub);

                                    if (instr.Param.X != Register.Id.VFRegister)
                                    {
                                        _generator.Emit(CIL.OpCodes.Dup); // Duplicate result so we can check for overflow

                                        // Truncate result to byte then store in VX
                                        _generator.Emit(CIL.OpCodes.Conv_U1);
                                        EmitStoreRegister(instr.Param.X);
                                    }

                                    // Check for overflow
                                    _generator.Emit(CIL.OpCodes.Ldc_I4_M1);
                                    _generator.Emit(CIL.OpCodes.Cgt);

                                    EmitStoreRegister(Register.Id.VFRegister);
                                    break;
                                case OpCode.Secondary8.Shr: // VF = VY & 1; VX = VY >> 1
                                    EmitLoadRegister(instr.Param.Y);

                                    if (instr.Param.X != Register.Id.VFRegister)
                                    {
                                        _generator.Emit(CIL.OpCodes.Dup); // Duplicate result so we can set flags after


                                        // Y is on stack, shr then store
                                        _generator.Emit(CIL.OpCodes.Ldc_I4_1); // Load 1 (shr by one) onto stack
                                        _generator.Emit(CIL.OpCodes.Shr);
                                        EmitStoreRegister(instr.Param.X);
                                    }

                                    // Set flags
                                    _generator.Emit(CIL.OpCodes.Ldc_I4_1); // Load 1 onto stack
                                    _generator.Emit(CIL.OpCodes.And);
                                    EmitStoreRegister(Register.Id.VFRegister); // VF = VY & 1
                                    break;
                                case OpCode.Secondary8.SubN: // VF = (VY - VX) <= 0; VX = VY - VX
                                    EmitLoadRegisterPair(instr.Param.Y, instr.Param.X);

                                    _generator.Emit(CIL.OpCodes.Sub);

                                    if (instr.Param.X != Register.Id.VFRegister)
                                    {
                                        _generator.Emit(CIL.OpCodes.Dup); // Duplicate result so we can check for overflow after

                                        // Truncate result to byte then store in VX
                                        _generator.Emit(CIL.OpCodes.Conv_U1);
                                        EmitStoreRegister(instr.Param.X);
                                    }

                                    // Check for overflow
                                    _generator.Emit(CIL.OpCodes.Ldc_I4_M1);
                                    _generator.Emit(CIL.OpCodes.Cgt);

                                    EmitStoreRegister(Register.Id.VFRegister);
                                    break;
                                case OpCode.Secondary8.Shl: // VF = VY >> 7; VX = VY >> 1
                                    EmitLoadRegister(instr.Param.Y);

                                    if (instr.Param.X != Register.Id.VFRegister)
                                    {
                                        _generator.Emit(CIL.OpCodes.Dup); // Duplicate result so we can set flags after

                                        // Y is on stack, shl then store
                                        _generator.Emit(CIL.OpCodes.Ldc_I4_1); // Load 1 (shl by one) onto stack
                                        _generator.Emit(CIL.OpCodes.Shl);
                                        EmitStoreRegister(instr.Param.X);
                                    }

                                    // Set flags
                                    _generator.Emit(CIL.OpCodes.Ldc_I4, 7); // Load 7 (shr by 7) onto stack
                                    _generator.Emit(CIL.OpCodes.Shr);
                                    EmitStoreRegister(Register.Id.VFRegister); // VF = VY >> 7
                                    break;
                                default:
                                    break;
                            }
                        }
                        break;
                    case OpCode.Primary.LoadI: // I = NNN
                        EmitSetRegisterValue(Register.Id.IRegister, instr.Param.NNN);
                        break;
                    case OpCode.Primary.Rand: // VX = rand() & KK
                        EmitLoadInstructionHelpers();

                        // Returns random value on stack
                        _generator.Emit(CIL.OpCodes.Call, typeof(InstructionHelpers).GetMethod("Random"));

                        _generator.Emit(CIL.OpCodes.Ldc_I4, (int)instr.Param.KK); // Load mask onto stack

                        _generator.Emit(CIL.OpCodes.And); // Apply mask
                        EmitStoreRegister(instr.Param.X);
                        break;
                    case OpCode.Primary.Draw: // VF = draw(VX, VY, I, N)
                        EmitLoadInstructionHelpers();
                        EmitLoadRegisterPair(instr.Param.X, instr.Param.Y);
                        EmitLoadRegister(Register.Id.IRegister);
                        _generator.Emit(CIL.OpCodes.Ldc_I4, (int)instr.Param.N);


                        // Returns VF state
                        _generator.Emit(CIL.OpCodes.Call, typeof(InstructionHelpers).GetMethod("Draw"));

                        EmitStoreRegister(Register.Id.VFRegister);
                        break;
                    case OpCode.Primary.SecondaryF:
                        {
                            OpCode.SecondaryF secondary = (OpCode.SecondaryF)(instr.Raw & (ushort)OpCode.SecondaryF.Mask);

                            switch (secondary)
                            {
                                case OpCode.SecondaryF.MoveD: // VX = DT
                                    EmitLoadInstructionHelpers();
                                    _generator.Emit(CIL.OpCodes.Call, typeof(InstructionHelpers).GetMethod("GetDelayTimer"));
                                    EmitStoreRegister(instr.Param.X);
                                    break;
                                case OpCode.SecondaryF.KeyD: // VX = waitKey()
                                    EmitLoadInstructionHelpers();
                                    _generator.Emit(CIL.OpCodes.Call, typeof(InstructionHelpers).GetMethod("WaitKey"));
                                    EmitStoreRegister(instr.Param.X);
                                    break;
                                case OpCode.SecondaryF.LoadD: // DT = VX
                                    EmitLoadInstructionHelpers();
                                    EmitLoadRegister(instr.Param.X);
                                    _generator.Emit(CIL.OpCodes.Call, typeof(InstructionHelpers).GetMethod("SetDelayTimer"));
                                    break;
                                case OpCode.SecondaryF.LoadS:
                                    Debug.Assert(false);
                                    break;
                                case OpCode.SecondaryF.AddI: // I = I + VX
                                    EmitLoadRegisterPair(Register.Id.IRegister, instr.Param.X);
                                    _generator.Emit(CIL.OpCodes.Add);

                                    // Truncate to ushort
                                    _generator.Emit(CIL.OpCodes.Conv_U2);
                                    EmitStoreRegister(Register.Id.IRegister);
                                    break;
                                case OpCode.SecondaryF.Ldspr: // I = FONT[X]
                                    EmitLoadRegister(instr.Param.X);
                                    _generator.Emit(CIL.OpCodes.Ldc_I4, 5);
                                    _generator.Emit(CIL.OpCodes.Mul);
                                    EmitStoreRegister(Register.Id.IRegister);
                                    break;
                                case OpCode.SecondaryF.Bcd: // MEM[I] = VX / 100
                                                            // MEM[I + 1] = (VX / 10) % 10
                                                            // MEM[I + 2] = (VX % 10)
                                    EmitLoadMemoryArray();

                                    // Duplicate twice as we need to write thrice
                                    _generator.Emit(CIL.OpCodes.Dup);
                                    _generator.Emit(CIL.OpCodes.Dup);

                                    // Load target address
                                    EmitLoadRegister(Register.Id.IRegister);

                                    // Calculate BCD byte 0
                                    EmitLoadRegister(instr.Param.X);
                                    _generator.Emit(CIL.OpCodes.Ldc_I4, 100);
                                    _generator.Emit(CIL.OpCodes.Div_Un); // MEM[I] = VX / 100


                                    _generator.Emit(CIL.OpCodes.Stelem_I1); // Write byte into array

                                    // Add 1 to I for MEM[I + 1]
                                    EmitLoadRegister(Register.Id.IRegister);
                                    _generator.Emit(CIL.OpCodes.Ldc_I4, 1);
                                    _generator.Emit(CIL.OpCodes.Add);
                                    _generator.Emit(CIL.OpCodes.Conv_U2);

                                    // Calculate BCD byte 1
                                    EmitLoadRegister(instr.Param.X);
                                    _generator.Emit(CIL.OpCodes.Ldc_I4, 10);
                                    _generator.Emit(CIL.OpCodes.Div_Un);
                                    _generator.Emit(CIL.OpCodes.Ldc_I4, 10);
                                    _generator.Emit(CIL.OpCodes.Rem_Un); //  MEM[I + 1] = (VX / 10) % 10

                                    _generator.Emit(CIL.OpCodes.Stelem_I1); // Write byte into array

                                    // Add 2 to I for MEM[I + 2]
                                    EmitLoadRegister(Register.Id.IRegister);
                                    _generator.Emit(CIL.OpCodes.Ldc_I4, 2);
                                    _generator.Emit(CIL.OpCodes.Add);
                                    _generator.Emit(CIL.OpCodes.Conv_U2);

                                    // Calculate BCD byte 2
                                    EmitLoadRegister(instr.Param.X);
                                    _generator.Emit(CIL.OpCodes.Ldc_I4, 10);
                                    _generator.Emit(CIL.OpCodes.Rem_Un); //  MEM[I + 2] = (VX % 10)

                                    _generator.Emit(CIL.OpCodes.Stelem_I1); // Write byte into array
                                    break;
                                case OpCode.SecondaryF.Stor: // MEM[I + i] = V[i] FOR i IN 0...X
                                                             // I = I + X + 1
                                    for (Register.Id i = 0; i <= instr.Param.X; i++)
                                    {
                                        EmitLoadMemoryArray();
                                        EmitLoadRegister(Register.Id.IRegister);

                                        // If i > 0 then add it to the I Register to get the memory offset
                                        if (i != 0)
                                        {
                                            _generator.Emit(CIL.OpCodes.Ldc_I4, (int)i);
                                            _generator.Emit(CIL.OpCodes.Add); // I + i
                                                                              // Truncate to ushort
                                            _generator.Emit(CIL.OpCodes.Conv_U2);
                                        }

                                        EmitLoadRegister(i);
                                        _generator.Emit(CIL.OpCodes.Stelem_I1); // Write byte into array
                                    }

                                    EmitLoadRegister(Register.Id.IRegister);

                                    _generator.Emit(CIL.OpCodes.Ldc_I4, (int)instr.Param.X);
                                    _generator.Emit(CIL.OpCodes.Add); // I + X
                                    _generator.Emit(CIL.OpCodes.Ldc_I4, 1);
                                    _generator.Emit(CIL.OpCodes.Add); // I + X + 1

                                    // Truncate to ushort
                                    _generator.Emit(CIL.OpCodes.Conv_U2);

                                    EmitStoreRegister(Register.Id.IRegister);
                                    break;
                                case OpCode.SecondaryF.Read: // V[i] = MEM[I + i] FOR i IN 0...X
                                                             // I = I + X + 1
                                    for (Register.Id i = 0; i <= instr.Param.X; i++)
                                    {
                                        EmitLoadMemoryArray();
                                        EmitLoadRegister(Register.Id.IRegister);

                                        // If i > 0 then add it to the I Register to get the memory offset
                                        if (i != 0)
                                        {
                                            _generator.Emit(CIL.OpCodes.Ldc_I4, (int)i);
                                            _generator.Emit(CIL.OpCodes.Add); // I + i
                                                                              // Truncate to ushort
                                            _generator.Emit(CIL.OpCodes.Conv_U2);
                                        }

                                        _generator.Emit(CIL.OpCodes.Ldelem_U1); // Read byte onto stack
                                        EmitStoreRegister(i);
                                    }

                                    EmitLoadRegister(Register.Id.IRegister);

                                    _generator.Emit(CIL.OpCodes.Ldc_I4, (int)instr.Param.X);
                                    _generator.Emit(CIL.OpCodes.Add); // I + X
                                    _generator.Emit(CIL.OpCodes.Ldc_I4, 1);
                                    _generator.Emit(CIL.OpCodes.Add); // I + X + 1

                                    // Truncate to ushort
                                    _generator.Emit(CIL.OpCodes.Conv_U2);

                                    EmitStoreRegister(Register.Id.IRegister);
                                    break;
                                default:
                                    break;
                            }
                        }
                        break;
                    default:
                        break;
                }
            }

            private void EmitBlockTerminator(Block block)
            {
                // Parse the instruction and emit code
                Instruction.Instruction instr = block.TerminatingInstr;

                if (block.Successor == null)
                {
                    switch (instr.Primary)
                    {
                        case OpCode.Primary.Jump: // Jumps with no successors are infloops, return if they happen
                            _generator.Emit(CIL.OpCodes.Ret);
                            return;
                        case OpCode.Primary.Secondary0:
                            if ((OpCode.Secondary0)(instr.Raw & (ushort)OpCode.Secondary0.Mask) == OpCode.Secondary0.Rts)
                                _generator.Emit(CIL.OpCodes.Br, _returnDispatcher); // Jump to return dispatcher

                            return;
                        default:
                            return;
                    }
                }

                // Create label for successor
                if (!_labelMap.TryGetValue(block.Successor, out CIL.Label successorLabel))
                {
                    successorLabel = _generator.DefineLabel();
                    _labelMap[block.Successor] = successorLabel;
                }

                // Only check for skipping instrs if this block has a conditional successor
                if (block.ConditionalSuccessor != null)
                {
                    // Create label for conditional successor
                    if (!_labelMap.TryGetValue(block.ConditionalSuccessor, out CIL.Label conditionalSuccessorLabel))
                    {
                        conditionalSuccessorLabel = _generator.DefineLabel();
                        _labelMap[block.ConditionalSuccessor] = conditionalSuccessorLabel;
                    }

                    switch (instr.Primary)
                    {
                        case OpCode.Primary.Ske: // SKIP if VX == KK
                                                 // Load register and constant
                            EmitLoadRegister(instr.Param.X);
                            _generator.Emit(CIL.OpCodes.Ldc_I4, (uint)instr.Param.KK);

                            // Compare and skip if equal
                            _generator.Emit(CIL.OpCodes.Beq, successorLabel);
                            break;
                        case OpCode.Primary.Skne: // SKIP if VX != KK
                            // Load register and constant
                            EmitLoadRegister(instr.Param.X);
                            _generator.Emit(CIL.OpCodes.Ldc_I4, (uint)instr.Param.KK);

                            // Compare and skip if not equal
                            _generator.Emit(CIL.OpCodes.Bne_Un, successorLabel);
                            break;
                        case OpCode.Primary.Skre: // SKIP if VX == VY
                            EmitLoadRegisterPair(instr.Param.X, instr.Param.Y);

                            // Compare and skip if equal
                            _generator.Emit(CIL.OpCodes.Beq, successorLabel);

                            break;
                        case OpCode.Primary.SecondaryE:
                            {
                                OpCode.SecondaryE secondary = (OpCode.SecondaryE)(instr.Raw & (ushort)OpCode.SecondaryE.Mask);

                                switch (secondary)
                                {
                                    case OpCode.SecondaryE.Skpr: // SKIP if PRESSED[X]
                                        EmitLoadInstructionHelpers();
                                        EmitLoadRegister(instr.Param.X);
                                        _generator.Emit(CIL.OpCodes.Call, typeof(InstructionHelpers).GetMethod("IsKeyPressed"));

                                        _generator.Emit(CIL.OpCodes.Brtrue, successorLabel);
                                        break;
                                    case OpCode.SecondaryE.Skup: // SKIP if !PRESSED[X]
                                        EmitLoadInstructionHelpers();
                                        EmitLoadRegister(instr.Param.X);
                                        _generator.Emit(CIL.OpCodes.Call, typeof(InstructionHelpers).GetMethod("IsKeyPressed"));

                                        _generator.Emit(CIL.OpCodes.Brfalse, successorLabel);
                                        break;
                                    default:
                                        break;
                                }
                            }
                            break;
                        case OpCode.Primary.Skrne: // SKIP if VX != VY
                            EmitLoadRegisterPair(instr.Param.X, instr.Param.Y);

                            // Compare and skip if not equal
                            _generator.Emit(CIL.OpCodes.Bne_Un, successorLabel);
                            break;
                        default:
                            break;
                    }

                    // All skipping instructions will fall through to the conditional successor if their skip condition wasn't met
                    _generator.Emit(CIL.OpCodes.Br, conditionalSuccessorLabel);
                }
                else
                {
                    switch (instr.Primary)
                    {
                        case OpCode.Primary.Jump: // JMP NNN
                            _generator.Emit(CIL.OpCodes.Br, successorLabel);
                            break;
                        case OpCode.Primary.Call: // STACK[SP++] = returnAddress
                                                  // JMP NNN
                                                  // Push return address onto stack to be popped by the return dispatcher

                            EmitLoadReturnAddressStack();

                            // Increment SP but use pre increment value for stelem
                            EmitLoadRegister(Register.Id.SPRegister);
                            _generator.Emit(CIL.OpCodes.Dup);
                            _generator.Emit(CIL.OpCodes.Ldc_I4_1);
                            _generator.Emit(CIL.OpCodes.Add);

                            // Store incremented SP into local
                            EmitStoreRegister(Register.Id.SPRegister);

                            // Load return address to store
                            _generator.Emit(CIL.OpCodes.Ldc_I4, block.EndAddr);

                            // Store
                            _generator.Emit(CIL.OpCodes.Stelem_I2);

                            _generator.Emit(CIL.OpCodes.Br, successorLabel);
                            break;
                        default:
                            break;
                    }
                }
            }

            private void BlockVisitor(Block block)
            {
                // Create label for the block if needed
                if (!_labelMap.TryGetValue(block, out CIL.Label blockLabel))
                {
                    blockLabel = _generator.DefineLabel();
                    _labelMap[block] = blockLabel;
                }

                // Mark a label at the start of the block
                _generator.MarkLabel(blockLabel);

                // Emit  code for the block instruction contents
                block.DispatchInstructions(InstructionVisitor);

                // Handle block termination and control flow to the next block
                EmitBlockTerminator(block);
            }

            private void EmitErrorHandler()
            {
                _generator.MarkLabel(_errorHandler);
                _generator.EmitWriteLine("Fatal Error!");
                _generator.Emit(CIL.OpCodes.Break);

                // Might not work as stack could have values on it but try nonetheless
                _generator.Emit(CIL.OpCodes.Ret);
            }


            // Emits a jump table for each return address in the program
            // The return address is left on the stack after a method call
            private void EmitReturnDispatcher()
            {
                _generator.MarkLabel(_returnDispatcher);

                // Allocate the jump table
                CIL.Label[] labelArray = new CIL.Label[_blocks.GetLastReturnTargetAddr() + 1];

                Block target = _blocks.GetNextReturnTarget(0);

                // Fill it out
                for (int i = 0; i < labelArray.Length && target != null; i++)
                {
                    if (target.StartAddr == i)
                    {
                        // Create label for the block if needed
                        if (!_labelMap.TryGetValue(target, out CIL.Label blockLabel))
                        {
                            blockLabel = _generator.DefineLabel();
                            _labelMap[target] = blockLabel;
                        }

                        // Add to jump table
                        labelArray[i] = blockLabel;

                        // Move to checking the successor block
                        target = _blocks.GetNextReturnTarget((ushort)(target.StartAddr + 1));
                    }
                    else
                    {
                        // A return to an address not in the return target list should never happen
                        labelArray[i] = _errorHandler;
                    }
                }

                // Load stack array
                EmitLoadReturnAddressStack();

                // Decrement and store SP
                EmitLoadRegister(Register.Id.SPRegister);
                _generator.Emit(CIL.OpCodes.Ldc_I4_1);
                _generator.Emit(CIL.OpCodes.Sub);

                // Leave SP - 1 on stack as we will use for ldelem
                _generator.Emit(CIL.OpCodes.Dup);
                EmitStoreRegister(Register.Id.SPRegister);

                // Read return address onto stack
                _generator.Emit(CIL.OpCodes.Ldelem_U2);

                // Emit jump table, this will switch based off the return address
                _generator.Emit(CIL.OpCodes.Switch, labelArray);

                // Use error handler for default case
                _generator.Emit(CIL.OpCodes.Br, _errorHandler);

                // Rest will be marked in the blocks
            }

            public Translator.ProgramDelegate Translate()
            {

                _blocks.DispatchLinear(BlockVisitor);

                EmitErrorHandler();
                EmitReturnDispatcher();

                Type subType = _typeBuilder.CreateType();


                return (Translator.ProgramDelegate)subType.GetMethods()[0].CreateDelegate(typeof(Translator.ProgramDelegate));
            }
        }


        public static Translator.ProgramDelegate Translate(BlockList blocks, CIL.ModuleBuilder moduleBuilder)
        {
            Context emitCtx = new(string.Format("PROG_{0:X}", blocks.GetStartAddr()), blocks, moduleBuilder);

            Translator.ProgramDelegate del = emitCtx.Translate();



            return del;
        }
    }
}
