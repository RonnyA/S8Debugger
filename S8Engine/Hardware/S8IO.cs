using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if _EXPERIMENTAL_
namespace S8Debugger.Hardware
{
    public class S8IO
    {
        public void WriteIO(UInt16 port, byte value)
        {
            // Not yet implemented
        }

        public byte  ReadIO(UInt16 port)
        {
            return 0;
        }

    }
}
#endif