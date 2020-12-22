﻿using System;
using S8Debugger;

namespace S8Console
{
    class Program
    {

        static void Main(string[] args)
        {

            string defaultS8File = @"s8.s8";

            S8CommandParser parser = new S8CommandParser();
            parser.Message += Parser_Message;

            parser.s8d.Init(defaultS8File);


            Console.WriteLine("Velkommen til Slede8 debugger");
            Console.WriteLine("H => HELP");

            bool debugging = true;            

            while (debugging)
            {
                if (parser.showAddress)
                {
                    Console.Write("S8#[");
                }
                else
                {
                    Console.Write("s8 [");
                }
                Console.Write(parser.currentAddress.ToString("X3") + "] ");


                string input = Console.ReadLine().ToUpper();

                switch (input)
                {
                    case "Q":
                    case "QUIT":
                    case "DIE":
                        debugging = false;
                        break;
                   
                    default:
                        parser.ParseCommand(input);
                        break;
                }


            }
        }

        //private static void SaveToS8File(byte[] s8prog, string s8outputfile)
        //{
        //    var MAGIC = new byte[] { 0x2E, 0x53, 0x4C, 0x45, 0x44, 0x45, 0x38 };
        //    var stream = File.Open(s8outputfile, FileMode.Create);

        //    stream.Write(MAGIC, 0, MAGIC.Length);
        //    stream.Write(s8prog, 0, s8prog.Length);
        //    stream.Close();

        //}

        private static void Parser_Message(object sender, LogMessageEventArgs e)
        {
            Console.WriteLine(e.LogMessage);
        }

        static void PrintHelp()
        {

        }

    }
}