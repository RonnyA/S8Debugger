using System;
using S8Console.GUI;
using S8Console.WinGui;
using S8Debugger;

namespace S8Console
{
    class Program
    {
        static bool disableConsoleLogging = false;

        static VgaView vgaView;
        static S8CommandParser parser;

        static void Main(string[] args)
        {

            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Green;

            string defaultS8File = @"s8.s8";

            parser = new S8CommandParser();
            parser.MessageHandler += Parser_Message;

            parser.s8d.Init(defaultS8File);


            parser.s8d.cpu.HWDisplay.OnVSync += HWDisplay_OnVSync;


            Console.WriteLine("Velkommen til Slede8 debugger");
            Console.WriteLine("H => HELP");
            Console.WriteLine("");

            Console.WriteLine("Enter command 'GUI' to enter GUI mode");
            Console.WriteLine("");

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


                string input = Console.ReadLine();

                switch (input.ToUpper())
                {
                    case "Q":
                    case "QUIT":
                    case "DIE":
                        debugging = false;
                        break;

                    case "CLS":
                        Console.Clear();
                        break;

                    case "G":
                    case "GUI":

                        var s8gui = new S8Gui(parser);
                        disableConsoleLogging = true;
                        s8gui.RunGui(null);
                        disableConsoleLogging = false;

                        Console.Clear();
                        Console.BackgroundColor = ConsoleColor.Black;
                        Console.ForegroundColor = ConsoleColor.Green;
                        break;

                    case "W":
                    case "WIN":
                    case "WINRUN":
                        if (vgaView is null)
                        {
                            initVga();
                        }
                        if (vgaView is not null)
                        {
                            vgaView.RunUI();
                            vgaView.CleanupSDL();
                            vgaView = null;
                        }
                        else
                        {
                            Console.WriteLine("Failed to initialize SDL2/WIN");
                        }
                        break;
                    default:
                        parser.ParseCommand(input);
                        break;


                }


            }

            if (vgaView is not null)
            {
                vgaView.CleanupSDL();
                vgaView = null;
            }
        }

        static void initVga()
        {
            if (vgaView is null)
            {
                vgaView = new();
                if (vgaView.InitVga(parser) == false)
                {
                    vgaView = null;
                    return;
                }
            }
        }

        private static void HWDisplay_OnVSync(bool obj)
        {
            if (vgaView is null)
            {
                Console.WriteLine("Detected VSync. This could should be run in a windows using WINRUN");
                return;
            }
            
            vgaView.UpdateDisplay();
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
            if (!disableConsoleLogging)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(e.LogMessage);
                Console.ForegroundColor = ConsoleColor.Green;
            }
        }

        static void PrintHelp()
        {

        }

    }
}
