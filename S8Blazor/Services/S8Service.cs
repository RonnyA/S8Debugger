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
                output.Add(e.LogMessage);
            };
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

        public bool Run()
        {
            output.Clear();

            parser.ParseCommand("RUN");

            return true;

        }


        public void  SetInput(string input)
        {
            parser.s8d.SetInputFromHexString(input);
        }

        public void SetSourceCode(string src)
        {
            parser.SetSourceCode(src);
        }
    }
}
