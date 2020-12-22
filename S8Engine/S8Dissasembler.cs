using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace S8Debugger
{
    public class S8Dissasembler
    {

        #region Eventing

        public delegate void LogMessageEventHandler(Object sender, LogMessageEventArgs e);

        public event LogMessageEventHandler Message;
        protected virtual void OnLogMessage(LogMessageEventArgs e)
        {
            LogMessageEventHandler handler = Message;
            handler?.Invoke(this, e);
        }

        private void LogMessage(string message = "")
        {
            LogMessageEventArgs ea = new LogMessageEventArgs();
            ea.LogMessage = message;
            LogMessage(ea);
        }

        private void LogMessage(LogMessageEventArgs ea)
        {                        
            OnLogMessage(ea);
        }

        #endregion
        byte[] bytes = new byte[4096];
        S8CPU cpu;

        /// <summary>
        /// Ctor
        /// </summary>
        public S8Dissasembler()
        {
            cpu = new S8CPU();
            cpu.Message += Cpu_Message;
        }

        private void Cpu_Message(object sender, LogMessageEventArgs e)
        {
            LogMessage(e);  
        }

        public bool Init(string fname)
        {

            if (!File.Exists(fname)) return false;

            var executable = File.ReadAllBytes(fname);
            bytes = cpu.Load(executable);

            if (bytes is null) return false;

            LogMessage("Loaded image " + cpu.state.memoryUsed + " bytes");
            return true;
        }

        internal bool InitFromMemory(byte[] s8prog)
        {
            bytes = cpu.Load(s8prog);

            if (bytes is null) return false;
            LogMessage("Loaded image " + cpu.state.memoryUsed + " bytes");
            return true;
        }

        public UInt16 MemoryDump(UInt16 start, UInt16 length, bool showAddress = false, bool allowOutsideLoadedMemory = false)
        {
            UInt16 currentAddress = start;
            UInt16 endAddress = (UInt16) (currentAddress + length);

            // Special - if no length givien, assume 40 bytes (4 lines)
            if (length == 0)
            {
                endAddress = (UInt16)(currentAddress + 40);
                //endAddress = cpu.state.memoryUsed;
            }

            if (!allowOutsideLoadedMemory)
            {
                if (endAddress > cpu.state.memoryUsed)
                {
                    endAddress = cpu.state.memoryUsed;
                }
            }


            int lineCounter = 0;

            string line1 = "";
            string line2 = "";


            while (currentAddress < endAddress)
            {

                if (lineCounter == 0)
                {
                    string sHexAddress = currentAddress.ToString("X3");
                    LogMessage("m" + sHexAddress + ":");
                    line1 = ".DATA ";
                    line2 = ";" + currentAddress.ToString("X3") + ":  ";
                }
                else
                {
                    line1 += ", ";
                    line2 += "  ";

                }
                byte b = bytes[currentAddress];
                //b = (byte)(b ^ (byte)65);

                line1 += "0x" + b.ToString("X2");

                // Line to ASCII mapping
                char c = (char)bytes[currentAddress];
                if (char.IsLetterOrDigit(c))
                {
                    line2 += c;
                }
                else
                {
                    line2 += '.';
                }
                line2 += "   ";


                currentAddress++;

                if ((lineCounter++ > 8) | (currentAddress >= endAddress))
                {
                    LogMessage(line1);
                    LogMessage(line2);

                    lineCounter = 0;
                    LogMessage();
                }
            }

            LogMessage();

            return currentAddress;
        }



        internal void Reset()
        {
            cpu.ResetRegs();
        }

        public UInt16 Dissasemble(UInt16 start, UInt16 length, bool showAddress = false, bool allowOutsideLoadedMemory = false)
        {
            S8Instruction s8i;

            UInt16 currentAddress = start;
            UInt16 endAddress = (UInt16)(currentAddress + length);

            // Special - if no length givien, assume 20 instructions
            if (length == 0)
            {
                endAddress = (UInt16)(currentAddress + 20);
                //endAddress = cpu.state.memoryUsed;
            }
            if (!allowOutsideLoadedMemory)
            {
                if (endAddress > cpu.state.memoryUsed)
                {
                    endAddress = cpu.state.memoryUsed;
                }
            }

            while (currentAddress < endAddress)
            {
                int lineAddress = currentAddress;

                byte opcode = bytes[currentAddress++];
                byte param = bytes[currentAddress++];

                s8i = new S8Instruction(opcode, param);                                
                s8i.DecodeInstruction();


                LogMessage(s8i.Instruction2Text(lineAddress, showAddress));


            }
            return currentAddress;

        }

        /*
        internal void PrettyPrintInstruction(S8Instruction s8i, int currentAddress,bool showAddress)
        {
            string sHexAddress = currentAddress.ToString("X3");

            if (s8i.ValidInstruction)
            {
                if (!showAddress)
                    LogMessage("a" + sHexAddress + ":");
            }
            else
            {
                if (!showAddress)
                    LogMessage("m" + sHexAddress + ":");
            }

            if (showAddress)
            {
                string sOpcode = s8i.Opcode.ToString("X2");
                string sParam = s8i.Param.ToString("X2");
                Console.Write("A[" + sHexAddress + "] | I[" + sOpcode + " " + sParam + "] ");
            }


            if (s8i.ValidInstruction)
            {
                LogMessage(s8i.DecodedInstruction);
            }
            else
            {
                string data = ".DATA 0x" + s8i.Opcode.ToString("X2");
                LogMessage(data + " ; " + s8i.ErrorMessage);
            }
        }
        */

        internal UInt16 SetPC(UInt16 start, bool allowOutsideLoadedMemory = false)
        {
            if (start > 0xFFF)
                start = 0;

            if (!allowOutsideLoadedMemory)
            {
                if (start > cpu.state.memoryUsed)
                {
                    start = 0;
                }
            }
            cpu.state.pc = start;

            return cpu.state.pc;
        }

        internal void SetInput(byte[] inputBuffer)
        {
            cpu.state.stdin = inputBuffer;
        }

        internal void SetInput(string v)
        {
            string s = ConvertHex2Asii(v);

            cpu.state.stdin = new byte[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                cpu.state.stdin[i] = (byte)s[i];
            }
        }
        internal string GetOutput()
        {
            return cpu.state.stdout;
        }

        internal void ClearOutput()
        {
            cpu.state.stdout = "";
        }

        public void SetMaxTicks(int Ticks)
        {
            cpu.SetMaxTicks(Ticks);
        }

        public int GetMaxTicks()
        {
            return cpu.state.maxTicks;
        }


   
        public UInt16 Run(bool ShowOppgulp = true, bool verbose = false, bool showaddress=false)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            cpu.Run(verbose, showaddress);

            stopwatch.Stop();
            var elapsed_time = stopwatch.ElapsedMilliseconds;

            LogMessage("Elapsed time " + elapsed_time + "ms, Ticks " + cpu.state.tick);
            if (ShowOppgulp)
                Oppgulp();
            return cpu.state.pc;
        }


        public int RunUntil(int pc)
        {
            cpu.RunUntil(pc);
            Oppgulp();
            return cpu.state.pc;
        }

        public UInt16 Step(int numStep)
        {
            do
            {
                cpu.Step();
                numStep--;
            } while ((numStep > 0) & (!cpu.state.crashed));

            Oppgulp();
            Regs();
            return cpu.state.pc;
        }


        private string ConvertHex2Asii(string hex)
        {
            int i = 0;
            int value = 0;
            string prefixedHex;

            string returnText = "";

            try
            {
                while (i < hex.Length)
                {
                    prefixedHex = "0x" + hex[i] + hex[i + 1];
                    value = Convert.ToInt32(prefixedHex, 16);
                    i = i + 2;

                    returnText += (char)value;
                }
            }
            catch (Exception)
            {

                //
            }

            return returnText;

        }


        private void Oppgulp()
        {
            if (cpu.state.stdout.Length > 0)
            {
                LogMessage(">HEX: " + cpu.state.stdout);


                LogMessage(">ASCII: " + ConvertHex2Asii(cpu.state.stdout));


                cpu.state.stdout = "";

            }
        }

        public void Regs()
        {
            LogMessage("PC [" + cpu.state.pc.ToString("X3") + "]");
            LogMessage("FLAG [" + cpu.state.flag + "]");
            for (int i = 0; i < 16; i++)
            {
                Console.Write("R" + i + "[" + cpu.state.regs[i].ToString("X2") + "] ");
            }
            LogMessage();
        }
    }
}
