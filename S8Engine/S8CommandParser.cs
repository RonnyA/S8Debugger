using System;
using System;
using System.IO;

namespace S8Debugger
{
    public class S8CommandParser
    {

        #region public variables
        public S8Dissasembler s8d { get; set; }
        public S8Assembler s8a { get; set; }
        public S8UnitTest s8unit { get; set; }


        public UInt16 currentAddress { get; set; }
        public bool showAddress { get; set; }
        #endregion

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

        private void LogMessage(LogMessageEventArgs e)
        {
            OnLogMessage(e);
        }
        #endregion

        #region CTOR
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="dissasembler"></param>
        public S8CommandParser()
        {
            // create helper components and hook up event handlers
            s8d = new S8Dissasembler();
            s8d.Message += HandleEventMessage;

            s8a = new S8Assembler();
            s8a.Message += HandleEventMessage;


            s8unit = new S8UnitTest();
            s8unit.Message += HandleEventMessage;

            currentAddress = 0;
            showAddress = true; //default is to show memory and instruction memory address
        }



        private void HandleEventMessage(object sender, LogMessageEventArgs e)
        {
            LogMessage(e);
        }
        #endregion

        #region Parse
        public void ParseCommand(string inputCommand)
        {
            UInt16 start = 0;
            UInt16 length = 0;
            try
            {
                var cmd = inputCommand.Split(" ");
                if (cmd.Length > 1)
                {
                    start = parseVal(cmd[1]);
                }
                else
                {
                    start = currentAddress;
                }

                if (cmd.Length > 2)
                {
                    length = parseVal(cmd[2]);
                }

                switch (cmd[0].ToUpper())
                {

                    case "!":
                        showAddress = !showAddress;
                        if (showAddress)
                        {
                            LogMessage("Show memory address enabled");
                        }
                        else
                        {
                            LogMessage("Show memory address disabled");
                        }
                        break;

                    case "H":
                    case "HELP":
                    case "?":
                        PrintHelp();
                        break;

                    case "A": // ASssemble statement
                        if (cmd.Length > 1)
                        {

                            var result = s8a?.AssembleStatement(inputCommand.Substring(2));

                            LogMessage($"A return {result.Length} bytes");
                            Console.Write(" >");

                            foreach (byte b in result)
                            {
                                Console.Write($"0x{b:X2} ");
                            }
                            LogMessage();
                        }
                        break;

                    case "ASM!": //Assemble file (for validation only)
                        if (cmd.Length > 1)
                        {
                            string asmFile = cmd[1];
                            string s8File = "";
                            if (cmd.Length > 2)
                            {
                                s8File = cmd[2];
                            }

                            var s8prog = s8a?.AssembleFile(asmFile, s8File);
                            if (s8prog is null)
                            {
                                LogMessage("Assembly FAILED!!");
                            }
                            else
                            {
                                LogMessage("Assembled file OK, size = " + s8prog.Length);
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

                            var s8prog = s8a?.AssembleFile(asmFile, s8File);
                            if (s8prog is null)
                            {
                                LogMessage("Assembly FAILED!!");
                            }
                            else
                            {
                                //s8d = new S8Dissasembler();
                                s8d.InitFromMemory(s8prog);
                            };
                        }
                        break;


                    case "LOAD":
                        if (cmd.Length > 1)
                        {
                            if (File.Exists(cmd[1]))
                            {
                                //s8d = new S8Dissasembler();
                                if (!s8d.Init(cmd[1]))
                                {
                                    LogMessage("Failed to load image");
                                }
                                currentAddress = 0;
                            }
                            else
                            {
                                LogMessage("Unknown S8 file " + cmd[1]);
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
                            LogMessage("Missing input string");
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
                            currentAddress = s8unit.RunUnitTest(s8d, cmd[1]);
                        }
                        break;

                    case "R":
                    case "RUN":
                        currentAddress = s8d.Run(true, false, showAddress);
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
                            s8d.SetMaxTicks(parseIntVal(cmd[1]));
                        }

                        LogMessage("MaxTicks is set to " + s8d.GetMaxTicks().ToString());
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


                    default:
                        if (inputCommand.Length > 0)
                        {
                            LogMessage("Unknown command [" + inputCommand + "]. Type H for help");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {

                LogMessage("Sleden kræsjet: " + ex.ToString());
            }

        }



        #endregion Parse

        #region Helper Functions


        UInt16 parseVal(string valStr)
        {
            return ((UInt16)parseIntVal(valStr));
        }

        int parseIntVal(string valStr)
        {
            try
            {

                if (valStr.StartsWith("0x"))
                {
                    return int.Parse(valStr.Substring(2), System.Globalization.NumberStyles.HexNumber);
                }
                return int.Parse(valStr);
            }
            catch (Exception)
            {
                return 0;
            }
        }


        public void PrintHelp()
        {
            LogMessage("D - Dissassemble [start] [length]");
            LogMessage("M - Memory Dump  [start] [length]");
            LogMessage("Limits itself to inside loaded image");
            LogMessage();

            LogMessage("D!- Dissassemble [start] [length]");
            LogMessage("M!- Memory Dump  [start] [length]");
            LogMessage("Enables access to memory ourside loaded image"); ;
            LogMessage("");

            LogMessage("");
            LogMessage("INPUT - SET INPUT hexhexhex");
            LogMessage("PC    - SET pc = xxx");
            LogMessage("RUN   - Run program from 0");
            LogMessage("RUNV  - Run program from 0 with verbose output");
            LogMessage("REGS  - Dump registers");
            LogMessage("RESET - Reset registers");
            LogMessage("STEP  - Step PC [steps]");
            LogMessage("TICKS - Set Max Ticks 0xNN");
            LogMessage("UNITTEST [filename] - Run unit tests agains [filename]");
            LogMessage("! = Change showaddress flag");
            LogMessage("");


            LogMessage("");
            LogMessage("A    statement        - Assemble [statement]");
            LogMessage("ASM  sledefil [s8fil] - Assemble and load into memory. Optionally save to S8 file");
            LogMessage("ASM! sledefil         - Assemble, dont load into memory");
            LogMessage("");


            LogMessage("Q = Quit");
        }

        #endregion Helper Functions
    }

}
