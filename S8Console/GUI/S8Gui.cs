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
        static TextField inputField;
        static FrameView asmFrameView;
        static AsmView asmLinesView;
        static FrameView srcFrameView;
        static SourceView srcLinesView;

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

            inputField.Text = s8parser.s8d.GetInput();
            asmLinesView.refreshUI(0);
        }

        static private void SelectSourceWindow()
        {
            string src = s8parser.GetSourceCode();
            if (string.IsNullOrEmpty(src))
            {
                SetViewAsm();
            }
            else
            {
                SetViewSource();
            }
        }

        static void SetViewSource()
        {
            asmFrameView.Visible = false;
            asmFrameView.CanFocus = false;


            srcFrameView.Visible = true;

            srcFrameView.CanFocus = true;
            srcLinesView.CanFocus = true;

            srcLinesView.SetFocus();
        }

        static void SetViewAsm()
        {
            srcFrameView.Visible = false;
            srcFrameView.CanFocus = false;

            asmFrameView.Visible = true;
            asmFrameView.CanFocus = true;

            asmFrameView.SetFocus();
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


            var win = new Window("SLEDE8")
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
                    new MenuItem("_Save", "Save file", Save),
                    new MenuItem("_New file", "", () => { NewFile();  srcLinesView.SourceCode = s8parser.GetSourceCode(); }),
                    new MenuItem("Show _Log", "", () =>  { running = ShowLog; Application.RequestStop (); }),
                    new MenuItem("Show _Hex", "", () =>  { running = ShowHex; Application.RequestStop (); }),
                    new MenuItem("_Quit", "", () => { running = null; top.Running = false; Application.RequestStop(); } )
                }), // end of file menu

                new MenuBarItem("_Window", new MenuItem[]{
                    new MenuItem("_Dissasembled", "", ()
                        => {
                            SetViewAsm();
                            top.BringSubviewToFront(asmFrameView);
                    }),
                    new MenuItem("_Source", "", () => {
                        SetViewSource();
                        top.BringSubviewToFront(srcLinesView);
                    })
                }), // end of Window menu

            new MenuBarItem("_Help", new MenuItem[]{
                    new MenuItem("_Commands", "", Commands),
                    new MenuItem("_About", "", ()
                        => MessageBox.Query(10, 5, "About", "Written by Ronny Hansen\nVersion: 0.1.0", "Ok"))
                }) // end of the help menu
            }); ;



            #region Input (Føde)
            var inputFrameView = new FrameView("Input")
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                //Height = Dim.Fill()
                Height = 3
            };

            inputField = new TextField("")
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            inputField.TextChanged += (NStack.ustring obj) =>
            {
                s8parser.s8d.SetInputFromHexString(obj.ToString());
            };

            inputFrameView.Add(inputField);
            win.Add(inputFrameView);

            #endregion Føde

            #region Main Source window
            srcFrameView = new FrameView("Source")
            {
                X = 0,
                Y = Pos.Bottom(inputFrameView),
                Width = Dim.Percent(75),
                Height = Dim.Percent(50),
            };

            srcLinesView = new SourceView(s8parser)
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
            };

            srcFrameView.Add(srcLinesView);
            win.Add(srcFrameView);

            srcFrameView.Visible = false; // default dont show this, but show dissasembler

            #endregion Main Source window



            #region Main ASM window
            asmFrameView = new FrameView("Dissasembled")
            {
                X = 0,
                Y = Pos.Bottom(inputFrameView),
                Width = Dim.Percent(75),
                Height = Dim.Percent(50),
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

            var frmDebugController = new DebugController(s8parser, "Controller")
            {
                X = Pos.Right(asmFrameView),
                Y = Pos.Bottom(inputFrameView),
                Width = Dim.Fill(),
                Height = 5
            };

            frmDebugController.CommandButton += (string obj) =>
            {
                ExecuteCommandFunction(obj);
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
                Y = Pos.AnchorEnd(3), //   Pos.Bottom(hexFrameView),
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

            SelectSourceWindow();
            UpdateAsmAndHex();

            Application.Run();

        }

        private static void ExecuteCommandFunction(string obj)
        {
            bool runCommand = true;
            // If we are in source code mode, we need to check if we need a new recompile
            if (srcFrameView.Visible)
            {

                string src = srcLinesView.SourceCode;


                // if the src code in the source view is different, then recompile
                if (src.Length > 0)
                {

                    var hashCode = src.GetHashCode();
                    if ((hashCode != s8parser.SourceFileHash) | (obj == "RUN!"))
                    {
                        // Source code in editor different from stored version. Compile!!

                        try
                        {
                            s8parser.SetSourceCode(src);
                            var result = s8parser.s8a.AssembleSourceCode(src);

                            if (result is not null)
                            {
                                s8parser.s8d.InitFromMemory(result);
                            };

                        }
                        catch (S8AssemblerException s8ex)
                        {
                            runCommand = false;
                            string error = s8ex.Message + "\r\nLine: " + s8ex.SourceCodeLine.ToString();

                            MessageBox.ErrorQuery(50, 7, "Compile Error", error, "Cancel");

                            srcLinesView.SetLineFocus(s8ex.SourceCodeLine);

                        }
                        catch (Exception ex)
                        {
                            runCommand = false;
                            MessageBox.ErrorQuery(50, 7, "Compile Error", ex.ToString(), "Cancel");
                        }
                    }
                }
            }
            if (runCommand)
                s8parser.ParseCommand(obj);
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
            ok.Clicked += () =>
            {
                s8parser.SetSourceCode("");
                Application.RequestStop();
            };
            var cancel = new Button("Cancel");
            cancel.Clicked += () => { Application.RequestStop(); };

            var d = new Dialog("New File", 50, 20, ok, cancel);

            Application.Run(d);


        }

        static void Commands()
        {
            string strCommands = s8parser.GetCommands();

            MessageBox.Query(50, 20, $"SLEDE8 Commands", strCommands, "OK");
        }

        static void Save()
        {

            var d = new SaveDialog("Save", "Save to file");
            Application.Run(d);

            if (!d.Canceled)
            {
                currentFileName = d.FilePath.ToString();

                if (File.Exists(currentFileName))
                {
                    var result = MessageBox.Query(50, 7, "File exists. Overwrite ? ", currentFileName, "OK", "Cancel");
                    if (result != 0) return;
                }


                string sourceCode = s8parser.GetSourceCode();
                using (StreamWriter sw = new StreamWriter(currentFileName))
                {
                    sw.Write(sourceCode);

                    sw.Flush();                    
                    sw.Close();
                }

                MessageBox.Query(50, 7, $"Saved file. {sourceCode.Length} bytes." , currentFileName, "OK");

            }
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
                    SetViewSource();

                    srcLinesView.SourceCode = File.ReadAllText(currentFileName);
                    s8parser.ParseCommand("ASM " + currentFileName);
                    UpdateAsmAndHex();
                }
                else if (s8parser.s8d.Init(currentFileName))
                {
                    SetViewAsm();
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
