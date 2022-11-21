using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using TestsGenerator;

namespace TestsGeneratorTests
{
    [TestClass]
    public class GeneratorTests
    {
        private const string sourceCode = @"
        using System;

        namespace SrcFiles
        {
            public class Class1
            {
                public void Method1()
                {

                }

                public void Method2()
                {

                }
            }
        }";



        [TestInitialize]
        public void TestsInit()
        {

        }

        [TestMethod]
        public void Count_Num_Of_Test_Classes()
        {

            var classes = TestsGenerator.TestsGenerator.GetClasses(sourceCode);

            Assert.AreEqual(1, classes.Count());
        }
        [TestMethod]
        public void Correct_Class_Name()
        {
            var test = TestsGenerator.TestsGenerator.GenerateTests(TestsGenerator.TestsGenerator.GetClasses(sourceCode).First());

            var className1 = CSharpSyntaxTree.ParseText(test.Content)
                .GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Single()
                .Identifier
                .ValueText;

            Assert.AreEqual("Class1Test", className1);
        }
        [TestMethod]
        public void Correct_Num_Of_Methods()
        {
            var test = TestsGenerator.TestsGenerator.GenerateTests(TestsGenerator.TestsGenerator.GetClasses(sourceCode).First());

            var count = CSharpSyntaxTree.ParseText(test.Content)
                .GetRoot()
                .DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Count();

            Assert.AreEqual(3, count);
        }
    }
}
