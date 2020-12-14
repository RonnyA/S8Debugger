using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

namespace S8Debugger
{    
    public class CpuStack
    {
        int RECURSION_LIMIT = 100;
        

        Stack<int> stack = new Stack<int>();
        
        public bool Push(int pc)
        {
            if (stack.Count > RECURSION_LIMIT) return false;
            stack.Push(pc);

            return true;
        }
        public int Pop()
        {
            if (stack.Count == 0) return -1;
            return stack.Pop();
        }

    }

    public class CpuState
    {
        public int inputPtr = 0;
        public int tick = 0;
        public int pc = 0;
        public bool flag = false;
        public byte[] regs = new byte[16];
        public byte[] memory = null;
        public int memoryUsed = 0;


        public bool crashed;

        public byte[] stdin = new byte[1];
        public string stdout = "";
        public int maxTicks;
    };

    public class S8CPU
    {
        int DEFAULT_MAX_STEPS = 50000; //Default allow 50.000 ticks

        public CpuState state;
        public CpuStack stack;

        public enum ERROR_MESSAGE_ID { segmentationFault, recursionLimitExceeded, fileSizeTooBig, readAfterEndOfInput, unsupportedExecutable, resourcesExhausted };

        public static string[] ERROR_MESSAGE =
        {
            "Segmenteringsfeil",
            "Alt for mange funksjonskall inni hverandre",
            "Programmet får ikke plass i minnet",
            "Programmet gikk tom for føde",
            "Dette skjønner jeg ingenting av",
            "Programmet ble brutalt drept etter å ha benyttet hele ${maxTicks} sykluser"
        };

        /// <summary>
        /// Ctor
        /// </summary>
        public S8CPU()
        {
            state = new CpuState();
            stack = new CpuStack();

            SetMaxTicks(DEFAULT_MAX_STEPS); 
        }


