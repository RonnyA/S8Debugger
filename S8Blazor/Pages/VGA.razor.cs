using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.JSInterop.WebAssembly;
using S8Blazor.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Timers;


// Read more here: https://developer.mozilla.org/en-US/docs/Web/API/ImageData/data
// https://www.davideguida.com/blazor-gamedev-part-2-canvas-initialization/

namespace S8Blazor.Pages
{
    public partial class VGAModel : ComponentBase
    {
        System.Timers.Timer vgaLoop;

        public bool Running = false;

        private bool Vsync = true;

        [Inject]
        public IS8Service s8service { get; set; }

        [Inject]
        protected HttpClient Http { get; set; }
        [Inject]
        protected IJSRuntime JSRuntime { get; set; }

        public VGAModel()
        {
            vgaLoop = new System.Timers.Timer(20);
            vgaLoop.Elapsed += vgaLoop_Elapsed;        
        }

        private async void vgaLoop_Elapsed(object sender, ElapsedEventArgs e)
        {

            if (Running)
            {
                s8service.Parser.s8d.SetMaxTicks(2000000);
                if (!s8service.Parser.s8d.cpu.Step(100000))
                {
                    Running = false;                    
                }

                Paint();                                    
            }
        }

        protected async override void OnAfterRender(bool firstRender)
        {
            await JSRuntime.InvokeAsync<bool>("InitCanvas");
            base.OnAfterRender(firstRender);
        }
        protected async override Task OnInitializedAsync()
        {
#if _EXPERIMENTAL_
            s8service.Parser.s8d.cpu.state.HWDisplay.OnVSync += HWDisplay_OnVSync;
#endif
            vgaLoop.Start();
            
            await base.OnInitializedAsync();
        }
        private void HWDisplay_OnVSync(bool obj)
        {
            Debug.WriteLine("VSync -" + DateTime.Now.ToUniversalTime());
            // Paint to the canvas
            Vsync = true;            
        }

        public void Dispose()
        {
            // Clean up hooked event handlers
#if _EXPERIMENTAL_
            s8service.Parser.s8d.cpu.state.HWDisplay.OnVSync -= HWDisplay_OnVSync;
#endif
        }

        public async void Paint()
        {
            if (!Vsync) return;

#if _EXPERIMENTAL_

            var screen = s8service.Parser.s8d.cpu.state.HWDisplay.Memory;

            //Allocate memory
            var gch = GCHandle.Alloc(screen, GCHandleType.Pinned);
            var pinned = gch.AddrOfPinnedObject();
            var mono = JSRuntime as WebAssemblyJSRuntime;
            mono.InvokeUnmarshalled<IntPtr, string>("PaintCanvas", pinned);
            gch.Free();
#endif

            Vsync = false;
        }
    }
}
