using Lokad.ILPack;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Chip8_CIL.Chip8.Translation
{
    class Translator
    {
        private readonly byte[] _memory;
        private readonly Logger _logger;

        private readonly ModuleBuilder _moduleBuilder;

        public delegate void ProgramDelegate(ref Register.Context ctx, InstructionHelpers helpers, byte[] memory, ushort[] returnAddressStack);

        public Translator(byte[] memory, Logger logger)
        {
            _memory = memory;
            _logger = logger;


            AssemblyBuilder asmBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(Guid.NewGuid().ToString()),
            AssemblyBuilderAccess.Run);

            _moduleBuilder = asmBuilder.DefineDynamicModule("MyDynamicAsm");
        }

        private BlockList DiscoverBlocks(ushort startAddr)
        {
            _logger.StartFunction("DiscoverBlocks", Logger.Level.Debug);

            Stack<Block> blockWalkingStack = new();

            Block currentBlock = new(startAddr);
            Block potentialNextBlock = null;


            BlockList blocks = new(currentBlock);


            Block FindNextEmptyBlock()
            {
                Block emptyBlock;

                while (blockWalkingStack.TryPop(out emptyBlock) && !emptyBlock.Empty()) { }

                return emptyBlock;
            }

            Block GetPotentialNextBlock() => blocks.GetNextBlock((ushort)(currentBlock.EndAddr + 1));

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


                if (instr.Primary == OpCode.Primary.Call)
                {
                    ushort targetAddr = instr.Param.NNN;

                    _logger.LogVerbose("Call to {0:x} at: {1:x}", targetAddr, instrAddr);

                    // This treats the call as a block terminator instruction
                    ushort returnTargetAddr = currentBlock.Finalise(instr);

                    // Add the return target of call to the block list and mark it for walking
                    blockWalkingStack.Push(blocks.AddReturnTarget(returnTargetAddr));

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

                    // Will be overwritten for skipping instrs which have their target at instrAddr + 2 * Width
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
            return blocks;

        }

        // Translates a chip8 subroutine into a c# 'SubroutineDelegate' and caches it in the sub table
        public ProgramDelegate TranslateProgram(ushort startAddr)
        {

            BlockList blocks = DiscoverBlocks(startAddr);
            //if (Settings.DumpEnabled)
              //  File.WriteAllText(Settings.GetDumpPathFor(string.Format("PROG.dot", startAddr)), blocks.GetGraphDotString());

            return CilEmitter.Translate(blocks, _moduleBuilder);
        }

        public void DumpAssembly()
        {
            AssemblyGenerator generator = new();
      //      generator.GenerateAssembly(_moduleBuilder.Assembly, Settings.GetDumpPathFor("TRANSLATED_ASSEMBLY.dll"));
        }
    }
}
