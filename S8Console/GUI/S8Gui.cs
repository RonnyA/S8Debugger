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

namespace S8Console.GUI
{
    public class S8Gui
    {

        // Access to all S8 functionality thriugh this object
        static S8CommandParser s8parser;

        // public GUI variables to allow for access to internal variables from events and other places
        static HexView hexLinesView;
        static TextField commandMessage;
        static AsmView asmLinesView;

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
        static void UpdateAsmAndHex()
        {
            // Todo:  Even if you set AllowEdits to false you can change the variables in the UI, but it doesnt update the source data.
            hexLinesView.AllowEdits = false;

            // hexLinesView doesnt detect that source data has changed, force new update through setting the Source prop
            // Doc: https://migueldeicaza.github.io/gui.cs/api/Terminal.Gui/Terminal.Gui.HexView.html

            hexLinesView.Source = s8parser.s8d.MemoryDump();

            asmLinesView.refreshUI(0);
        }



        public void RunGui(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.Default;

            //Connect to event handler for messaging
            s8parser.MessageHandler += S8parser_Message;
            s8parser.s8d.cpu.CpuStepHandler += Cpu_CpuStepHandler;
            // Start MainApp
            MainApp();

            // loop (allows switching between "main" windows and withut exiting the app
            while (running != null)
            {
                running.Invoke();
            }
            Application.Shutdown();

            // Disconnect from Message events (clean up and be nice)
            s8parser.s8d.cpu.CpuStepHandler -= Cpu_CpuStepHandler;
            s8parser.MessageHandler -= S8parser_Message;
        }


        /// <summary>
        /// This event is fired after every program execution line
        /// Use this to update UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cpu_CpuStepHandler(object sender, CpuStepInfo e)
        {
            
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
                Y = 0,
                Width = Dim.Percent(75),
                Height = Dim.Percent(60),
            };

            asmLinesView = new AsmView(s8parser)
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


            #region Debug Controller

            var frmDebugController = new DebugController(s8parser)
            {
                X = Pos.Right(asmFrameView),
                Y = 0,
                Width = Dim.Fill(),
                Height = 5
            };

            win.Add(frmDebugController);

            #endregion Debug Controlelr

            #region Register windows

            var regFrameView = new RegsView(s8parser.s8d.cpu)
            {
                X = Pos.Right(asmFrameView),
                Y = Pos.Bottom(frmDebugController),
                Width = Dim.Fill(),
                Height = 15
                //Height = Dim.Fill()
            };

            win.Add(regFrameView);
            #endregion Register windows

            #region Output view

            var outFrameView = new FrameView("Output")
            {
                X = Pos.Right(asmFrameView),
                Y = Pos.Bottom(regFrameView),
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            var outHexView = new OutputView(s8parser.s8d.cpu)
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
            };
            outFrameView.Add(outHexView);
            win.Add(outFrameView);
            #endregion Output view

            #region regOutputView
            var commandFrameView = new FrameView("Debug commands")
            {
                X = 0,
                Y = Pos.Bottom(hexFrameView),
                Width = asmFrameView.Width,
                //Height = Dim.Fill()
                Height = 3
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


            UpdateAsmAndHex();

            Application.Run();

        }


        static private void ExecuteButton_Clicked()
        {
            string cmd = commandMessage.Text.ToString();
            s8parser.ParseCommand(cmd);
            commandMessage.Text = "";

            UpdateAsmAndHex();
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
                    UpdateAsmAndHex();
                }
                else if (s8parser.s8d.Init(currentFileName))
                {
                    UpdateAsmAndHex();

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
