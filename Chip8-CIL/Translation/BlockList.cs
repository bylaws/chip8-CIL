using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Chip8_CIL
{ 
    class BlockList
    {
        private readonly Block _rootBlock;

        // Insertions in the middle will generally be infrequent and we only move pointers even when they do happen
        // so a list makes the most sense here
        private readonly List<Block> _blocks;

        // Blocks that can be entered be returned to from a RET instruction, used to generate the return dispatch block
        private readonly List<Block> _returnTargets = new();


        public BlockList(Block rootBlock)
        {
            _rootBlock = rootBlock;
            _blocks = new() { rootBlock };
        }

        public ushort GetStartAddr() => _rootBlock.StartAddr;

        public ushort GetLastReturnTargetAddr() =>
            (_returnTargets.Count != 0) ? _returnTargets[_returnTargets.Count - 1].StartAddr : (ushort)0;

        // Binary searches for the first block with an address that is >= addr
        // Returns true if the block at the returned lower bound contains the given address
        int LowerBound(List<Block> list, ushort addr)
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

        int LowerBoundBlock(ushort addr) => LowerBound(_blocks, addr);

        int LowerBoundReturnTarget(ushort addr) => LowerBound(_returnTargets, addr);

        public Block GetNext(List<Block> list, ushort addr)
        {
            int lowerBoundIdx = LowerBound(list, addr);
            if (lowerBoundIdx == list.Count)
                return null;
            else
                return list[lowerBoundIdx];
        }

        public Block GetNextBlock(ushort addr) => GetNext(_blocks, addr);

        public Block GetNextReturnTarget(ushort addr) => GetNext(_returnTargets, addr);

        // Adds a successor at the given address to the block splitting and linkign as necessary
        // predecessorBlock MUST be finalised when calling this
        // Returns true if a new, zero length, block was inserted and false if an existing block was updated/split
        public bool AddBlockSuccessor(Block predecessorBlock, bool conditional, ushort successorAddr, out Block successorBlock)
        {
            Debug.Assert(predecessorBlock.Finalised);

            int lowerBoundIdx = LowerBoundBlock(successorAddr);
            Block lowerBoundBlock = (lowerBoundIdx < _blocks.Count) ? _blocks[lowerBoundIdx] : null;
            bool lowerBoundEqual = lowerBoundBlock != null && lowerBoundBlock.StartAddr == successorAddr;
            Block lowerBoundBlockPrev = (lowerBoundIdx > 0) ? _blocks[lowerBoundIdx - 1] : null;

            // Check if the address is contained within an existing block
            if (lowerBoundEqual || (lowerBoundBlockPrev != null && lowerBoundBlockPrev.EndAddr > successorAddr))
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

        public Block AddReturnTarget(ushort addr)
        {
            // Check for an existing block first
            int blockLowerBoundIdx = LowerBoundBlock(addr);
            Block lowerBoundBlock = (blockLowerBoundIdx < _blocks.Count) ? _blocks[blockLowerBoundIdx] : null;

            Block targetBlock = lowerBoundBlock; // Will be changed in the below if
            if (lowerBoundBlock == null || lowerBoundBlock.StartAddr != addr)
            {
                // Create new block
                targetBlock = new(addr);

                // Insert it before the lower bound
                _blocks.Insert(blockLowerBoundIdx, targetBlock);
            }


            // Add it to the sorted return target list
            int returnTargetLowerBoundIdx = LowerBoundReturnTarget(addr);

            _returnTargets.Insert(returnTargetLowerBoundIdx, targetBlock);

            return targetBlock;
        }

        public void DispatchPostOrder(Action<Block> visitor, Block block, HashSet<ushort> visited)
        {
            if (block == null || visited.Contains(block.StartAddr))
                return;

            visited.Add(block.StartAddr);

            DispatchPostOrder(visitor, block.ConditionalSuccessor, visited);

            DispatchPostOrder(visitor, block.Successor, visited);

            visitor(block);
        }

        public void DispatchPostOrder(Action<Block> visitor) => DispatchPostOrder(visitor, _rootBlock, new());

        public void DispatchLinear(Action<Block> visitor)
        {
            foreach (Block block in _blocks)
                visitor(block);
        }

        public string GetGraphDotString()
        {
            void Visitor(Block block) => block.FillInUsageInfo();
            DispatchLinear(Visitor);

            string BlockDesc(Block block) => string.Format("0x{0:X}\\n\\nRead: {1}\\n Clobbered: {2}\\n Changed: {3}", block.StartAddr,
                block.RegisterUsageInfo.Read, block.RegisterUsageInfo.Clobbered, block.RegisterUsageInfo.Changed);


            StringBuilder sb = new(_blocks.Count * 80);

            sb.Append("digraph {\n");
            sb.Append(" splines=\"ortho\"\n");
            sb.Append(" nodesep=0.5\n");
            sb.Append(" ordering=\"out\"\n");

            sb.Append(" node [shape=box, height=0.5, fontname=\"monospace\"]\n");

            // Add root block to top with styling
            sb.AppendFormat(" {0} [fillcolor=\"grey\" style=filled, label=\"{1}\"]\n", _rootBlock.StartAddr,
                BlockDesc(_rootBlock));

            void PrintVisitor(Block block)
            {
                if (block != _rootBlock)
                    sb.AppendFormat(" {0} [fillcolor=\"white\" style=filled, label=\"{1}\"]\n", block.StartAddr,
                        BlockDesc(block));

                if (block.Successor != null)
                    sb.AppendFormat(" {0} -> {1} [taillabel=\"U\",color=green]\n", block.StartAddr,
                         block.Successor.StartAddr);

                if (block.ConditionalSuccessor != null)
                    sb.AppendFormat(" {0} -> {1} [taillabel=\"C\",color=blue]\n", block.StartAddr,
                        block.ConditionalSuccessor.StartAddr);
            }

            DispatchPostOrder(PrintVisitor);

            sb.Append("}\n");

            return sb.ToString();
        }
    }
}
