using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace S8Debugger
{
    // https://github.com/PSTNorge/slede8/blob/main/src/assembler.ts
    public class S8Assembler
    {
        /// <summary>
        /// Assemble a .slede file to .s8 file
        /// </summary>
        /// <param name="sledeFile"></param>
        /// <returns>Compiled memory</returns>
        public byte[] AssembleFile(string sledeFile)
        {

            /// TODO implement real assembler :-D
            /// 
            byte[] s8 = new byte[2];
            s8[0] = 0;
            s8[1] = 0;
            return s8;
        }
    }
}
