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

    public class AsmView : ListView
    {
        // List variables for ASM and REGS
        private List<string> _asms = new List<string>();
        S8CommandParser _parser;

        bool _isEnabled;


        public bool Enabled {
            get {
                return _isEnabled;
            }
            set {
                _isEnabled = value;
            }
        }

        public AsmView(S8CommandParser parser)
        {
            
            _parser = parser;

            this.SetSource(_asms);
            this.AllowsMultipleSelection = false;
            this.AllowsMarking = false;            

            parser.s8d.cpu.CpuStepHandler += delegate (object sender, CpuStepInfo cpustep)
            {
                refreshUI(cpustep.pc);
            };
        }

        public void refreshUI(UInt16 pc)
        {
            if (_isEnabled)
            {
                if ((pc == 0) | (_asms.Count == 0))
                {
                    _asms.Clear();
                    _asms.AddRange(_parser.s8d.DissasembleToList(0, 0xFFF, _parser.showAddress, false));
                }

                var lines = this.Height;

                try
                {
                    this.SelectedItem = pc / 2;
                }
                catch (Exception)
                {

                    // i am confused, I lost track of my lines..
                }
            }            
        }
    }
}
