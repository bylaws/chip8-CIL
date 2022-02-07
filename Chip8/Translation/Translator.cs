using Lokad.ILPack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

namespace Chip8_CIL.Chip8.Translation
{
    class Translator
    {
        private readonly byte[] _memory;
        private readonly Logger _logger;

        private readonly ModuleBuilder _moduleBuilder;
        Dictionary<uint, Program> _compilationUnits = new();

        public struct Program
        {
            public BitArray CodeSet;
            public ProgramDelegate TranslatedProg;
            public uint Hash;


            public delegate ushort ProgramDelegate(ref Register.Context ctx, InstructionHelpers helpers, byte[] memory, ushort[] returnAddressStack, BitArray codeSet, ref ushort smcRangeStart, ref ushort smcRangeSize);

            public Program(BitArray codeSet, ProgramDelegate translatedProg, uint hash)
            {
                CodeSet = codeSet;
                TranslatedProg = translatedProg;
                Hash = hash;
            }
        }


        public Translator(byte[] memory, Logger logger)
        {
            _memory = memory;
            _logger = logger;


            AssemblyBuilder asmBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(Guid.NewGuid().ToString()),
            AssemblyBuilderAccess.Run);

            _moduleBuilder = asmBuilder.DefineDynamicModule("TranslatedDynamicAsm");
        }

        private void DiscoverBlocks(ushort startAddr, BlockList blocks)
        {
            Stack<Block> blockWalkingStack = new();

            Block currentBlock = blocks.AddJumpTableEntry(startAddr);
            if (currentBlock.Finalised)
                return;

            Block potentialNextBlock = null;


//            BlockList blocks = new(currentBlock);


            Block FindNextEmptyBlock()
            {
                Block emptyBlock;

                while (blockWalkingStack.TryPop(out emptyBlock) && !emptyBlock.Empty()) { }

                return emptyBlock;
            }

            Block GetPotentialNextBlock() => blocks.GetNextBlock((ushort)(currentBlock.EndAddr + 1));

            _logger.StartFunction("DiscoverBlocks", Logger.Level.Debug);


            while (currentBlock != null)
            {

                if (potentialNextBlock != null)
                {
                    // We've walked up to another existing block, link our current block to it and switch to it
                    if (potentialNextBlock.StartAddr == currentBlock.EndAddr)
                    {
                        // Immediate jump to potentialNextBlock
                        currentBlock.Finalise(new(OpCode.Primary.Jump));
                        currentBlock.AddBranch(false, potentialNextBlock);

                        // Next block is empty so still needs to be walked
                        if (potentialNextBlock.Empty())
                            currentBlock = potentialNextBlock;
                        else
                            currentBlock = FindNextEmptyBlock();

                        if (currentBlock != null)
                            potentialNextBlock = GetPotentialNextBlock();
                        else
                            break;
                    }
                }

                ushort instrAddr = currentBlock.EndAddr;
                Instruction.Instruction instr = new(_memory, instrAddr);

                _logger.LogTrace("Addr: {0:x} Raw: {1:x}, Primary: {2:}, NNN: {3:x}, N: {4:x}, X: {5:x}, Y: {6:x}, KK: {7:x}",
                    instrAddr, instr.Raw, instr.Primary.ToString(), instr.Param.NNN, instr.Param.N, instr.Param.X, instr.Param.Y, instr.Param.KK);

                blocks.MarkAsCode(instrAddr, instr.Size);

                if (instr.Primary == OpCode.Primary.Call)
                {
                    ushort targetAddr = instr.Param.NNN;

                    _logger.LogVerbose("Call to {0:x} at: {1:x}", targetAddr, instrAddr);

                    // This treats the call as a block terminator instruction
                    ushort returnTargetAddr = currentBlock.Finalise(instr);

                    // Add the return table of call to the block list and mark it for walking
                    blockWalkingStack.Push(blocks.AddJumpTableEntry(returnTargetAddr));

                    // If this call target created a new block then walk it, otherwise find a new block to walk
                    if (blocks.AddBlockSuccessor(currentBlock, false, targetAddr, out Block targetBlock))
                        currentBlock = targetBlock;
                    else
                        currentBlock = FindNextEmptyBlock();

                    if (currentBlock != null)
                        potentialNextBlock = GetPotentialNextBlock();
                }
                else if (instr.IsTerminating(instrAddr))
                {
                    _logger.LogVerbose("Leaf at: {0:x}", instrAddr);

                    currentBlock.Finalise(instr);

                    currentBlock = FindNextEmptyBlock();

                    if (currentBlock != null)
                        potentialNextBlock = GetPotentialNextBlock();
                }
                else if (instr.Primary == OpCode.Primary.Jump || instr.IsSkipping())
                {
                    ushort instrEndAddr = currentBlock.Finalise(instr);

                    // Will be overwritten for skipping instrs which have their target at instrAddr + size
                    ushort targetAddr = instr.Param.NNN;

                    if (instr.IsSkipping())
                    {
                        targetAddr = (ushort)(instrEndAddr + new Instruction.Instruction(_memory, instrEndAddr).Size);

                        // Will be walked later if needed
                        if (blocks.AddBlockSuccessor(currentBlock, true, instrEndAddr, out Block skippableBlock))
                            blockWalkingStack.Push(skippableBlock);
                    }

                    // If this jump created a new block then walk it, otherwise find a new block to walk
                    if (blocks.AddBlockSuccessor(currentBlock, false, targetAddr, out Block targetBlock))
                        currentBlock = targetBlock;
                    else
                        currentBlock = FindNextEmptyBlock();

                    if (currentBlock != null)
                        potentialNextBlock = GetPotentialNextBlock();
                }
                else
                {
                    currentBlock.Extend(instr);
                }
            }

            _logger.StopFunction();
            return;

        }

