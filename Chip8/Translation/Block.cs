using System;
using System.Collections.Generic;
using System.Diagnostics;

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

        public Register.UsageInfo RegisterUsageInfo = new();
        public Register.Set EntryLoads = new();
        public Register.Set ExitSaves = new();

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

        public void DispatchInstructions(Action<Instruction.Instruction> visitor)
        {
            foreach (Instruction.Instruction instr in _instrs)
                visitor(instr);
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

        public void FillInUsageInfo()
        {
            void Visitor(Instruction.Instruction instr)
            {
                Register.UsageInfo instrUsage = instr.GetRegisterUsageInfo();

                // As we do clobbers on a block level we can't clobber registers that have been read earlier in the block,
                // otherwise registers that should be saved might not be saved before entering the block
                Register.Set unchangedUnreadClobbers = instrUsage.Clobbered & ~RegisterUsageInfo.Changed &
                    ~RegisterUsageInfo.Read;
                RegisterUsageInfo.Clobbered |= unchangedUnreadClobbers;

                // If a register has been changed then it has implicitly been read,
                // if a register has been clobbered the read doesn't need to be tracked
                Register.Set unchangedUnclobberedRead = instrUsage.Read & ~RegisterUsageInfo.Changed &
                    ~RegisterUsageInfo.Clobbered;
                RegisterUsageInfo.Read |= unchangedUnclobberedRead;

                // Changes don't need to be tracked to clobbered registers
                Register.Set unclobberedChanged = instrUsage.Changed & ~RegisterUsageInfo.Clobbered;
                RegisterUsageInfo.Changed |= unclobberedChanged;
            }

            DispatchInstructions(Visitor);
            Visitor(TerminatingInstr);

            EntryLoads = RegisterUsageInfo.Changed | RegisterUsageInfo.Read;
            ExitSaves = RegisterUsageInfo.Clobbered | RegisterUsageInfo.Changed;
        }
    }
}
