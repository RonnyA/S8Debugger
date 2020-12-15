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
        byte[] bytes = null;
        S8CPU cpu = new S8CPU();

        public bool Init(string fname, bool force = false)
        {

            if (!File.Exists(fname)) return false;

            var executable = File.ReadAllBytes(fname);
            bytes = cpu.Load(executable, force);

            if (bytes is null) return false;

            Console.WriteLine("Loaded image " + cpu.state.memoryUsed + " bytes");
            return true;
        }

        internal bool InitFromMemory(byte[] s8prog)
        {
            bytes = cpu.Load(s8prog, true);

            if (bytes is null) return false;
            Console.WriteLine("Loaded image " + cpu.state.memoryUsed + " bytes");
            return true;
        }

        public int  MemoryDump(int start, int length, bool showAddress = false)
        {            
            int currentAddress = start;
            int endAddress = currentAddress + length;

            // Special - if no length givien, assume 8 instructions
            if (length == 0)
            {
                endAddress = currentAddress + 8;
                //endAddress = cpu.state.memoryUsed;
            }

            if (endAddress > cpu.state.memoryUsed)
            {
                endAddress = cpu.state.memoryUsed;
            }

            

            int lineCounter = 0;

            string line1 = "";
            string line2 = "";

            while (currentAddress < endAddress)
            {
                
                if (lineCounter == 0)
                {
                    line1 = ".DATA ";
                    line2 = ";" + currentAddress.ToString("X4") + ":  ";
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
                    Console.WriteLine(line1);
                    Console.WriteLine(line2);

                    lineCounter = 0;
                    Console.WriteLine();
                }                
            }

            Console.WriteLine();

            return currentAddress;
        }

        

        internal void Reset()
        {
            cpu.ResetRegs();
        }

        public int Dissasemble(int start, int length, bool showAddress = false)
        {
            S8Instruction s8i;

            int currentAddress = start;
            int endAddress = currentAddress + length;

            // Special - if no length givien, assume 8 instructions
            if (length == 0)
            {
                endAddress = currentAddress + 8;
                //endAddress = cpu.state.memoryUsed;
            }

            if (endAddress > cpu.state.memoryUsed)
            {
                endAddress = cpu.state.memoryUsed;
            }

            while (currentAddress < endAddress)
            {
                string sHexAddress = currentAddress.ToString("X4");

                byte opcode = bytes[currentAddress++];
                byte param = bytes[currentAddress++];
                
                s8i = new S8Instruction(opcode, param);
                s8i.DecodeInstruction();

                if (showAddress)
                {
                    string sOpcode = opcode.ToString("X2");
                    string sParam = param.ToString("X2");
                    Console.Write("A["+sHexAddress + "] | I["+sOpcode + " " + sParam + "] ");
                }
                
                if (s8i.ValidInstruction)
                {
                    Console.WriteLine(s8i.DecodedInstruction);
                }
                else
                {
                    string data = ".DATA 0x" + opcode.ToString("X2");
                    Console.WriteLine(data + " ; " + s8i.ErrorMessage);
                }
            }
            return currentAddress;

        }

        internal int SetPC(int start)
        {
            if (start > cpu.state.memoryUsed)
            {
                start = 0;
            }
            cpu.state.pc = start;

            return cpu.state.pc;
        }

        internal void SetInput(string v)
        {
            string s = ConvertHex2Asii(v);

            cpu.state.stdin = new byte[s.Length];
            for (int i=0;i<s.Length;i++)
            {
                cpu.state.stdin[i] = (byte)s[i];
            }            
        }


        public void SetMaxTicks(int Ticks)
        {
            cpu.SetMaxTicks(Ticks);
        }

        public int GetMaxTicks()
        {
            return cpu.state.maxTicks;
        }

        public int Run(bool verbose = false)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start(); 
            
            cpu.Run(verbose);

            stopwatch.Stop();
            var elapsed_time = stopwatch.ElapsedMilliseconds;

            Console.WriteLine("Elapsed time " + elapsed_time + "ms, Ticks " + cpu.state.tick);
            Oppgulp();
            return cpu.state.pc;
        }


        public int RunUntil(int pc)
        {
            cpu.RunUntil(pc);
            Oppgulp();
            return cpu.state.pc;
        }

        public int Step(int numStep)
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
                Console.WriteLine(">HEX: " + cpu.state.stdout);


                Console.WriteLine(">ASCII: " + ConvertHex2Asii(cpu.state.stdout));


                cpu.state.stdout = "";

            }
        }

        public void Regs()
        {
            Console.WriteLine("PC [" + cpu.state.pc.ToString("X4") +"]");
            Console.WriteLine("FLAG [" + cpu.state.flag +"]");            
            for (int i=0; i<16;i++)
            {
                Console.Write("R" + i + "[" + cpu.state.regs[i].ToString("X2") + "] ");
            }
            Console.WriteLine();
        }
    }
}
