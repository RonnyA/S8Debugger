using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

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

        #region Source Code

        string _sourceCode = string.Empty;
        public void SetSourceCode(string sourceCode)
        {
            _sourceCode = sourceCode;

            SourceFileMD5 = CreateMD5(_sourceCode);
        }

        public string CreateMD5(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";

            // Use input string to calculate MD5 hash
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("X2"));
                }
                return sb.ToString();
            }
        }

        public string  GetSourceCode()
        {
            return _sourceCode;
        }

        public string SourceFileMD5 { get; set; }
        public string SourceFileName { get; set; }

        #endregion

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
            s8d.MessageHandler += HandleEventMessage;


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
                            try
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
                            catch (Exception ex)
                            {
                                LogMessage("Assembly failed, ex = " + ex.ToString());
                            }

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
                            try
                            {
                                var s8prog = s8a?.AssembleFile(asmFile, s8File);
                                if (s8prog is null)
                                {
                                    LogMessage("Assembly FAILED!!");
                                }
                                else
                                {
                                    LogMessage("Assembled file OK, size = " + s8prog.exe.Length);
                                };
                                currentAddress = s8d.cpu.state.pc;
                            }
                            catch (Exception ex)
                            {

                                LogMessage("Assembly failed, ex = " + ex.ToString());
                            }
                            
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
                            try
                            {
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
                                currentAddress = s8d.cpu.state.pc;
                            }
                            catch (Exception ex)
                            {
                                LogMessage("Assembly failed, ex = " + ex.ToString());
                            }

                        }
                        break;


                    case "LOAD":
                        if (cmd.Length > 1)
                        {
                            string fname = cmd[1];

                            if (File.Exists(fname))
                            {
                                if (fname.Contains(".slede8", StringComparison.InvariantCultureIgnoreCase))
                                {

                                    SourceFileName = fname;

                                    LogMessage($"Loading source code file: '{fname}'");
                                    var src = File.ReadAllText(fname);
                                    if (src.Length > 0)
                                    {
                                        SetSourceCode(src);
                                        AssembleSourceCode();
                                    }
                                    else
                                    {
                                        LogMessage($"Failed! File '{fname}' is empty ...?");
                                    }
                                }
                                else
                                {
                                    LogMessage($"Loading image file: '{fname}'");

                                    SourceFileName = string.Empty;
                                    SetSourceCode(string.Empty);
                                    SourceFileMD5 = string.Empty;

                                    if (!s8d.Init(fname))
                                    {
                                        LogMessage("Failed to load image");
                                    }
                                    currentAddress = 0;
                                }
                            }
                            else
                            {
                                LogMessage($"File doesn't exists: '{fname}'");
                            }

                        }
                        break;

                    case "INPUT":
                    case "FØDE":
                        if (cmd.Length > 1)
                        {
                            s8d.SetInputFromHexString(cmd[1]);
                        }
                        else
                        {
                            LogMessage("Missing input string");
                        }
                        break;

                    case "PC": // set PC
                        if (cmd.Length > 1)
                        {
                            var newaddress = parseVal(cmd[1]);
                            currentAddress = s8d.SetPC(newaddress);
                        }
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

                    case "VERBOSE":
                        s8d.cpu.VerboseMode = true;
                        break;
                    case "TERSE":
                        s8d.cpu.VerboseMode = false;
                        break;

                    case "R":
                    case "RUN":
                        if (GetSourceCode().Length >0)
                        {
                            AssembleSourceCode();
                        }
                        currentAddress = s8d.Run();
                        break;

                    case "RUN!": // Run without assembling source code
                        currentAddress = s8d.Run();
                        break;

                    case "RUNV":
                        s8d.cpu.VerboseMode = true;
                        currentAddress = s8d.Run();
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
                        int numSteps = 1;
                        if (cmd.Length > 1)
                        {
                            numSteps = parseVal(cmd[1]);
                        }
                        currentAddress = s8d.Step(numSteps);
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

        private void AssembleSourceCode()
        {
            string src = GetSourceCode();

            S8Assembler.Target result = s8a.AssembleSourceCode(src);

            if (result is null)
            {
                LogMessage("Assembly FAILED!!");
            }
            else
            {
                //s8d = new S8Dissasembler();
                s8d.InitFromMemory(result);
            };

            currentAddress = s8d.cpu.state.pc;
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

                if ((valStr.StartsWith("0x")) | (valStr.StartsWith("0X")))
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



        public string GetCommands()
        {
            string cmds =
                @"
D - Dissassemble [start] [length]
M - Memory Dump  [start] [length]
Limits itself to inside loaded image

D!- Dissassemble [start] [length]
M!- Memory Dump  [start] [length]
Enables access to memory ourside loaded image

INPUT - SET INPUT hexhexhex
PC    - SET pc = xxx
RUN   - Run program from 0 (Will compile source code first)
RUN!  - Run program from 0 (Will not compile source code)
REGS  - Dump registers
RESET - Reset registers
STEP  - Step PC [steps]
TICKS - Set Max Ticks 0xNN
UNITTEST [filename] - Run unit tests agains [filename]

VERBOSE = Set Verbose output of RUN and STEP
TERSE   = Remove verbose output of RUN and STEP
! = Change showaddress flag
            
A    statement        - Assemble [statement]
ASM  sledefil [s8fil] - Assemble and load into memory. Optionally save to S8 file
ASM! sledefil         - Assemble, dont load into memory

GUI - Enter GUI mode
Q   - Quit";
            return cmds;
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
            LogMessage("REGS  - Dump registers");
            LogMessage("RESET - Reset registers");
            LogMessage("STEP  - Step PC [steps]");
            LogMessage("TICKS - Set Max Ticks 0xNN");
            LogMessage("UNITTEST [filename] - Run unit tests agains [filename]");
            LogMessage("");

            LogMessage("VERBOSE = Set Verbose output of RUN and STEP");
            LogMessage("TERSE   = Remove verbose output of RUN and STEP");
            LogMessage("! = Change showaddress flag");
            LogMessage("");


            LogMessage("");
            LogMessage("A    statement        - Assemble [statement]");
            LogMessage("ASM  sledefil [s8fil] - Assemble and load into memory. Optionally save to S8 file");
            LogMessage("ASM! sledefil         - Assemble, dont load into memory");
            LogMessage("");

            LogMessage("GUI - Enter GUI mode");
            LogMessage("Q   - Quit");
        }

        #endregion Helper Functions
    }

}
