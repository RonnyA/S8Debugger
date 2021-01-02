using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace S8Debugger
{
    // https://github.com/PSTNorge/slede8/blob/main/src/assembler.ts
    public class S8Assembler
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
            OnLogMessage(ea);
        }
        #endregion
        public static readonly UInt16 UNDEFINED = 0XFFFF;

        class LabelDefinition
        {
            public String Label;
            public UInt16 Address;
        }

        public class Labels
        {
            List<LabelDefinition> _labels = new List<LabelDefinition>();

            internal UInt16 mapLabelToAddress(string label)
            {
                foreach (LabelDefinition l in _labels)
                {
                    if (l.Label == label)
                    {
                        return l.Address;
                    }
                }
                return (UInt16)UNDEFINED;
            }
            internal void addLabelAndAddress(string label, UInt16 address)
            {
                _labels.Add(new LabelDefinition() { Label = label, Address = address });
            }
        };

        public class InstructionInfo
        {
            public UInt16 lineNumber;
            public UInt16 address;
            public string raw;
        };

        public class SourceMap
        {
            public List<InstructionInfo> instructions = new List<InstructionInfo>();
            public Labels labels = new Labels();
        };

        public class Instruction
        {            
            public int LineNo;
            public string opCode;
            public List<string> args = new List<string>();


            internal string ensureNoArgs()
            {
                if (args.Count > 0)
                    throw new S8AssemblerException(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.expectedNoArguments].Replace("{extra}", $"{this.opCode} ${this.args}"), LineNo);

                return "";
            }

            internal string singleArg()
            {
                if (args.Count != 1)
                    throw new S8AssemblerException(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.expectedOneArgument].Replace("{extra}", $"{this.opCode} ${this.args}"), LineNo);

                return args[0];
            }

            internal string[] twoArguments()
            {
                if (args.Count != 2)
                    throw new S8AssemblerException(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.expectedTwoArguments].Replace("{extra}", $"{this.opCode} ${this.args}"), LineNo);

                return args.ToArray();
            }

        };

        public class DebugInfo
        {
            public UInt16 address;
            public InstructionInfo info;
        };

        public class Target
        {
            public byte[] exe;
            public DebugInfo[] pdb;
        };
        /// <summary>
        /// Assemble a .slede file to .s8 file
        /// </summary>
        /// <param name="sledeFile"></param>
        /// <returns>Compiled memory</returns>
        public Target AssembleFile(string sledeFile, string s8file = "")
        {
            //if (s8file.Length == 0)
            //{
            //    s8file = sledeFile;
            //    s8file.Replace(".asm", "", StringComparison.InvariantCultureIgnoreCase);
            //    s8file= s8file+ ".s8";
            //}

            if (!File.Exists(sledeFile))
            {
                LogMessage("Can't find SLEDE8 file " + sledeFile);
                return null;
            }

            var sledeTekst = File.ReadAllText(sledeFile);
            LogMessage($"Reading SLEDE8 from {sledeFile}, {sledeTekst.Length} characters");

            var result = AssembleSourceCode(sledeTekst);

            if (s8file.Length > 0)
            {
                LogMessage($"Writing S8 file {s8file}, {result.exe.Length} bytes");
                File.WriteAllBytes(s8file, result.exe);

                //TODO: Write PDB info also?
            }

            return result;
        }

        public Target AssembleSourceCode(string sledeTekst)
        {
            LogMessage("Compiling.....");
            var result = assemble(sledeTekst);
            LogMessage($"Compiled OK! {result.pdb.Length} instructions");
           
            return result;
        }

        public enum ERROR_MESSAGE_ID { expectedNoArguments, expectedOneArgument, expectedTwoArguments, unexpectedToken, invalidRegistry, invalidData };


        public static string[] ERROR_MESSAGE = {
            "Forventet ingen argumenter. {extra}",
            "Forventet ett argument. {extra}",
            "Forventet to argumenter. {extra}",
            "Skjønner ikke hva dette er: '{token}'",
            "Ugyldig register: '{reg}'",
            "Ugyldig .DATA format: '{data}'"
        };
        private ushort currentLine; // global variable - current source code line during compilation

        public byte[] AssembleStatement(string statement)
        {
            currentLine = 0;

            var map = Preprosess(statement);

            var instr = tokenize(map.instructions[0].raw);
            instr.LineNo = map.instructions[0].lineNumber;

            var bArray = Translate(instr, map.labels);

            return bArray;
        }
        /// <summary>
        /// "label" | "instruction" | "data" | "comment" | "whitespace" {
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public string classify(string line)
        {
            //line = line.Trim();

            if (line.Length == 0) return "whitespace";

            if (Regex.Match(line, @"^;.*$").Success) return "comment";

            if (Regex.Match(line, @"^[0-9a-zA-ZæøåÆØÅ\-_]+:$").Success) return "label";

            if (Regex.Match(line, @".DATA [x0-9a-fA-F, ]*$").Success) return "data";

            return "instruction";
        }

        public SourceMap Preprosess(string sourceCode)
        {
            SourceMap map = new SourceMap();
            UInt16 address = 0;
            UInt16 lineNo = 1; // First source code line is #1
            var all_lines = sourceCode.Split("\n");
            foreach (var current in all_lines)
            {
                // (prev, current, lineNumber) =>
                // const { instructions, labels } = prev;
                string line = current.Trim();
                switch (classify(line))
                {
                    case "label":
                        map.labels.addLabelAndAddress(line.Substring(0, line.Length - 1), address);
                        //labels[line.slice(0, -1)] = address;
                        //return { instructions, labels };
                        break;
                    case "data":
                        map.instructions.Add(new InstructionInfo() { lineNumber = lineNo, address = address, raw = line });
                        address += (UInt16)tokenize(line).args.Count;
                        break;
                    case "instruction":
                        map.instructions.Add(new InstructionInfo() { lineNumber = lineNo, address = address, raw = line });
                        address += 2;
                        break;
                    default:
                        break;
                }
                lineNo++;
            }

            return map;
        }
        Instruction tokenize(string raw)
        {
            var instr = new Instruction();
            var commentsRemoved = raw.Trim().Split(";")[0];
            var splitSpace = commentsRemoved.Split(" ");
            instr.opCode = splitSpace[0].Trim();

            //splitSpace.AsQueryable().Where((a, index) => a.Length > 0 && index > 1).Join("").Split(",");
            //var merge = splitSpace.AsQueryable().Where((a, index) => a.Length > 0 && index > 1); //.Join("").Split(",");

            string merge = string.Empty;
            for (int i=1;i<splitSpace.Length;i++)
            {
                merge += splitSpace[i];
            }

            var splitComma = merge.Split(",");
            foreach (string split in splitComma)
            {
                var splitTrimmed = split.Trim();
                if (splitTrimmed.Length > 0)
                {
                    instr.args.Add(splitTrimmed);
                }
            }
            

            return instr;
            /*
             * 
                const commentsRemoved = raw.trim().split(";")[0];
                const [opCode, ...rest] = commentsRemoved
                    .split(" ")
                    .map((x) => x.trim())
                    .filter((x) => x.length > 0);
                const args = (rest || [])
                    .join("")
                    .split(",")
                    .map((x) => x.trim())
                    .filter((x) => x.length > 0);
                return { opCode, args };
            */
        }




        public byte[] Translate(Instruction instruction, Labels labels)
        {
            //const { opCode, args } = instruction;


            if (instruction.opCode == ".DATA")
            {
                return TranslateData(instruction);
            }



            string[] aluOps = new string[] {
                "OG",
                "ELLER",
                "XELLER",
                "VSKIFT",
                "HSKIFT",
                "PLUSS",
                "MINUS"
                };

            string[] cmpOps = new string[] { "LIK", "ULIK", "ME", "MEL", "SE", "SEL" };

            UInt16 returnCode = 0x00;

            switch (instruction.opCode)
            {
                case "STOPP":                    
                    returnCode= writeHalt(instruction.ensureNoArgs());
                    break;
                case "SETT":
                    returnCode= writeSet(instruction.twoArguments());
                    break;

                case "FINN":
                    returnCode = writeLocate(instruction.singleArg(), labels);
                    break;
                case "LAST":
                    returnCode = writeLoad(instruction.singleArg());
                    break;

                case "LAGR":
                    returnCode = writeStore(instruction.singleArg());
                    break;
#if _EXPERIMENTAL_
                case "VLAST":
                    returnCode = writeVLoad(instruction.singleArg());
                    break;
                case "VLAGR":
                    returnCode = writeVStore(instruction.singleArg());
                    break;

#endif
                // ALU
                case "OG":
                case "ELLER":
                case "XELLER":
                case "VSKIFT":
                case "HSKIFT":
                case "PLUSS":
                case "MINUS":
                    byte bOps = (byte)Array.IndexOf(aluOps, instruction.opCode);
                    returnCode= writeAlu(bOps, instruction.twoArguments());
                    break;

                // I/O
                case "LES":
                    returnCode = writeRead(instruction.singleArg());
                    break;

                case "SKRIV":
                    returnCode= writeWrite(instruction.singleArg());
                    break;

#if _EXPERIMENTAL_
                case "INN":
                    returnCode = writeIORead(instruction.singleArg());
                    break;

                case "UT":
                    returnCode = writeIOWrite(instruction.singleArg());
                    break;

                case "VSYNK":                    
                    returnCode = writeVSync(instruction.ensureNoArgs());
                    break;                    
#endif

                // CMP
                case "LIK":
                case "ULIK":
                case "ME":
                case "MEL":
                case "SE":
                case "SEL":
                    byte bCode = (byte)Array.IndexOf(cmpOps, instruction.opCode);
                    returnCode = writeCmp(bCode, instruction.twoArguments());
                    break;

                case "HOPP":
                    returnCode = writeJmp(8, instruction.singleArg(), labels);
                    break;

                case "BHOPP":
                    returnCode = writeJmp(9, instruction.singleArg(), labels);
                    break;

                case "TUR":
                    returnCode = writeCall(instruction.singleArg(), labels);
                    break;

                case "RETUR":                    
                    returnCode = writeRet(instruction.ensureNoArgs());
                    break;

                case "NOPE":                    
                    returnCode = writeNop(instruction.ensureNoArgs());
                    break;

                default:
                    throw new S8AssemblerException(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.unexpectedToken].Replace("{token}", $"{instruction.opCode}"), currentLine);                    
            }

            return Uint8Array(returnCode); // will never happen - just put here to make compiler silent
        }


        /// <summary>
        /// Translate the ".DATA" statement to byte array
        /// </summary>
        /// <param name="instruction"></param>
        /// <returns></returns>
        private byte[] TranslateData(Instruction instruction)
        {
            int len = instruction.args.Count;

            // using memory stream instead of array as its easier for dumping string data
            using (MemoryStream ms = new MemoryStream())
            {

                for (int i = 0; i < len; i++)
                {
                    var arg = instruction.args[i];
                    if (arg.Substring(0, 1) == @"'")
                    {
                        // Handle string input
                        string data = arg.Substring(1);
                        int endingPoint = data.IndexOf("'");

                        if (endingPoint < 1)
                            throw new S8AssemblerException(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.invalidData].Replace("{data}", arg), currentLine);
                        data = data.Substring(0, endingPoint);

                        ms.Write(Encoding.ASCII.GetBytes(data));
                    }
                    else
                        ms.WriteByte((byte)getVal(arg));
                }

                return ms.ToArray();
            }
        }

        public Target assemble(string sourceCode)
        {
            byte[] magic = new byte[] { 0x2E, 0x53, 0x4C, 0x45, 0x44, 0x45, 0x38 };

            Target t = new Target();
            var sourceMap = Preprosess(sourceCode);


            using (MemoryStream ms = new MemoryStream())
            {
                // Add magic first
                ms.Write(magic);

                // Then instructions
                foreach (InstructionInfo instr in sourceMap.instructions)
                {
                    
                    currentLine = instr.lineNumber; // Global variable used by execptions

                    var instruction = tokenize(instr.raw);
                    instruction.LineNo = instr.lineNumber; // Used by exceptions inside Instruction

                    ms.Write(Translate(instruction, sourceMap.labels));
                }
                t.exe = ms.ToArray();
            }


            /// Map debug info
            t.pdb = new DebugInfo[sourceMap.instructions.Count];
            for (int i = 0; i < sourceMap.instructions.Count; i++) 
            {
                t.pdb[i] = new DebugInfo();
                t.pdb[i].address = sourceMap.instructions[i].address;
                t.pdb[i].info = sourceMap.instructions[i];
            };                        

            return t;
        }

        /*
        public concat(buffers)
        {
            int  totalLength = buffers.reduce((acc, value) => acc + value.length, 0);

            if (!buffers.length) return new Uint8Array([]);
                const result = new Uint8Array(totalLength);

            int length = 0;
            for (const array of buffers) 
            {
                result.set(array, length);
                length += array.length;
            }

            return result;
        }
        */
        byte[] Uint8Array(UInt16 val)
        {
            byte[] a = new byte[2];
            a[0] = (byte)((val & 0x00FF));
            a[1] = (byte)((val & 0xFF00) >> 8);

            return a;
        }

        UInt16 nibs(byte nib1, byte nib2, byte nib3, byte nib4)
        {
            UInt16 n = (UInt16)(nib1 | (nib2 << 4) | (nib3 << 8) | (nib4 << 12));
            return n;

        }

        UInt16 nibsByte(byte nib1, byte nib2, byte b)
        {
            UInt16 n = (UInt16)(nib1 | (nib2 << 4) | (b << 8));
            return n;
        }

        UInt16 nibVal(UInt16 nib, UInt16 val)
        {
            return (UInt16)(nib | (val << 4));
        }

        UInt16 parseVal(string valStr)
        {
            if ( (valStr.StartsWith("0x")) | (valStr.StartsWith("0X")))
            {
                return (UInt16)int.Parse(valStr.Substring(2), System.Globalization.NumberStyles.HexNumber);
            }
            return (UInt16)int.Parse(valStr);
        }

        bool isVal(string valStr)
        {
            //if (isNaN(+valStr)) return false;
            try
            {
                parseVal(valStr);
                return true;
            }
            catch
            {
                return false;
            }
        }

        UInt16 getVal(string valStr)
        {
            return (UInt16)parseVal(valStr);
        }

        byte getReg(string regStr)
        {
            if (!regStr.StartsWith('r')) 
                throw new S8AssemblerException(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.invalidRegistry].Replace("{reg}", regStr), currentLine);

            byte regNum = (byte)parseVal(regStr.Substring(1));

            if (regNum< 0 || regNum> 15)
                throw new S8AssemblerException(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.invalidRegistry].Replace("{reg}", regStr), currentLine);

            return regNum;
        }

        UInt16 getAddr(string addrStr, Labels labels)
        {
            if (isVal(addrStr))
            {
                return getVal(addrStr);
            }

            UInt16 address = labels.mapLabelToAddress(addrStr);

            if (address == UNDEFINED)
            {
                //throw ERROR_MESSAGE.unexpectedToken(addrStr); //TOFIX
            }

            return address;
        }
        #region Write byte for instructions
        UInt16 writeHalt(string arg)
        {
            return (UInt16)(0);
        }

        // SETT r1, 44
        UInt16 writeSet(string[] args)
        {

            string reg1 = args[0];
            string regOrValue = args[1];            

            byte reg1Num = getReg(reg1);

            if (isVal(regOrValue))
            {
                byte value = (byte)getVal(regOrValue);
                return nibsByte(1, reg1Num, value);
            }
            else
            {
                byte  reg2Num = getReg(regOrValue);
                return nibsByte(2, reg1Num, reg2Num);
            }
        }


        UInt16 writeLocate(string arg, Labels labels)
        {
            UInt16 addr = getAddr(arg, labels);
            return nibVal(3, addr);
        }

        UInt16 writeLoad(string regStr)
        {
            byte regNum = getReg(regStr);
            return nibs(4, 0, regNum, 0);
        }


        UInt16 writeStore(string reg)
        {
            byte regNum = getReg(reg);
            return nibs(4, 1, regNum, 0);
        }

