using System;
using System.IO;

namespace S8Debugger
{
    class Program
    {
        static void Main(string[] args)
        {

            string defaultS8File = @"s8.s8";

            S8Dissasembler s8d = new S8Dissasembler();

            s8d.Init(defaultS8File);


            bool showAddress = true;
            int currentAddress = 0;


            Console.WriteLine("Velkommen til Slede8 debugger");
            Console.WriteLine("H => HELP");

            bool debugging = true;

            while (debugging)
            {
                int start = 0;
                int length = 0;

                if (showAddress)
                {
                    Console.Write("S8#[");
                }
                else
                {
                    Console.Write("s8 [");
                }
                Console.Write(currentAddress.ToString("X3") + "] ");
                string input = Console.ReadLine();

                try
                {
                    var cmd = input.Split(" ");
                    if (cmd.Length > 1)
                    {
                        start = parseValue(cmd[1]);
                    }
                    else
                    {
                        start = currentAddress;
                    }

                    if (cmd.Length > 2)
                    {
                        length = parseValue(cmd[2]);
                    }

                    switch (cmd[0].ToUpper())
                    {

                        case "A": // ASssemble statement
                            if (cmd.Length > 1)
                            {
                                var result = AsmStatement(input.Substring(2));

                                Console.WriteLine($"A return {result.Length} bytes");
                                Console.Write(" >");

                                foreach(byte b in result)
                                {
                                    Console.Write($"0x{b:X2} ");
                                }
                                Console.WriteLine();
                            }
                            break;

                        case "ASM!": //Assemble file (for validation only)
                            if (cmd.Length > 1)
                            {
                                string asmFile = cmd[1];
                                string s8File = "";
                                if (cmd.Length>2)
                                {
                                    s8File = cmd[2];
                                }

                                var s8prog = AsmFile(asmFile, s8File);
                                if (s8prog is null)
                                {
                                    Console.WriteLine("Assembly FAILED!!");
                                }
                                else
                                {
                                    Console.WriteLine("Assembled file OK, size = " + s8prog.Length);
                                }
                                ;
                            }
                            break;

                        case "ASM": //Assemble and LOAD file to memory
                            if (cmd.Length > 1)
                            {

                                string asmFile = cmd[1];
                                string s8File = "";
                                if (cmd.Length > 2)
                                {
                                    s8File = cmd[2];
                                }

                                var s8prog = AsmFile(asmFile, s8File);

                                if (s8prog is null)
                                {
                                    Console.WriteLine("Assembly FAILED!!");
                                }
                                else
                                {
                                    s8d = new S8Dissasembler();
                                    s8d.InitFromMemory(s8prog);                                    
                                };
                            }
                            break;


                        case "LOAD":
                            if (cmd.Length > 1)
                            {
                                if (File.Exists(cmd[1]))
                                {
                                    s8d = new S8Dissasembler();
                                    if (!s8d.Init(cmd[1]))
                                    {
                                        Console.WriteLine("Failed to load image");
                                    }
                                    currentAddress = 0;
                                }
                                else
                                {
                                    Console.WriteLine("Unknown S8 file " + cmd[1]);
                                }

                            }
                            break;

                        case "INPUT":
                        case "FØDE":
                            if (cmd.Length > 1)
                            {
                                s8d.SetInput(cmd[1]);
                            }
                            else
                            {
                                Console.WriteLine("Missing input string");
                            }
                            break;

                        case "PC": // set PC
                            currentAddress = s8d.SetPC(start);
                            break;

                        case "PC!": // set PC
                            currentAddress = s8d.SetPC(start, true);
                            break;


                        case "UNITTEST":
                            if (cmd.Length > 1)
                            {
                                S8UnitTest s8unit = new S8UnitTest();
                                currentAddress = s8unit.RunUnitTest(s8d, cmd[1]);
                            }
                            break;

                        case "R":
                        case "RUN":
                            currentAddress = s8d.Run(true, false,showAddress);
                            break;
                        case "RUNV":
                            currentAddress = s8d.Run(true, true, showAddress);
                            break;

                        case "REGS":
                            s8d.Regs();
                            break;

                        case "RESET":
                            s8d.Reset();
                            currentAddress = 0;
                            break;


                        case "+":
                        case "S":
                        case "STEP":
                            if (start > 0)
                            {
                                currentAddress = s8d.Step(start);
                            }
                            else
                            {
                                currentAddress = s8d.Step(1);
                            }

                            break;
                        case "SETMAXTICKS":
                        case "TICKS":
                            if (start > 0)
                            {
                                s8d.SetMaxTicks(start);
                            }

                            Console.WriteLine("MaxTicks is set to " + s8d.GetMaxTicks().ToString());
                            break;
                        case ":":
                            hard();
                            break;
                        case "":
                            currentAddress = s8d.Dissasemble(currentAddress, 2, showAddress);
                            break;
                        case "D":
                            currentAddress = s8d.Dissasemble(start, length, showAddress);
                            break;
                        case "D!":
                            currentAddress = s8d.Dissasemble(start, length, showAddress, true);
                            break;
                        case "M":
                            currentAddress = s8d.MemoryDump(start, length, showAddress);
                            break;
                        case "M!":
                            currentAddress = s8d.MemoryDump(start, length, showAddress, true);
                            break;

                        case "H":
                        case "HELP":
                        case "?":
                            PrintHelp();
                            break;
                        case "Q":
                        case "QUIT":
                        case "DIE":
                            debugging = false;
                            break;
                        case "!":
                            showAddress = !showAddress;
                            if (showAddress)
                            {
                                Console.WriteLine("Show memory address enabled");
                            }
                            else
                            {
                                Console.WriteLine("Show memory address disabled");
                            }
                            break;
                        default:
                            if (input.Length > 0)
                            {
                                Console.WriteLine("Unknown command [" + input + "]. Type H for help");
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {

                    Console.WriteLine("Sleden kræsjet: " +ex.ToString());
                }

            }



        }

        private static void SaveToS8File(byte[] s8prog, string s8outputfile)
        {
            var MAGIC = new byte[] { 0x2E, 0x53, 0x4C, 0x45, 0x44, 0x45, 0x38 };
            var stream = File.Open(s8outputfile, FileMode.Create);

            stream.Write(MAGIC, 0, MAGIC.Length);
            stream.Write(s8prog, 0, s8prog.Length);
            stream.Close();

        }

        private static byte[] AsmFile(string sledeFile, string s8file="")
        {

            if (!File.Exists(sledeFile))
            {
                Console.WriteLine("Can't find SLEDE8 file " + sledeFile);
            }
            S8Assembler s8 = new S8Assembler();
            return s8.AssembleFile(sledeFile, s8file);
        }

        private static byte[] AsmStatement(string statement)
        {

            S8Assembler s8 = new S8Assembler();
            return s8.AssembleStatement(statement);
        }

        static int parseValue(string v)
        {
            try
            {
                return int.Parse(v, System.Globalization.NumberStyles.HexNumber);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        static void hard()
        {
            // implement hard coded test function

        }




        static void PrintHelp()
        {
            Console.WriteLine("D - Dissassemble [start] [length]");
            Console.WriteLine("M - Memory Dump  [start] [length]");
            Console.WriteLine("Limits itself to inside loaded image");
            Console.WriteLine();

            Console.WriteLine("D!- Dissassemble [start] [length]");
            Console.WriteLine("M!- Memory Dump  [start] [length]");
            Console.WriteLine("Enables access to memory ourside loaded image"); ;
            Console.WriteLine("");

            Console.WriteLine("");
            Console.WriteLine("INPUT - SET INPUT hexhexhex");
            Console.WriteLine("PC    - SET pc = xxx");
            Console.WriteLine("RUN   - Run program from 0");
            Console.WriteLine("RUNV  - Run program from 0 with verbose output");
            Console.WriteLine("REGS  - Dump registers");
            Console.WriteLine("RESET - Reset registers");
            Console.WriteLine("STEP  - Step PC [steps]");
            Console.WriteLine("TICKS - Set Max Ticks 0xNN");
            Console.WriteLine("UNITTEST [filename] - Run unit tests agains [filename]");
            Console.WriteLine("! = Change showaddress flag");
            Console.WriteLine("");


            Console.WriteLine("");
            Console.WriteLine("A    statement        - Assemble [statement]");
            Console.WriteLine("ASM  sledefil [s8fil] - Assemble and load into memory. Optionally save to S8 file");
            Console.WriteLine("ASM! sledefil         - Assemble, dont load into memory");
            Console.WriteLine("");


            Console.WriteLine("Q = Quit");
        }
    }
}
