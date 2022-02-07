using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Chip8_CIL.Chip8.Translation
{
    class BlockList
    {
        // Insertions in the middle will generally be infrequent and we only move pointers even when they do happen
        // so a list makes the most sense here
        private readonly List<Block> _blocks;

        // Blocks that can be entered be returned to from a RET instruction or from a JUMPI instruction, used to generate the jump table block
        private readonly List<Block> _jumpTableEntries = new();

        public BlockList()
        {
            _blocks = new() { };
        }

        public readonly BitArray CodeSet = new(Chip8System.MemorySize);

        public ushort GetLastJumpTableEntryAddr() =>
            _jumpTableEntries.Count != 0 ? _jumpTableEntries[^1].StartAddr : (ushort)0;

        // Binary searches for the first block with an address that is >= addr
        // Returns true if the block at the returned lower bound contains the given address
        private int LowerBound(List<Block> list, ushort addr)
        {
            int lower = 0;
            int count = list.Count;

            while (count > 0)
            {
                int step = count / 2;
                int check = lower + step;

                if (list[check].StartAddr < addr)
                {
                    lower = check + 1;
                    count -= step + 1;
                }
                else
                {
                    count = step;
                }
            }

            return lower;
        }

        private int LowerBoundBlock(ushort addr) => LowerBound(_blocks, addr);

        private int LowerBoundJumpTableEntry(ushort addr) => LowerBound(_jumpTableEntries, addr);

        public Block GetNext(List<Block> list, ushort addr)
        {
            int lowerBoundIdx = LowerBound(list, addr);
            if (lowerBoundIdx == list.Count)
                return null;
            else
                return list[lowerBoundIdx];
        }

        public Block GetNextBlock(ushort addr) => GetNext(_blocks, addr);

        public Block GetNextJumpTableEntry(ushort addr) => GetNext(_jumpTableEntries, addr);

        // Adds a successor at the given address to the block splitting and linking as necessary
        // predecessorBlock MUST be finalised when calling this
        // Returns true if a new, zero length, block was inserted and false if an existing block was updated/split
        public bool AddBlockSuccessor(Block predecessorBlock, bool conditional, ushort successorAddr, out Block successorBlock)
        {
            Debug.Assert(predecessorBlock.Finalised);

            int lowerBoundIdx = LowerBoundBlock(successorAddr);
            Block lowerBoundBlock = lowerBoundIdx < _blocks.Count ? _blocks[lowerBoundIdx] : null;
            bool lowerBoundEqual = lowerBoundBlock != null && lowerBoundBlock.StartAddr == successorAddr;
            Block lowerBoundBlockPrev = lowerBoundIdx > 0 ? _blocks[lowerBoundIdx - 1] : null;

            // Check if the address is contained within an existing block
            if (lowerBoundEqual || lowerBoundBlockPrev != null && lowerBoundBlockPrev.EndAddr > successorAddr)
            {
                // if it's not equal then the previous block contains our address and we will have to split i t
                if (lowerBoundEqual)
                {
                    // Successor is an existing block, no insert needed
                    successorBlock = lowerBoundBlock;
                }
                else
                {
                    // Successor is in the middle of an existing block, split and insert
                    successorBlock = lowerBoundBlockPrev.Split(successorAddr);

                    // Insert the second part into the list
                    _blocks.Insert(lowerBoundIdx, successorBlock);
                }

                if (successorBlock == predecessorBlock)
                    successorBlock.AddBranch(conditional, successorBlock);
                else
                    predecessorBlock.AddBranch(conditional, successorBlock);

                return false;
            }
            else
            {
                // Create a new empty block
                successorBlock = new(successorAddr);

                // Insert it before the lower bound
                _blocks.Insert(lowerBoundIdx, successorBlock);

                // Link to it
                predecessorBlock.AddBranch(conditional, successorBlock);

                return true;
            }
        }

        public Block AddJumpTableEntry(ushort addr)
        {
            // Check for an existing block first
            int blockLowerBoundIdx = LowerBoundBlock(addr);
            Block lowerBoundBlock = blockLowerBoundIdx < _blocks.Count ? _blocks[blockLowerBoundIdx] : null;

            Block targetBlock = lowerBoundBlock; // Will be changed in the below if
            if (lowerBoundBlock == null || lowerBoundBlock.StartAddr != addr)
            {
                // Create new block
                targetBlock = new(addr);

                // Insert it before the lower bound
                _blocks.Insert(blockLowerBoundIdx, targetBlock);
            }


            // Add it to the sorted return target list
            int jumpTableLowerBoundIdx = LowerBoundJumpTableEntry(addr);

            _jumpTableEntries.Insert(jumpTableLowerBoundIdx, targetBlock);

            return targetBlock;
        }

        public void MarkAsCode(ushort addr, ushort size)
        { 
            for (ushort i = addr; i < addr + size; i++)
                CodeSet.Set(i, true);   
        }

        public void DispatchLinear(Action<Block> visitor)
        {
            foreach (Block block in _blocks)
                visitor(block);
        }

        public ushort Hash()
        {
            ushort val = 0;
            void HashBlock(Block block) => val ^= block.Hash();
            DispatchLinear(HashBlock);
            return val;
        }

        public string GetGraphDotString()
        {

            string BlockDesc(Block block)
            {
                if (block.TerminatingInstr.Primary == OpCode.Primary.Call)
                    return string.Format("0x{0:X}\ncall 0x{1:X}", block.StartAddr, block.TerminatingInstr.Param.NNN);
                else if (block.TerminatingInstr.Primary == OpCode.Primary.Jump)
                    return string.Format("0x{0:X}\njump 0x{1:X}", block.StartAddr, block.TerminatingInstr.Param.NNN);
                else if (block.TerminatingInstr.Primary == OpCode.Primary.Jumpi)
                    return string.Format("0x{0:X}\njumpi 0x{1:X} + V0", block.StartAddr, block.TerminatingInstr.Param.NNN);
                else
                    return string.Format("0x{0:X}", block.StartAddr);
            }


                StringBuilder sb = new(_blocks.Count * 80);

            sb.Append("digraph {\n");
            sb.Append(" splines=\"ortho\"\n");
            sb.Append(" nodesep=0.5\n");
            sb.Append(" ordering=\"out\"\n");

            sb.Append(" node [shape=box, height=0.5, fontname=\"monospace\"]\n");

            void PrintVisitor(Block block)
            {
                if (block.Successor != null || block.ConditionalSuccessor != null)
                {
                    if (block.Predecessors.Count == 0)
                        sb.AppendFormat(" {0} [fillcolor=\"green\" style=filled, label=\"{1}\"]\n", block.StartAddr,
                            BlockDesc(block));
                    else
                        sb.AppendFormat(" {0} [fillcolor=\"white\" style=filled, label=\"{1}\"]\n", block.StartAddr,
                            BlockDesc(block));

                    if (block.Successor != null)
                        sb.AppendFormat(" {0} -> {1} [taillabel=\"U\",color=green]\n", block.StartAddr,
                             block.Successor.StartAddr);

                    if (block.ConditionalSuccessor != null)
                        sb.AppendFormat(" {0} -> {1} [taillabel=\"C\",color=blue]\n", block.StartAddr,
                            block.ConditionalSuccessor.StartAddr);
                }
                else
                {
                    string Colour(Instruction.Instruction instr)
                    {
                        switch (instr.Primary)
                        {
                            case OpCode.Primary.Jumpi:
                                return "blue";
                            case OpCode.Primary.Secondary0:
                                {
                                    OpCode.Secondary0 secondary = (OpCode.Secondary0)(instr.Raw & (ushort)OpCode.Secondary0.Mask);

                                    switch (secondary)
                                    {
                                        case OpCode.Secondary0.Tertiary0E:
                                            {
                                                OpCode.Tertiary0E tertiary = (OpCode.Tertiary0E)(instr.Raw & (ushort)OpCode.Tertiary0E.Mask);

                                                switch (tertiary)
                                                {
                                                    case OpCode.Tertiary0E.Rts:
                                                        return "yellow";
                                                    default:
                                                        return "grey";
                                                }
                                            }
                                        default:
                                            return "grey";
                                    }
                                }
                            default:
                                return "grey";
                        }
                    }

                    sb.AppendFormat(" {0} [fillcolor=\"{2}\" style=filled, label=\"{1}\"]\n", block.StartAddr, 
                        BlockDesc(block), Colour(block.TerminatingInstr));
                }


            }

            DispatchLinear(PrintVisitor);

            sb.Append("}\n");

            return sb.ToString();
        }
    }
}
