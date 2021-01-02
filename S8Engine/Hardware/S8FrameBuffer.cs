using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if _EXPERIMENTAL_
namespace S8Debugger.Hardware
{    
    public class S8FrameBuffer
    {
        const int MEMORY_SIZE = 65536;

        public event Action<bool> OnVSync;

        public byte[] Memory = new byte[MEMORY_SIZE];

        public byte Read(UInt16 addr)
        {
            return Memory[addr];
        }
        public void Write(UInt16 addr, byte value)
        {
            Memory[addr] = value;
        }

        public void VSync()
        {
            OnVSync?.Invoke(true);
        }
    }
}
#endif