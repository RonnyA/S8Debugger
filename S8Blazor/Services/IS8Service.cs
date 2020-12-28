using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace S8Blazor.Services
{
    public interface IS8Service
    {
        bool Run();

        void SetSourceCode(string src);
        void SetInput(string input);
        string GetOutput();

    }
}
