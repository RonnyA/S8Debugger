using System;
using System.Collections.Generic;
using System.IO;
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


        Stack<UInt16> stack = new Stack<UInt16>();

        public bool Push(UInt16 pc)
        {
            if (stack.Count > RECURSION_LIMIT) return false;
            stack.Push(pc);

            return true;
        }
        public UInt16 Pop()
        {
            if (stack.Count == 0) return 0xFFFF;
            return stack.Pop();
        }

    }

    public class CpuState
    {
        public const int MemSize = 4096;
        public int inputPtr = 0;
        public int tick = 0;
        public UInt16 pc = 0;
        public bool flag = false;
        public byte[] regs = new byte[16];
        public byte[] memory = new byte[MemSize];
        // Single breakpoint "covers" the whole word
        public bool[] breakpoints    = new bool[MemSize];

        public UInt16 memoryUsed = 0;


        public bool crashed;

        public byte[] stdin = new byte[0];
        public MemoryStream outputStream = new MemoryStream();
        public int maxTicks;

#if _EXPERIMENTAL_
        public Hardware.S8FrameBuffer HWDisplay = new Hardware.S8FrameBuffer();
        public Hardware.S8IO HWIO = new Hardware.S8IO();
#endif

    };

    public class S8CPU
    {
        
        #region LogEventing
        public delegate void LogMessageEventHandler(Object sender, LogMessageEventArgs e);

        public event LogMessageEventHandler MessageHandler;
        protected virtual void OnLogMessage(LogMessageEventArgs e)
        {
            LogMessageEventHandler handler = MessageHandler;
            handler?.Invoke(this, e);
        }

        private void LogMessage(string message = "")
        {
            LogMessageEventArgs ea = new LogMessageEventArgs();
            ea.LogMessage = message;
            OnLogMessage(ea);
        }
        #endregion LogEventing


        #region CpuStepInfo
        public delegate void CpuStepInfoEventHandler(Object sender, CpuStepInfo e);

        public event CpuStepInfoEventHandler CpuStepHandler;
        protected virtual void OnCpuStepInfo(CpuStepInfo e)
        {
            CpuStepInfoEventHandler handler = CpuStepHandler;
            handler?.Invoke(this, e);
        }

        private void CpuStepInfo(UInt16 pc)
        {
            CpuStepInfo csi = new CpuStepInfo();
            csi.pc = pc;
            OnCpuStepInfo(csi);
        }
        #endregion LogEventing

        public bool VerboseMode {get; set;}
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
            state.tick = 0; //reset tick counter also

        }

        // Stop a running CPU
        public void Stop()
        {
            state.crashed = true;
        }

        public void ToggleBreakpoint(UInt16 addr)
        {
            bool current = state.breakpoints[addr];
            state.breakpoints[addr] = !current;
        }

        public void SetBreakpoint(UInt16 addr)
        {
            state.breakpoints[addr] = true;
        }

        public void ClearBreakpoint(UInt16 addr)
        {
            state.breakpoints[addr] = false;
        }


        public void ClearAllBreakpoints()
        {
            for (int i = 0; i < state.breakpoints.Length; i++)
            {
                state.breakpoints[i] = false;
            }
        }


        //const memory = load(executable);
        //let stdout = new Uint8Array();
        //const backtrace: number[] = [];

        public byte[] Load(byte[] executable)
        {
            int oldMaxTicks = DEFAULT_MAX_STEPS;
            if (state is not null)
            {
                oldMaxTicks = state.maxTicks;
            }
            state = new CpuState();
            SetMaxTicks(oldMaxTicks);

            ResetRegs();

            if (executable.Length > 4096)
            {
                LogMessage(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.fileSizeTooBig]);
                return null;
            }

            if (!validateMagic(executable))
            {
                LogMessage(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.unsupportedExecutable]);
                return null;
            }

            state.memory = new byte[4096];
            int seek = 7;

            UInt16 i = 0;
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


            state.outputStream = new MemoryStream();

            // After each step in the CPU tell anyone listening to events about the step. Used to update UI
            CpuStepInfo(state.pc);

        }

        public void Run()
        {
            ResetRegs();
            RunSteps(0);
        }

        /// <summary>
        /// Run the code "runSteps" number of cpu steps. 
        /// </summary>
        /// <param name="runSteps">Number of CPU steps to run. 0 means no limit, runs until STOPP or crash</param>        
        /// <param name="verbose"></param>
        /// <param name="showaddress"></param>
        /// <returns></returns>
        internal bool RunSteps(int runSteps)
        {

            int stepsLeft = 1;// Default 1 step left

            if (runSteps >0)
            {
                stepsLeft = runSteps;
            }
            

            if (state.memoryUsed == 0)
            {
                LogMessage("No s8 program loaded");
                return false;
            }

            if (state.memoryUsed == 0) return false;

            while ((stepsLeft>0) && (!state.crashed))
            {
                // if there is a limit of how many steps we can run, reduce the steps left
                if (runSteps>0)
                {
                    stepsLeft--;
                }


                if (++state.tick > state.maxTicks)
                {
                    var strErr = ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.resourcesExhausted].Replace("${maxTicks}", state.tick.ToString());
                    LogMessage(strErr);
                    return false;
                }


                byte opcode = state.memory[state.pc];
                byte param = state.memory[state.pc + 1];
                //yield { pc, flag, regs, memory, stdout, inputPtr };

                S8Instruction s8 = new S8Instruction(opcode, param);

                if (!ExecuteInstruction(s8))
                {
                    state.crashed = true;
                }

                // After each step in the CPU tell anyone listening to events about the step. Used to update UI
                CpuStepInfo(state.pc);

                if (state.breakpoints[state.pc]) return true;

            }

            if (state.crashed) return false;
            return true;
        }

        public bool Step(int steps = 1)
        {            
            return RunSteps(steps);
        }