#if _EXPERIMENTAL_
        //VLAST = 4, subcode 2
        UInt16 writeVLoad(string regStr)
        {
            byte regNum = getReg(regStr);
            return nibs(4, 2, regNum, 0);
        }

        //VLAGR = 4, subcode 3
        UInt16 writeVStore(string reg)
        {
            byte regNum = getReg(reg);
            return nibs(4, 3, regNum, 0);
        }
#endif


        UInt16 writeAlu(byte aluOp, string[] args)
        {
            string reg1 = args[0];
            string reg2 = args[1];
            //const [reg1, reg2] = args;

            byte reg1Num = getReg(reg1);
            byte reg2Num = getReg(reg2);
            return nibs(5, aluOp, reg1Num, reg2Num);
        }

        UInt16 writeRead(string arg)
        {
            string reg = arg.Trim();
            byte regNum = getReg(reg);
            return nibs(6, 0, regNum, 0);
        }


        UInt16 writeWrite(string arg)
        {
            string reg = arg.Trim();
            byte regNum = getReg(reg);
            return nibs(6, 1, regNum, 0);
        }
#if _EXPERIMENTAL_
        // IO routines. INN = subcode 2
        UInt16 writeIORead(string arg)
        {
            string reg = arg.Trim();
            byte regNum = getReg(reg);
            return nibs(6, 2, regNum, 0);
        }

        // IO routines. UT = subcode 3
        UInt16 writeIOWrite(string arg)
        {
            string reg = arg.Trim();
            byte regNum = getReg(reg);
            return nibs(6, 3, regNum, 0);
        }

        // IO routines. VGA VSYNC = subcode 4
        UInt16 writeVSync(string arg)
        {                        
            return nibs(6, 4, 0, 0);
        }

#endif
        UInt16 writeCmp(byte cmpOp, string[] args)
        {
            string reg1 = args[0];
            string reg2 = args[1];
            //const [reg1, reg2] = args;

            byte reg1Num = getReg(reg1);
            byte reg2Num = getReg(reg2);
            return nibs(7, cmpOp, reg1Num, reg2Num);
        }

        UInt16 writeJmp(byte jmpOp, string arg, Labels labels)
        {
            return nibVal(jmpOp, getAddr(arg, labels));
        }

        UInt16 writeCall(string arg, Labels labels)
        {
            return nibVal(0xa, getAddr(arg, labels));
        }

        UInt16 writeRet(string arg)
        {
            return (UInt16)0xb;
        }

        UInt16 writeNop(string arg)
        {
            return (UInt16)0xc;
        }

        #endregion 


    }

    public class S8AssemblerException : SystemException
    {
        public int SourceCodeLine { get; set; }
        public S8AssemblerException() : base() { }
        public S8AssemblerException(string message, int sourceLine) : base(message) {
            SourceCodeLine = sourceLine;
        }
    }



}

