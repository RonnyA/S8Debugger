using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace S8Debugger
{
    public class S8UnitTest
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
        /// <summary>
        /// Run one or more unit tests
        /// </summary>
        /// <param name="s8d">Existing S8Dissasembler object</param>
        /// <param name="unitTestFile">The name of the file containing the unit tests</param>
        /// <returns></returns>
        public UInt16 RunUnitTest(S8Dissasembler s8d,string unitTestFile)
        {
            byte[] bInput;
            int lineCounter = 0;
            int errCnt = 0;
            UInt16 currentaddress = 0;
            bool verbose = false;

            if (s8d is null) return 0;

            if (!File.Exists(unitTestFile))
            {
                LogMessage("Unknown file " + unitTestFile);
                return 0;
            }

            var allLines = File.ReadAllLines(unitTestFile);

            LogMessage("Read unit test file " + allLines.Length + " lines.");

            foreach (string currentLine in allLines)
            {
                var actualLine = currentLine.Trim();
                lineCounter++;

                if (actualLine.Length == 0)
                    continue;

                if (actualLine[0] == '#')
                {
                    LogMessage(actualLine);
                    continue;
                }

                if (actualLine[0] == '!')
                {
                    // COmmands
                    var cmds = actualLine.Split(" ");
                    if (cmds.Length < 2)
                        continue;
                    string param = string.Empty;

                    var command = cmds[1].ToUpper().Trim();
                    if (cmds.Length > 2)
                    {
                        param = cmds[2].ToUpper().Trim();
                    }

                    switch (command)
                    {
                        case "VERBOSE":
                            verbose = true;
                            if (param.Contains("FALSE"))
                            {
                                verbose = false;
                            }
                            
                            break;
                        case "TICKS":
                        case "MAXTICKS":
                            int newTicks = 0;
                            if (int.TryParse(param, out newTicks))
                            {
                                s8d.SetMaxTicks(newTicks);
                                LogMessage("[" + lineCounter.ToString() + "] MAXTICKS " + newTicks);
                            }

                            break;
                        case "LOAD":
                            LogMessage("[" + lineCounter.ToString() + "] LOAD FILE " + param);
                            s8d.Init(param);
                            break;
                        default:
                            LogMessage("[" + lineCounter.ToString() + "] Unknown command " + command);
                            break;
                    }

                    continue;
                }

                var input = actualLine.Split(";");
                if (input.Length < 2)
                {
                    LogMessage("[" + lineCounter.ToString() + "] Invalid line format");
                    continue;
                }
                input[0] = input[0].Trim();
                input[1] = input[1].Trim().ToUpper();

                int inputLen = input[0].Length;
                int hexInputLen = inputLen / 2;

                if (hexInputLen > 1000)
                {
                    LogMessage("FAILED: Input length > 1000");
                    return 0;
                }
                LogMessage("[" + lineCounter.ToString() + "] Run started");

                bInput = new byte[hexInputLen];

                for (int i = 0; i < hexInputLen; i++)
                {
                    var inStr = input[0].Substring((i * 2), 2);
                    bInput[i] = byte.Parse(inStr, System.Globalization.NumberStyles.HexNumber);

                }

                s8d.SetInput(bInput);

                currentaddress = s8d.Run(false, verbose);
                string stdout = s8d.GetOutput();

                if (input[1].Equals(stdout))
                {
                    LogMessage("[" + lineCounter.ToString() + "] Run successfull");
                }
                else
                {
                    errCnt++;
                    LogMessage("[" + lineCounter.ToString() + "] FAILED! Output differs");
                    LogMessage("[" + lineCounter.ToString() + "]   OUTPUT   = " + stdout);
                    LogMessage("[" + lineCounter.ToString() + "]   EXPECTED = " + input[1]);
                    //return 0;
                }
            }

            if (errCnt > 0)
            {
                LogMessage("Unit test failed with " + errCnt + " errors!");
            }
            return currentaddress;
        }
    }
}
