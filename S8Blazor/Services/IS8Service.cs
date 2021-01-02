using S8Debugger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace S8Blazor.Services
{
    public interface IS8Service
    {
        S8CommandParser Parser { get; }

        string GetOutput();
        void ClearOutput();
    }
}
