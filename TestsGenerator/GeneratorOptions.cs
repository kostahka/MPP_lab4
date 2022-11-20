using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestsGenerator
{
    public class GeneratorOptions
    {
        public int MaxRead { get; set; } = 5;

        public int MaxGenerate { get; set; } = 5;

        public int MaxWrite { get; set; } = 5;

        public string SourceDirectory { get; set; }

        public string DestinationDirectory { get; set; }
    }
}
