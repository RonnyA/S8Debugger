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
        bool _isEnabled;

        public bool Enabled
        {
            get
            {
                return _isEnabled;
            }
            set
            {
                _isEnabled = value;
            }
        }

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
            this.CanFocus = true;
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
            if (_isEnabled)
            {

                // TextView is zero based.
                // Bug (?) the Move() API doesnt seem to work
                base.Move(1, sourceCodeLine-1);

                // Move to line (set top line in the text editor.
                // This means that the topmost line is always the code that is executing during stepping
                base.ScrollTo(sourceCodeLine-1);

                // Focus!!
                base.SetFocus();

            }
        }
    }

}