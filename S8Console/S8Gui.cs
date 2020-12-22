using S8Debugger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Terminal.Gui;


// GUI implementation is using Gui.cs
// Check out https://github.com/migueldeicaza/gui.cs
//
// Good intro read: https://itnext.io/terminal-console-user-interface-in-net-core-4e978f1225b

namespace S8Console
{
    public class S8Gui
    {

        // Access to all S8 functionality thriugh this object
        static S8CommandParser s8parser;

        // public GUI variables to allow for access to internal variables from events and other places
        static HexView hexLinesView;
        static TextField commandMessage;

        // List variables for ASM and REGS
        static private readonly List<string> _asms = new List<string>();
        static private readonly List<string> _regs = new List<string>();

        // List variable for log
        static private readonly List<string> _log = new List<string>();


        // Current loaded filename
        static string currentFileName = "";

        public static Action running = MainApp;

        public S8Gui(S8CommandParser parser)
        {
            s8parser = parser;

        }

        private void S8parser_Message(object sender, LogMessageEventArgs e)
        {
            while (_log.Count > 20)
            {
                _log.RemoveAt(0);
            }
            _log.Add(e.LogTimeStamp.ToShortTimeString() + " - " + e.LogMessage);
        }

        /// <summary>
        /// Update dissasm and hex view after a new file has been loaded
        /// </summary>
        static void UpdateAll()
        {
            _asms.Clear();
            _asms.AddRange(s8parser.s8d.DissasembleToList(0, 0xFFF, s8parser.showAddress, false));


            // ToDo - find a way to update from Cpu.CpuState
            // Add eventing when code runs
            _regs.Clear();
            _regs.Add($"PC   [000]");
            _regs.Add("FLAG [FALSE]");
            for (int i = 0; i < 16; i++)
            {
                _regs.Add($"R{i}   [00]");
            }

            // Todo:  Even if you set AllowEdits to false you can change the variables in the UI, but it doesnt update the source data.
            hexLinesView.AllowEdits = false;

            // hexLinesView doesnt detect that source data has changed, force new update through setting the Source prop
            // Doc: https://migueldeicaza.github.io/gui.cs/api/Terminal.Gui/Terminal.Gui.HexView.html

            hexLinesView.Source = s8parser.s8d.MemoryDump();

        }



        public void RunGui(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.Default;

            //Connect to event handler for messaging
            s8parser.Message += S8parser_Message;

            // Start MainApp
            MainApp();

            // loop (allows switching between "main" windows and withut exiting the app
            while (running != null)
            {
                running.Invoke();
            }
            Application.Shutdown();

            // Disconnect from Message events (clean up and be nice)
            s8parser.Message -= S8parser_Message;
        }


        static void MainApp()
        {
            Application.Init();
            var top = Application.Top;


            var win = new Window("S8 Debugger")
            {
                X = 0,
                Y = 1, // Leave one row for the toplevel menu

                // By using Dim.Fill(), it will automatically resize without manual intervention
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            var menu = new MenuBar(new MenuBarItem[] {
                new MenuBarItem("_File", new MenuItem[]{
                    //new MenuItem ("_New", "Creates new file", NewFile),
                    new MenuItem("_Open", "Open file", Open),
                    new MenuItem("Show _log", "", () =>  { running = ShowLog; Application.RequestStop (); }),
                    new MenuItem("_ShowHex", "", () =>  { running = ShowHex; Application.RequestStop (); }),
                    new MenuItem("_Quit", "", () => { running = null; top.Running = false; Application.RequestStop(); } )
                }), // end of file menu
            
                new MenuBarItem("_Help", new MenuItem[]{
                    new MenuItem("_About", "", ()
                        => MessageBox.Query(10, 5, "About", "Written by Ronny Hansen\nVersion: 0.0.4", "Ok"))
                }) // end of the help menu
            });




            #region Main ASM window
            var asmFrameView = new FrameView("ASM")
            {
                X = 0,
                Y = 1,
                Width = Dim.Percent(75),
                Height = Dim.Percent(60),
            };

            var asmLinesView = new ListView(_asms)
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
            };

            asmFrameView.Add(asmLinesView);
            win.Add(asmFrameView);
            #endregion Main ASM window

            #region Main Hex View
            var hexFrameView = new FrameView("HEX Memory")
            {
                X = 0,
                Y = Pos.Bottom(asmFrameView),
                Width = Dim.Percent(75),
                Height = Dim.Percent(30),
            };

            hexLinesView = new HexView()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
            };
            hexLinesView.AllowEdits = false;