#if _EXPERIMENTAL_

        // For clarity: These two metods calculate the address identical to the LAST/LAGR addr calculation

        UInt16 GetFrameBufferAddress()
        {
            return (UInt16)(this.state.regs[0] + ((UInt16)this.state.regs[1] << 8));
        }


        UInt16 GetIOAddress()
        {
            return (UInt16)(this.state.regs[0] + ((UInt16)this.state.regs[1] << 8));
        }
#endif

        public bool ExecuteInstruction(S8Instruction instr)
        {            
            LogInstructionToDebugger(instr);

            // STOPP
            if (instr.operationClass == 0x0) return false;

            // increase counter if we do anything else than STOPP
            state.pc += 2;


            // SETT
            if (instr.operationClass == 0x1)
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
                switch (instr.operation)
                {
                    case 0: // LAST
                        state.regs[instr.argument1] = state.memory[addr];
                        break;
                    case 1: //LAGR
                        state.memory[addr] = (byte)state.regs[instr.argument1];
                        break;
#if _EXPERIMENTAL_
                    case 2: //VLAST
                        state.regs[instr.argument1] = state.HWDisplay.Memory[GetFrameBufferAddress()];
                        break;
                    case 3: //VLAGR
                        UInt16 hwaddr = GetFrameBufferAddress();
                        state.HWDisplay.Write(hwaddr, (byte)state.regs[instr.argument1]);
                        break;
#endif
                    default:
                        LogMessage(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.segmentationFault]);
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
                    LogMessage(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.segmentationFault]);
                    return false;
                }
            }

            // I/O
            else if (instr.operationClass == 0x6)
            {

                switch (instr.operation)
                {
                    case 0x0: // LES
                        if (state.stdin.Length > state.inputPtr)
                        {
                            state.regs[instr.argument1] = state.stdin[state.inputPtr++];
                        }
                        else
                        {
                            LogMessage(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.readAfterEndOfInput]);
                            return false;
                        }

                        break;

                    case 0x01: // SKRIV
                        {
                            if (state.outputStream.Length > 1000)
                            {
                                LogMessage(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.segmentationFault]);
                                return false;
                            }

                            byte val = state.regs[instr.argument1];
                            //state.stdout += val.ToString("X2");

                            state.outputStream.Seek(0, SeekOrigin.End);
                            state.outputStream.WriteByte(val);
                        }
                        break;
#if _EXPERIMENTAL_
                    case 0x02: //INN
                        state.regs[instr.argument1] = this.state.HWIO.ReadIO(GetIOAddress());
                        break;

                    case 0x03: //UT
                        this.state.HWIO.WriteIO(GetIOAddress(), state.regs[instr.argument1]);
                        break;

                    case 0x04: //VSYNK
                        this.state.HWDisplay.VSync();
                        break;

#endif
                    default:
                        {
                            LogMessage(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.segmentationFault]);
                            return false;
                        }
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
                    LogMessage(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.segmentationFault]);
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
                    LogMessage(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.recursionLimitExceeded]);
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
                    LogMessage(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.segmentationFault]);
                    return false;
                }
            }
            else if (instr.operationClass == 0xc)
            {
                // No Op.. Do nothing
            }
            else
            {
                LogMessage(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.segmentationFault]);
                return false;
            }


            return true;
        }

        public byte[] prevRegs = new byte[16];
        private void LogInstructionToDebugger(S8Instruction instr)
        {
            if (VerboseMode)
            {
                var regs = GetChangedRegs();
                if (!string.IsNullOrEmpty(regs)) LogMessage(regs);
                instr.DecodeInstruction();
                LogMessage(instr.Instruction2Text(state.pc, true));
                prevRegs = (byte[])state.regs.Clone();
            }
        }

        private string GetChangedRegs()
        {
            var changed = string.Empty;
            for (int i = 0; i < 15; i++)
            {
                if (prevRegs[i] != state.regs[i])
                    changed += $"[r{i}: 0x{prevRegs[i]:X2} -> 0x{state.regs[i]:X2}]"; // #TODO Make it hex -> "X2"
            }
            return changed;
        }

    }
}
