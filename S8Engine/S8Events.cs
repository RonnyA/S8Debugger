﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace S8Debugger
{
    public class LogMessageEventArgs : EventArgs
    {
        public string LogMessage { get; set; }
        public DateTime LogTimeStamp { get; set; }

        public LogMessageEventArgs()
        {
            LogTimeStamp = DateTime.Now;
        }


        
    }
    
    public class CpuStepInfo : EventArgs
    {
        //yield { pc, flag, regs, memory, stdout, inputPtr };

        public UInt16 pc { get; set; }

    }
}
