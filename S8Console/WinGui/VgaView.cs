using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using S8Debugger;
using SDL2;

namespace S8Console.WinGui
{
    class VgaView
    {
        /* Screen Variables */
        int ScreenWidth = 0;
        int ScreenHeight = 0;
        IntPtr window = IntPtr.Zero;
        IntPtr renderer = IntPtr.Zero;
        S8CommandParser parser;


        public bool InitVga(S8CommandParser Parser)
        {
            parser = Parser;


            // Read screen resolution from VIC chip
            ScreenWidth = 256;
            ScreenHeight = 256;

            return InitSDL("Slede8 VGA");
        }

        /// <summary>
        /// Initialize SDL Subsystem
        /// </summary>
        /// <param name="windowTitle"></param>
        /// <returns></returns>
        public bool InitSDL(string windowTitle)
        {
            if ((ScreenWidth == 0) || (ScreenHeight == 0))
            {
                Console.WriteLine("Unable to read Screensize");
                return false;
            }

            // When running C# applications under the Visual Studio debugger, native code that names threads with the 0x406D1388 exception will silently exit.
            // To prevent this exception from being thrown by SDL, add this line before your SDL_Init call:
            SDL.SDL_SetHint(SDL.SDL_HINT_WINDOWS_DISABLE_THREAD_NAMING, "1");


            if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO) < 0)
            {
                Console.WriteLine("Unable to initialzie SDL. Error {0}", SDL.SDL_GetError());
                return false;
            }


            window = SDL.SDL_CreateWindow(windowTitle, SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED, ScreenWidth * 2, ScreenHeight * 2, SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
            if (window == IntPtr.Zero)
            {
                Console.WriteLine("Unable to create window. Error {0}", SDL.SDL_GetError());
                SDL.SDL_Quit();
                return false;
            }


            renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);

            if (renderer == IntPtr.Zero)
            {
                Console.WriteLine("SDL could not create a valid renderer.");
                return false;
            }

            return true;
        }

        public void CleanupSDL()
        {
            if (renderer != IntPtr.Zero)
                SDL.SDL_DestroyRenderer(renderer);

            if (window != IntPtr.Zero)
                SDL.SDL_DestroyWindow(window);

            SDL.SDL_Quit();
        }

        public void RunUI()
        {
            bool running = true;
            
            parser.s8d.cpu.ResetRegs();
            while (running)
            {
                parser.s8d.SetMaxTicks(100000); // Will also reset tick counter so we dont have an unexpected death
                parser.s8d.cpu.RunSteps(50000); // Run 50.000 cycles
                running = GameLoop();
            }
        }
        public bool GameLoop()
        {
            bool running = true;
            SDL.SDL_Event sdlEvent;


            // Check for and handle SDL events (Keyboard, Mouse, Window, ++)
            while (SDL.SDL_PollEvent(out sdlEvent) != 0)
            {
                //Console.WriteLine($"Type {sdlEvent.type}  {sdlEvent.ToString()}");

                if (sdlEvent.type == SDL.SDL_EventType.SDL_QUIT)
                {
                    running = false;
                }

                /* Debug keyboard 
                else if(sdlEvent.type == SDL.SDL_EventType.SDL_MOUSEMOTION)
                {
                    byte c197 = dbg.machine.cpu.Memory.ReadMemory(197);
                    byte c203 = dbg.machine.cpu.Memory.ReadMemory(203);

                    byte c653 = dbg.machine.cpu.Memory.ReadMemory(653);
                    byte c654 = dbg.machine.cpu.Memory.ReadMemory(654);

                    Console.WriteLine($"197 {c197:X2},  203 {c203:X2}  |  653 {c653:X2}, 654 {c654:X2} ");
                }
                */
                else if (sdlEvent.type == SDL.SDL_EventType.SDL_KEYDOWN)
                {


                    if (sdlEvent.key.repeat == 0)
                    {
                        Console.WriteLine($"DOWN: {sdlEvent.key.keysym.scancode.ToString()}  " + (char)sdlEvent.key.keysym.sym);


                        if (sdlEvent.key.keysym.scancode == SDL.SDL_Scancode.SDL_SCANCODE_PAUSE)
                        {
                            Console.WriteLine("RESET");
                            //dbg.machine.Reset();
                        }

                    }
                    else if (sdlEvent.type == SDL.SDL_EventType.SDL_KEYUP)
                    {
                        //Console.WriteLine($"UP: {sdlEvent.key.keysym.scancode.ToString()}  {sdlEvent.key.keysym.mod.ToString()} ");

                        if (sdlEvent.key.repeat == 0)
                        {
                            //Console.WriteLine($"Mapped to {key.ToString()}");
                            //dbg.machine.keyboard.KeyReleased(port, key);
                        }

                    }


                }
            }


            return running;
        }

        public void UpdateDisplay()
        {

            byte[] display = parser.s8d.cpu.HWDisplay.Memory;

            IntPtr sdlSurface, sdlTexture = IntPtr.Zero;

            var displayHandle = GCHandle.Alloc(display, GCHandleType.Pinned); // / ABGR in 8 bit order left to right            

            sdlSurface = SDL.SDL_CreateRGBSurfaceFrom(displayHandle.AddrOfPinnedObject(), ScreenWidth, ScreenHeight, 32, ScreenWidth * 4, 0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000);
            sdlTexture = SDL.SDL_CreateTextureFromSurface(renderer, sdlSurface);

            displayHandle.Free();
            GC.Collect();


            SDL.SDL_RenderClear(renderer);
            SDL.SDL_RenderCopy(renderer, sdlTexture, IntPtr.Zero, IntPtr.Zero);
            SDL.SDL_RenderPresent(renderer);


            // Clean up Surface object
            if (sdlSurface != IntPtr.Zero)
                SDL.SDL_FreeSurface(sdlSurface);
            sdlSurface = IntPtr.Zero;

            // Clean up Texture object
            if (sdlTexture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(sdlTexture);
            sdlTexture = IntPtr.Zero;

        }

    }
}
