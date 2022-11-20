using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestsGenerator;

namespace GeneratorApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var generatingOptions = new TestsGenerator.GeneratorOptions();
            //Console.WriteLine("Enter source path");
            generatingOptions.SourceDirectory = @"E:\5сем\SPP\MPP_lab4\TestClasses";
            //Console.WriteLine("Enter destination path");
            generatingOptions.DestinationDirectory = @"E:\5сем\SPP\MPP_lab4\GeneratedTests";
            TestsGenerator.TestsGenerator.Generate(generatingOptions).Wait();
        }
    }
}
