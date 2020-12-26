using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace S8Debugger
{
    public class S8Dissasembler
    {

        #region Eventing

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
            LogMessage(ea);
        }

        private void LogMessage(LogMessageEventArgs ea)
        {
            OnLogMessage(ea);
        }

        #endregion
        byte[] bytes = new byte[4096];
        public S8CPU cpu;

        public S8Assembler.DebugInfo[] PDB = null;

        /// <summary>
        /// Ctor
        /// </summary>
        public S8Dissasembler()
        {
            cpu = new S8CPU();
            cpu.MessageHandler += Cpu_MessageHandler;
        }

        private void Cpu_MessageHandler(object sender, LogMessageEventArgs e)
        {
            LogMessage(e);
        }

        public bool Init(string fname)
        {
            PDB = null;

            if (!File.Exists(fname)) return false;

            var executable = File.ReadAllBytes(fname);
            bytes = cpu.Load(executable);

            if (bytes is null) return false;

            LogMessage("Loaded image " + cpu.state.memoryUsed + " bytes");
            return true;
        }

        public bool InitFromMemory(S8Assembler.Target s8)
        {
            PDB = s8.pdb;
            return InitExeFromMemory(s8.exe);
        }
        public bool InitExeFromMemory(byte[] s8prog)
        {            
            bytes = cpu.Load(s8prog);

            if (bytes is null) return false;
            LogMessage("Loaded image " + cpu.state.memoryUsed + " bytes");
            return true;
        }


        /// <summary>
        /// Map memory address (most often pc) to source code line.
        /// This info is available in the PDB memory structure if we have it.
        /// </summary>
        /// <param name="pc"></param>
        /// <returns></returns>
        public UInt16 PDB_MapPC2SourceLine(UInt16 pc)
        {
            if (PDB is not null)
            {
                foreach (S8Assembler.DebugInfo dbg in PDB)
                {
                    if (dbg.address == pc)
                    {
                        return dbg.info.lineNumber;
                    }
                }
            }
            return 0;
        }        


        public MemoryStream MemoryDump()
        {
            MemoryStream ms = new MemoryStream(bytes);

            //ms.Write(bytes, 0, bytes.Length);
            return ms;
        }

        public UInt16 MemoryDump(UInt16 start, UInt16 length, bool showAddress = false, bool allowOutsideLoadedMemory = false)
        {
            UInt16 currentAddress = start;
            UInt16 endAddress = (UInt16)(currentAddress + length);

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

            // Dont read outside physical memory
            if (endAddress > bytes.Length)
            {
                endAddress = (UInt16)(bytes.Length - 1);
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

        public List<string> DissasembleToList(UInt16 start, UInt16 length, bool showAddress = false, bool allowOutsideLoadedMemory = false)
        {
            List<string> asms = new List<string>();
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

            // Dont read outside physical memory
            if (endAddress > bytes.Length)
            {
                endAddress = (UInt16)(bytes.Length - 1);
            }


            while (currentAddress < endAddress)
            {
                int lineAddress = currentAddress;

                byte opcode = bytes[currentAddress++];
                byte param = bytes[currentAddress++];

                s8i = new S8Instruction(opcode, param);
                s8i.DecodeInstruction();

                asms.Add(s8i.Instruction2Text(lineAddress, showAddress));

            }

            return asms;
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

            // Dont read outside physical memory
            if (endAddress > bytes.Length)
            {
                endAddress = (UInt16)(bytes.Length - 1);
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


        internal UInt16 SetPC(UInt16 start, bool allowOutsideLoadedMemory = false)
        {
            if (start > 0xFFF)
            {
                LogMessage($"Can't set PC outside MAX memory. Max memory = 0x{cpu.state.memory.Length:X3}");
                return cpu.state.pc;
            }

            if (!allowOutsideLoadedMemory)
            {
                if (start > cpu.state.memoryUsed)
                {
                    LogMessage($"Can't set PC outside loaded memory. MemoryUsed = 0x{cpu.state.memoryUsed:X3}");
                    return cpu.state.pc;
                }
            }
            cpu.state.pc = start;

            return cpu.state.pc;
        }

        public void SetInput(byte[] inputBuffer)
        {
            cpu.state.stdin = inputBuffer;
            cpu.state.inputPtr = 0;
        }

        public void SetInputFromHexString(string v)
        {
            cpu.state.stdin = ConvertHex2ByteArray(v);
            cpu.state.inputPtr = 0;
        }
        internal string GetOutput()
        {
            return Encoding.ASCII.GetString(cpu.state.outputStream.ToArray());
        }

        // Show input as HEX string
        public string GetInput()
        {
            return ConvertByteArray2HexString(cpu.state.stdin);
        }


        public void SetMaxTicks(int Ticks)
        {
            cpu.SetMaxTicks(Ticks);
        }

        public int GetMaxTicks()
        {
            return cpu.state.maxTicks;
        }



        public UInt16 Run()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            cpu.Run();

            stopwatch.Stop();
            var elapsed_time = stopwatch.ElapsedMilliseconds;

            LogMessage("Elapsed time " + elapsed_time + "ms, Ticks " + cpu.state.tick);

            Oppgulp();
            return cpu.state.pc;
        }


        public UInt16 Step(int numStep)
        {

            cpu.Step(numStep);

            Oppgulp();
            Regs();
            return cpu.state.pc;
        }

        private string ConvertByteArray2HexString(byte[] bArray)
        {
            string hexString = string.Empty;
            for (int i = 0; i < bArray.Length; i++)
            {
                hexString += bArray[i].ToString("X2");
            }
            return hexString;

        }

        private byte[] ConvertHex2ByteArray(string hex)
        {
            int i = 0;
            byte value = 0;
            string prefixedHex;

            MemoryStream ms = new MemoryStream();

            try
            {
                while (i < hex.Length)
                {
                    prefixedHex = "0x" + hex[i] + hex[i + 1];
                    value = (byte)Convert.ToInt32(prefixedHex, 16);
                    i = i + 2;

                    ms.WriteByte(value);
                }
            }
            catch (Exception)
            {

                //
            }

            return ms.ToArray();

        }


        private void Oppgulp()
        {
            if (cpu.state.outputStream.Length > 0)
            {
                cpu.state.outputStream.Position = 0;

                var output = cpu.state.outputStream.ToArray();


                LogMessage(">HEX   : " + ConvertByteArray2HexString(output));
                LogMessage(">ASCII : " + Encoding.ASCII.GetString(output));
            }
        }

        public void Regs()
        {
            string regs = string.Empty;

            LogMessage("PC [" + cpu.state.pc.ToString("X3") + "]");
            LogMessage("FLAG [" + cpu.state.flag + "]");
            for (int i = 0; i < 16; i++)
            {
                regs += "R" + i + "[" + cpu.state.regs[i].ToString("X2") + "] ";
            }

            LogMessage(regs);
            LogMessage();
        }
    }
}
