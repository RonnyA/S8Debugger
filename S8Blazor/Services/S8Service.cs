using S8Debugger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace S8Blazor.Services
{
    public class S8Service : IS8Service
    {
        S8CommandParser parser;
        /// <summary>
        /// Ctor
        /// </summary>
        public S8Service()
        {
            parser = new S8CommandParser();
        }

        public Task<bool> Run()
        {
            parser.ParseCommand("RUN");

            return Task.FromResult(true);

        }
    }
}
