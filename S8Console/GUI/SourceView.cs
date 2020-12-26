using S8Debugger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace S8Console.GUI
{
    public class SourceView : TextView
    {
        S8CommandParser parser;


        public string SourceCode {
            get {
                return this.Text.ToString();
            }
            set {
                this.Text = value;
            }
        }

        public SourceView(S8CommandParser parser)
        {
            this.parser = parser;
            this.ReadOnly = false;

            SourceCode = parser.GetSourceCode();

            parser.s8d.cpu.CpuStepHandler += (object sender, CpuStepInfo e) =>
            {
                SetCpuStepLine(e);
            };
        }

        private void SetCpuStepLine(CpuStepInfo e)
        {
            // map hex PC to line no

            UInt16 pc = e.pc;

            if (parser.s8d.PDB is null) return;

            foreach (S8Assembler.DebugInfo dbg in parser.s8d.PDB)
            {
                if (dbg.address == e.pc)
                {
                    SetLineFocus(dbg.info.lineNumber);
                    break;
                }
            }

        }

        internal void SetLineFocus(int sourceCodeLine)
        {
            base.SetFocus();
            base.ScrollTo(sourceCodeLine);
        }
    }

}