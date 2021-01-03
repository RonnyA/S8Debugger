using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

#if _EXPERIMENTAL_
namespace S8Debugger.Hardware
{
    public class S8FrameBuffer
    {
        const int MEMORY_SIZE = 262144;

        public event Action<bool> OnVSync;

        public byte[] Memory = new byte[MEMORY_SIZE];

        Color[] defaultVGAPalette = VGAPalette.GetDefaultVGAPalette();

        //CTOR
      


        public byte Read(UInt16 addr)
        {
            return Memory[addr];
        }
        public void Write(UInt16 addr, byte value)
        {
            int memoryAddr = addr * 4;

            Color c = defaultVGAPalette[value];

            Memory[memoryAddr] = c.R;
            Memory[memoryAddr+1] = c.G;
            Memory[memoryAddr+2] = c.B;
            Memory[memoryAddr+3] = c.A;
        }

        public void VSync()
        {
            OnVSync?.Invoke(true);
        }
    }
}
#endif