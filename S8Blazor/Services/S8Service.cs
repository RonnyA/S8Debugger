using S8Debugger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace S8Blazor.Services
{
    public class S8Service : IS8Service
    {
        List<string> output = new List<string>();
        S8CommandParser parser;
        /// <summary>
        /// Ctor
        /// </summary>
        public S8Service()
        {
            parser = new S8CommandParser();
            parser.MessageHandler += (object sender, LogMessageEventArgs e) =>
            {
                Console.WriteLine(e.LogMessage);
                output.Add(e.LogMessage);
            };
        }
        
        public S8CommandParser Parser { get { return parser; }}

        public void ClearOutput()
        {
            output.Clear();
        }

        public string GetOutput()
        {
            string result = string.Empty;

            foreach (string l in output)
            {

                result += l + "\r\n";
            }
            return result;
        }


        private void ExecuteCommandFunction(string command, bool isInSourceMode=false)
        {
            bool runCommand = true;
            // If we are in source code mode, we need to check if we need a new recompile
            if (isInSourceMode)
            {

                //string src = srcLinesView.SourceCode;
                string src = string.Empty;

                // if the src code in the source view is different, then recompile
                if (src.Length > 0)
                {

                    var hashCode = src.GetHashCode();
                    if ((hashCode != parser.SourceFileHash) | (command == "RUN!"))
                    {
                        // Source code in editor different from stored version. Compile!!

                        try
                        {
                            parser.SetSourceCode(src);
                            var result = parser.s8a.AssembleSourceCode(src);

                            if (result is not null)
                            {
                                parser.s8d.InitFromMemory(result);
                            };

                        }
                        catch (S8AssemblerException s8ex)
                        {
                            runCommand = false;
                            string error = s8ex.Message + "\r\nLine: " + s8ex.SourceCodeLine.ToString();

                            /*
                            MessageBox.ErrorQuery(50, 7, "Compile Error", error, "Cancel");

                            srcLinesView.SetLineFocus(s8ex.SourceCodeLine);
                            */
                        }
                        catch (Exception ex)
                        {
                            runCommand = false;
                            /*
                            MessageBox.ErrorQuery(50, 7, "Compile Error", ex.ToString(), "Cancel");
                            */
                        }
                    }
                }
            }
            if (runCommand)
                parser.ParseCommand(command);
        }

    }
}
