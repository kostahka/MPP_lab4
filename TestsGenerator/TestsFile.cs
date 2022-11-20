using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestsGenerator
{
    public class TestsFile
    {
        public string Filename { get; }

        public string Content { get; }

        public TestsFile(string filename, string content)
        {
            Filename = filename;
            Content = content;
        }
    }
}
