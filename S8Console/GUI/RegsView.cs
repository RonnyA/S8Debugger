using S8Debugger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace S8Console.GUI
{
    public class RegsView : FrameView
    {
        public RegsView(S8CPU cpu)
        {

            this.Title = "Registers";

            int ypos=0;

            var pcl = new Label(new Rect(1, ypos++, 20, 1),   "PC   [000]");
            var flagl = new Label(new Rect(1, ypos++, 20, 1), "Flag [FALSE]");
            ypos++;

            var regLine1 = new Label(new Rect(1, ypos++, 20, 1), "r0 [00]   r8  [00]");
            var regLine2 = new Label(new Rect(1, ypos++, 20, 1), "r1 [00]   r9  [00]");
            var regLine3 = new Label(new Rect(1, ypos++, 20, 1), "r2 [00]   r10 [00]");
            var regLine4 = new Label(new Rect(1, ypos++, 20, 1), "r3 [00]   r11 [00]");
            var regLine5 = new Label(new Rect(1, ypos++, 20, 1), "r4 [00]   r12 [00]");
            var regLine6 = new Label(new Rect(1, ypos++, 20, 1), "r5 [00]   r13 [00]");
            var regLine7 = new Label(new Rect(1, ypos++, 20, 1), "r6 [00]   r14 [00]");
            var regLine8 = new Label(new Rect(1, ypos++, 20, 1), "r7 [00]   r15 [00]");

            ypos++;
            var ticksLine = new Label(new Rect(1, ypos++, 20, 1), $"Ticks [{cpu.state.tick}/{cpu.state.maxTicks}]");

            cpu.CpuStepHandler += delegate (object sender, CpuStepInfo cpustep)
            {
                pcl.Text = $"PC   [{cpu.state.pc:X3}]";
                flagl.Text = $"FLAG [{cpu.state.flag}]";

                regLine1.Text = $"R0 [{cpu.state.regs[0]:X2}] R8  [{cpu.state.regs[8]:X2}]";
                regLine2.Text = $"R1 [{cpu.state.regs[1]:X2}] R9  [{cpu.state.regs[9]:X2}]";
                regLine3.Text = $"R2 [{cpu.state.regs[2]:X2}] R10 [{cpu.state.regs[10]:X2}]";
                regLine4.Text = $"R3 [{cpu.state.regs[3]:X2}] R11 [{cpu.state.regs[11]:X2}]";
                regLine5.Text = $"R4 [{cpu.state.regs[4]:X2}] R12 [{cpu.state.regs[12]:X2}]";
                regLine6.Text = $"R5 [{cpu.state.regs[5]:X2}] R13 [{cpu.state.regs[13]:X2}]";
                regLine7.Text = $"R6 [{cpu.state.regs[6]:X2}] R14 [{cpu.state.regs[14]:X2}]";
                regLine8.Text = $"R7 [{cpu.state.regs[7]:X2}] R15 [{cpu.state.regs[15]:X2}]";
                ticksLine.Text = $"Ticks [{cpu.state.tick}/{cpu.state.maxTicks}]";
            };

            this.Add(pcl);
            this.Add(flagl);

            this.Add(regLine1);
            this.Add(regLine2);
            this.Add(regLine3);
            this.Add(regLine4);
            this.Add(regLine5);
            this.Add(regLine6);
            this.Add(regLine7);
            this.Add(regLine8);

            this.Add(ticksLine);


        }

    }
}

