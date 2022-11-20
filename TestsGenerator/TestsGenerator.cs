using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.IO;

namespace TestsGenerator
{
    static public class TestsGenerator
    {
        private static TransformManyBlock<string, string> CreateReadDirectoryBlock() =>
            new TransformManyBlock<string, string>(path =>
                {
                    if (!Directory.Exists(path))
                    {
                        throw new ArgumentException("Directory doesn't exists");
                    }

                    return Directory.EnumerateFiles(path);
                }
            );

        private static TransformBlock<string, string> CreateReadFileBlock(int maxParallelism) =>
            new TransformBlock<string, string>( async path =>
                    {
                        if (!File.Exists(path))
                        {
                            throw new ArgumentException("File doesn't exist");
                        }

                        var file = File.OpenText(path);
                        return await file.ReadToEndAsync();
                    }
                , new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = maxParallelism }
                );

        /*private static TransformManyBlock<string, string> CreateSplitClassesBlock() =>
            new TransformManyBlock<string, string>( code => 
            {
                return default;    
            } );*/

        private static TransformBlock<string, string> CreateTestGeneratorBlock(int maxParallelism) =>
            new TransformBlock<string, string>( GenerateTests,new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = maxParallelism });

        private static ActionBlock<TestsFile> CreateWriterBlock(string path, int maxParallelism) =>
            new ActionBlock<TestsFile>(testFile =>
           {
               if (!Directory.Exists(path))
               {
                   throw new ArgumentException("Directory doesn't exists");
               }

               var file = File.CreateText($"{path}/{testFile.Filename}");
               return file.WriteAsync(testFile.Content);
           }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = maxParallelism });

        private static string GenerateTests(string classCode)
        {
            return default;
        }
    }
}
