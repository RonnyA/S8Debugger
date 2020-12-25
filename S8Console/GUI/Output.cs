using S8Debugger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace S8Console.GUI
{
    public class OutputView : HexView
    {
        public OutputView(S8CPU cpu)
        {
            this.AllowEdits = false;
            cpu.CpuStepHandler += delegate (object sender, CpuStepInfo cpustep)
            {            
                this.Source = cpu.state.outputStream;
            };

        }
    }
}

