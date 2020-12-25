using S8Debugger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace S8Console.GUI
{
    public class DebugController : FrameView
    {
        public DebugController(S8CommandParser parser)
        {
            int y = 0;

            var stepButton = new Button("Step", true)
            {
                X = 0,
                Y = y++,
                Width = Dim.Fill(),
                Height = Dim.Fill()                
            };

            //stepButton.ColorScheme =  Colors.TopLevel; ;

            var runButton = new Button("RUN", true)
            {
                X = 0,
                Y = y++,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            var resetButton = new Button("RESET", true)
            {
                X = 0,
                Y = y++,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };


            this.Add(stepButton);
            this.Add(runButton);
            this.Add(resetButton);

            stepButton.Clicked += delegate ()
            {
                parser.ParseCommand("STEP");
            };

            runButton.Clicked += delegate ()
            {
                parser.ParseCommand("RUN");
            };


            resetButton.Clicked += delegate ()
            {
                parser.ParseCommand("RESET");
            };

        }

        private void RunButton_Clicked()
        {
            throw new NotImplementedException();
        }
    }
}