        public void SetMaxTicks(int Ticks)
        {
            state.maxTicks = Ticks;
            ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.resourcesExhausted] = ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.resourcesExhausted].Replace("${maxTicks}", Ticks.ToString());
        }

        //const memory = load(executable);
        //let stdout = new Uint8Array();
        //const backtrace: number[] = [];

        public byte[] Load(byte[] executable, bool skipMagicHeader)
        {
            state = new CpuState();
            SetMaxTicks(DEFAULT_MAX_STEPS);

            ResetRegs();

            if (executable.Length > 4096)
            {
                Console.WriteLine(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.fileSizeTooBig]);
                return null;
            }

            if (!skipMagicHeader)
            { 
                if (!validateMagic(executable))
                {
                    Console.WriteLine(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.unsupportedExecutable]);
                    return null;
                }
            }

            state.memory = new byte[4096];
            int seek = 7;
            if (skipMagicHeader)
            {
                seek = 0;
            }

            int i = 0;
            while (seek < executable.Length)
            {
                state.memory[i++] = executable[seek++];
            }
            state.memoryUsed = i;

            return state.memory;
        }

        private bool validateMagic(byte[] bytes)
        {

            var MAGIC = new byte[] { 0x2E, 0x53, 0x4C, 0x45, 0x44, 0x45, 0x38 };
            if (bytes.Length <= MAGIC.Length)
            {
                return false;
            }

            for (int i = 0; i < MAGIC.Length; i++)
            {
                if (bytes[i] != MAGIC[i])
                {
                    return false;
                }
            }
            return true;
        }


        /// <summary>
        /// Reset CPU counter and registers
        /// </summary>
        public void ResetRegs()
        {
            state.inputPtr = 0;
            state.tick = 0;

            state.pc = 0;
            state.flag = false;
            state.crashed = false;

            for (int i = 0; i < 16; i++)
            {
                state.regs[i] = 0;
            }


            state.stdout = "";
        }

        public void Run()
        {
            ResetRegs();
            RunUntil(state.memoryUsed);
        }

        internal bool RunUntil(int stop_pc_at)
        {

            if (state.memoryUsed == 0)
            {
                Console.WriteLine("No s8 program loaded");
                return false;
            }

            if (state.memoryUsed == 0) return false;

            while ((state.pc < stop_pc_at) && (!state.crashed))
            {
                if (++state.tick > state.maxTicks)
                {
                    Console.WriteLine(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.resourcesExhausted]);
                    return false;
                }


                byte opcode = state.memory[state.pc];
                byte param = state.memory[state.pc + 1];
                //yield { pc, flag, regs, memory, stdout, inputPtr };

                S8Instruction s8 = new S8Instruction(opcode, param);
                state.pc += 2;

                if (!ExecuteInstruction(s8))
                {
                    state.crashed = true;                    
                }
            }

            if (state.crashed) return false;
            return true;
        }

        public bool Step(int steps=1)
        {
            state.crashed = false;

            return RunUntil(state.pc+ (steps*2));
        }


        public bool ExecuteInstruction(S8Instruction instr)
        {
            // HALT
            if (instr.operationClass == 0x0) return false;

            // SET

            else if (instr.operationClass == 0x1)
            {
                state.regs[instr.operation] = (byte)instr.value;
            }
            else if (instr.operationClass == 0x2)
            {
                state.regs[instr.operation] = state.regs[instr.argument1];
            }
            // FINN
            else if (instr.operationClass == 0x3)
            {
                state.regs[1] = (byte)((instr.address & 0x0f00) >> 8);
                state.regs[0] = (byte)(instr.address & 0xff);
            }

            // LOAD / STORE
            else if (instr.operationClass == 0x4)
            {
                int addr = ((state.regs[1] << 8) | state.regs[0]) & 0xfff;
                if (instr.operation == 0) state.regs[instr.argument1] = state.memory[addr];
                else if (instr.operation == 1) state.memory[addr] = (byte)state.regs[instr.argument1];
                else
                {
                    Console.WriteLine(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.segmentationFault]);
                    return false;
                }
            }

            // ALU
            else if (instr.operationClass == 0x5)
            {
                byte reg1 = state.regs[instr.argument1];
                byte reg2 = state.regs[instr.argument2];

                if (instr.operation == 0x0) state.regs[instr.argument1] &= reg2;
                else if (instr.operation == 0x1) state.regs[instr.argument1] |= reg2;
                else if (instr.operation == 0x2) state.regs[instr.argument1] ^= reg2;
                else if (instr.operation == 0x3)
                    state.regs[instr.argument1] = (byte)((reg1 << reg2) & 0xff);
                else if (instr.operation == 0x4) state.regs[instr.argument1] >>= reg2;
                else if (instr.operation == 0x5)
                    state.regs[instr.argument1] = (byte)((reg1 + reg2) & 0xff);
                else if (instr.operation == 0x6)
                    state.regs[instr.argument1] = (byte)((reg1 - reg2) & 0xff);
                else
                {
                    Console.WriteLine(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.segmentationFault]);
                    return false;
                }
            }

            // I/O
            else if (instr.operationClass == 0x6)
            {
                // READ
                if (instr.operation == 0x0)
                {
                    if (state.stdin.Length > state.inputPtr)
                    {
                        state.regs[instr.argument1] = state.stdin[state.inputPtr++];
                    }
                    else
                    {
                        Console.WriteLine(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.readAfterEndOfInput]);
                        return false;
                    }
                }

                // WRITE
                else if (instr.operation == 0x1)
                {
                    byte val = state.regs[instr.argument1];
                    state.stdout += val.ToString("X2");
                }
                else
                {
                    Console.WriteLine(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.segmentationFault]);
                    return false;
                }
            }
            // CMP
            else if (instr.operationClass == 0x7)
            {
                byte reg1 = state.regs[instr.argument1];
                byte reg2 = state.regs[instr.argument2];

                if (instr.operation == 0x0) state.flag = reg1 == reg2;
                else if (instr.operation == 0x1) state.flag = reg1 != reg2;
                else if (instr.operation == 0x2) state.flag = reg1 < reg2;
                else if (instr.operation == 0x3) state.flag = reg1 <= reg2;
                else if (instr.operation == 0x4) state.flag = reg1 > reg2;
                else if (instr.operation == 0x5) state.flag = reg1 >= reg2;
                else
                {
                    Console.WriteLine(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.segmentationFault]);
                    return false;
                }
            }

            // JMP
            else if (instr.operationClass == 0x8) state.pc = instr.address;
            // COND JMP
            else if (instr.operationClass == 0x9)
            {
                if (state.flag)
                {
                    state.pc = instr.address;
                }
            }

            // CALL
            else if (instr.operationClass == 0xa)
            {


                if (!stack.Push(state.pc))
                {
                    Console.WriteLine(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.recursionLimitExceeded]);
                    return false;
                }
                state.pc = instr.address;
            }

            // RET
            else if (instr.operationClass == 0xb)
            {
                state.pc = stack.Pop();
                if (state.pc < 0)
                {
                    state.pc = 0;
                    Console.WriteLine(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.segmentationFault]);
                    return false;
                }
            }
            else if (instr.operationClass == 0xc) return true;
            else
            {
                Console.WriteLine(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.segmentationFault]);
                return false;
            }

            return true;
        }
    }
}
   