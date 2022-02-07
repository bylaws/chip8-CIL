using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Chip8_CIL.Chip8.Translation
{
    // A block represents a linear range of code linked into a control flow graph
    class Block
    {
        public ushort StartAddr { get; private set; }
        public ushort EndAddr { get; private set; } // Exclusive end address, if Start == End then the block contains nothing

        public List<Block> Predecessors { get; private set; } = new();

        public Block Successor { get; private set; } = null;
        public Block ConditionalSuccessor { get; private set; } = null;  // Block directly following a skippable instruction branch

        public bool Finalised { get; private set; }

        public Instruction.Instruction TerminatingInstr { get; private set; }

        private readonly List<Instruction.Instruction> _instrs = new();

        public Block(ushort startAddr)
        {
            StartAddr = startAddr;
            EndAddr = startAddr;
        }

        protected Block(ushort startAddr, ushort endAddr, List<Block> predecessors, Block successor,
            Block conditionalSuccessor, List<Instruction.Instruction> instrs,
            Instruction.Instruction terminatingInstr)
        {
            StartAddr = startAddr;
            EndAddr = endAddr;
            Predecessors = predecessors;
            Successor = successor;
            ConditionalSuccessor = conditionalSuccessor;
            Finalised = true;

            _instrs = instrs;
            TerminatingInstr = terminatingInstr;
        }

        public void DispatchInstructions(Action<Instruction.Instruction, ushort> visitor)
        {
            ushort addr = StartAddr;
            foreach (Instruction.Instruction instr in _instrs)
            {
                visitor(instr, addr);
                addr += instr.Size;
            }
        }

        // Checks if a block is completely empty and contains nothing
        public bool Empty() => StartAddr == EndAddr;

        // Extends a blocks length by incr and adds an instruction to it
        public ushort Extend(Instruction.Instruction instr)
        {
            Debug.Assert(!Finalised);

            EndAddr += instr.Size;
            _instrs.Add(instr);

            return EndAddr;
        }

        // Adds a terminating instruction to the block and locks in the block's size
        // After this the block should then be linked to give a target for the terminating instruction
        public ushort Finalise(Instruction.Instruction terminatingInstr)
        {
            Debug.Assert(!Finalised);

            EndAddr += terminatingInstr.Size;

            Debug.Assert(!Empty());

            TerminatingInstr = terminatingInstr;
            Finalised = true;

            return EndAddr;
        }

        public ushort Hash()
        {
            ushort val = 0;
            void HashInstr(Instruction.Instruction instr, ushort addr) => val ^= instr.Raw;

            DispatchInstructions(HashInstr);
            return val;
        }

        // Splits this block into two at the given offset, returning the new block
        // splitAddr MUST be within this block and this block MUST be finalised
        public Block Split(ushort splitAddr)
        {
            Debug.Assert(Finalised);

            int instrIndex = 0;
            ushort instrOffset = StartAddr;
            for (; instrOffset < splitAddr; instrOffset += _instrs[instrIndex].Size, instrIndex++) ;

            Debug.Assert(instrOffset == splitAddr);


            // Copy the instructions where the new block will be into a new list
            List<Instruction.Instruction> successorInstrs = _instrs.GetRange(instrIndex, _instrs.Count - instrIndex);

            // Remove this range from our instructions
            _instrs.RemoveRange(instrIndex, _instrs.Count - instrIndex);

            // Create the second part using our end and successors linked to ourself
            Block successorBlock = new(splitAddr, EndAddr, new List<Block> { this }, Successor, ConditionalSuccessor,
                successorInstrs, TerminatingInstr);

            // Shrink ourself
            EndAddr = splitAddr;

            // Mark our block as ending with a jump to the successor
            TerminatingInstr = new(OpCode.Primary.Jump);

            // Add the split half as our only successor
            Successor = successorBlock;

            ConditionalSuccessor = null;

            return successorBlock;
        }

        // Adds an branch to the successor block, this block MUST be finalised before calling this
        public void AddBranch(bool conditional, Block successor)
        {
            Debug.Assert(Finalised);

            if (conditional)
                ConditionalSuccessor = successor;
            else
                Successor = successor;

            successor.Predecessors.Add(this);
        }
    }
}