        const bool Dump = false;

        const bool Smc = true;

        int count = 0;

        private Program TranslateProgram(ushort baseAddr, ushort execAddr, BlockList blocks)
        {
            if (Smc)
                DiscoverBlocks(baseAddr, blocks);

            DiscoverBlocks(execAddr, blocks);
            if (Dump)
                File.WriteAllText(@"H:\TRANSLATED_ASSEMBLY.dot", blocks.GetGraphDotString());

            uint hash = (blocks.Hash() | ((uint)execAddr << 16)) ^ baseAddr;

            if (_compilationUnits.TryGetValue(hash, out Program prog)) {
                _logger.LogWarning("Hit Cache!");
                return prog;
            }

            Program.ProgramDelegate translatedProg = CilEmitter.Translate(blocks, _moduleBuilder, hash, execAddr, Smc);
            if (Dump)
                new AssemblyGenerator().GenerateAssembly(_moduleBuilder.Assembly, @"H:\TRANSLATED_ASSEMBLY.dll");

            prog = new(blocks.CodeSet, translatedProg, hash);

            _compilationUnits[prog.Hash] = prog;

            return prog;
        }

        public void ExecuteProgram(ushort startAddr, ref Register.Context ctx, InstructionHelpers instrHelpers, ushort[] stack)
        {
            ushort execAddr = startAddr;

            BlockList blocks = new();

            do
            {
                Program prog = TranslateProgram(startAddr, execAddr, blocks);

                _logger.StartFunction(string.Format("Invoke {0:X}", prog.Hash), Logger.Level.Warning);
                ushort smcRangeStart = 0;
                ushort smcRangeSize = 0;
                execAddr = prog.TranslatedProg.Invoke(ref ctx, instrHelpers, _memory, stack, prog.CodeSet, ref smcRangeStart, ref smcRangeSize);
                _logger.StopFunction();

                if (Smc)
                    blocks = new();
            } while (execAddr != 0);
        }

        
    }
}