            hexFrameView.Add(hexLinesView);
            win.Add(hexFrameView);
            #endregion Main ASM window


            #region Register windows
            var regFrameView = new FrameView("Regs")
            {
                X = Pos.Right(asmFrameView),
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            var regList = new ListView(_regs)
            {
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            regFrameView.Add(regList);
            win.Add(regFrameView);
            #endregion Register windows

            #region CommandWindow
            var commandFrameView = new FrameView("Debug commands")
            {
                X = 0,
                Y = Pos.Bottom(hexFrameView),
                Width = asmFrameView.Width,
                //Height = Dim.Fill()
                Height = 4
            };

            commandMessage = new TextField("")
            {
                X = 0,
                Y = 0,
                Width = Dim.Percent(75),
                Height = Dim.Fill()
            };

            var executeButton = new Button("Execute", true)
            {
                X = Pos.Right(commandMessage),
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            executeButton.Clicked += ExecuteButton_Clicked;

            commandFrameView.Add(commandMessage);
            commandFrameView.Add(executeButton);
            win.Add(commandFrameView);
            #endregion CommandWindow




#if _mouse_debug_
            int count = 0;
            var ml = new Label(new Rect(3, 17, 47, 1), "Mouse: ");
            Application.RootMouseEvent += delegate (MouseEvent me) {
                ml.Text = $"Mouse: ({me.X},{me.Y}) - {me.Flags} {count++}";
            };

            win.Add(ml);

#endif
            top.Add(win, menu);
            top.Add(menu);


            UpdateAll();

            Application.Run();

        }


        static private void ExecuteButton_Clicked()
        {
            string cmd = commandMessage.Text.ToString();
            s8parser.ParseCommand(cmd);
            commandMessage.Text = "";

            UpdateAll();
        }

        static void ShowHex()
        {
            //Key.Q = 81
            uint AltMask = 0x80000000;

            var ntop = Application.Top;

            var menu = new MenuBar(new MenuBarItem[] {
            new MenuBarItem ("_File", new MenuItem [] {
                new MenuItem ("_Close", "", () => { running = MainApp; Application.RequestStop (); }),
            }),
            });
            ntop.Add(menu);

            var win = new Window(currentFileName)
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            ntop.Add(win);


            var logLinesView = new HexView(s8parser.s8d.MemoryDump())
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
            };
            win.Add(logLinesView);
            Application.Run(ntop);
        }

        static void ShowLog()
        {
            //Key.Q = 81
            uint AltMask = 0x80000000;

            var ntop = Application.Top;

            var menu = new MenuBar(new MenuBarItem[] {
            new MenuBarItem ("_File", new MenuItem [] {
                new MenuItem ("_Close", "", () => { running = MainApp; Application.RequestStop (); }),
                }),
            });
            ntop.Add(menu);

            var win = new Window("S8 Engine log messages")
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            ntop.Add(win);

            var hex = new ListView(_log)
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            win.Add(hex);
            Application.Run(ntop);
        }


        static void NewFile()
        {
            var ok = new Button("Ok", is_default: true);
            ok.Clicked += () => { Application.RequestStop(); };
            var cancel = new Button("Cancel");
            cancel.Clicked += () => { Application.RequestStop(); };

            var d = new Dialog("New File", 50, 20, ok, cancel);

            Application.Run(d);
        }

        static void Open()
        {
            var d = new OpenDialog("Open", "Open a file") { AllowsMultipleSelection = false };
            Application.Run(d);

            if (!d.Canceled)
            {
                currentFileName = d.FilePath.ToString();

                if ((currentFileName.Contains(".asm")) | (currentFileName.Contains(".slede8")))
                {
                    s8parser.ParseCommand("ASM " + currentFileName);
                    UpdateAll();
                }
                else if (s8parser.s8d.Init(currentFileName))
                {
                    UpdateAll();

                    //MessageBox.Query(50, 7, "Loaded file", d.FilePath, "Ok");                    
                }
                else
                {
                    MessageBox.Query(50, 7, "Failed to load file ", d.FilePath, "Cancel");
                    currentFileName = "";
                }

            }

        }

    }
}
