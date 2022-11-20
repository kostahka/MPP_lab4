using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TestsGenerator
{
    static public class TestsGenerator
    {
        public static Task Generate(GeneratorOptions options)
        {
            var directoryBlock = CreateReadDirectoryBlock();
            var fileBlock = CreateReadFileBlock(options.MaxRead);
            var splitClassesBlock = CreateSplitClassesBlock();
            var generatorBlock = CreateTestGeneratorBlock(options.MaxGenerate);
            var writeBlock = CreateWriterBlock(options.DestinationDirectory, options.MaxWrite);

            var opt = new DataflowLinkOptions() { PropagateCompletion = true };
            directoryBlock.LinkTo(fileBlock, opt);
            fileBlock.LinkTo(splitClassesBlock, opt);
            splitClassesBlock.LinkTo(generatorBlock, opt);
            generatorBlock.LinkTo(writeBlock, opt);

            directoryBlock.Post(options.SourceDirectory);
            directoryBlock.Complete();

            return directoryBlock.Completion;
        }
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

        private static TransformManyBlock<string, ClassDeclarationSyntax> CreateSplitClassesBlock() =>
            new TransformManyBlock<string, ClassDeclarationSyntax>( code => 
            {
                return CSharpSyntaxTree.ParseText(code).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();    
            } );

        private static TransformBlock<ClassDeclarationSyntax, TestsFile> CreateTestGeneratorBlock(int maxParallelism) =>
            new TransformBlock<ClassDeclarationSyntax, TestsFile>( GenerateTests,new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = maxParallelism });

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

        private static TestsFile GenerateTests(ClassDeclarationSyntax classDeclaration)
        {
            var tab = " ";

            var methods = classDeclaration
                .DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword))); ;

            var testCode = SyntaxFactory.CompilationUnit()
            .AddUsings(
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")),
                SyntaxFactory.UsingDirective(SyntaxFactory.QualifiedName(SyntaxFactory.QualifiedName(
                    SyntaxFactory.IdentifierName("System"),
                    SyntaxFactory.IdentifierName("Collections")
                    ),
                    SyntaxFactory.IdentifierName("Generic")
                )),
                SyntaxFactory.UsingDirective(SyntaxFactory.QualifiedName(
                    SyntaxFactory.IdentifierName("System"),
                    SyntaxFactory.IdentifierName("Linq")
                )),
                SyntaxFactory.UsingDirective(SyntaxFactory.QualifiedName(
                    SyntaxFactory.IdentifierName("System"),
                    SyntaxFactory.IdentifierName("Text")
                )),
                SyntaxFactory.UsingDirective(SyntaxFactory.QualifiedName(
                    SyntaxFactory.IdentifierName("NUnit"),
                    SyntaxFactory.IdentifierName("Framework")
                )),
                SyntaxFactory.UsingDirective(SyntaxFactory.IdentifierName("Autogenerated"))
                )
            .AddMembers(
                SyntaxFactory.NamespaceDeclaration(
                        SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("Autogenerated"),
                        SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(SyntaxFactory.TriviaList(), "Tests",
                        SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed))))
                    )
                .WithNamespaceKeyword(
                    SyntaxFactory.Token(
                        SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.CarriageReturnLineFeed),
                        SyntaxKind.NamespaceKeyword,
                        SyntaxFactory.TriviaList(SyntaxFactory.Space)))
                .WithOpenBraceToken(
                    SyntaxFactory.Token(SyntaxFactory.TriviaList(), SyntaxKind.OpenBraceToken,
                    SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)))
                .WithCloseBraceToken(
                    SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed),
                    SyntaxKind.CloseBraceToken,
                    SyntaxFactory.TriviaList()))
                .AddMembers(
                    SyntaxFactory.ClassDeclaration(SyntaxFactory.Identifier(
                            SyntaxFactory.TriviaList(),
                            classDeclaration.Identifier.Text + "Test",
                            SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)))
                    .WithAttributeLists(SyntaxFactory.SingletonList(SyntaxFactory
                                .AttributeList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("TestFixture"))))
                                .WithOpenBracketToken(SyntaxFactory.Token(
                                    SyntaxFactory.TriviaList(SyntaxFactory.Whitespace(tab)),
                                    SyntaxKind.OpenBracketToken,
                                    SyntaxFactory.TriviaList()))
                                .WithCloseBracketToken(SyntaxFactory.Token(
                                    SyntaxFactory.TriviaList(),
                                    SyntaxKind.CloseBracketToken,
                                    SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)))))
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(
                            SyntaxFactory.TriviaList(SyntaxFactory.Whitespace(tab)),
                            SyntaxKind.PublicKeyword,
                            SyntaxFactory.TriviaList(SyntaxFactory.Space))))
                    .WithKeyword(SyntaxFactory.Token(
                            SyntaxFactory.TriviaList(),
                            SyntaxKind.ClassKeyword,
                            SyntaxFactory.TriviaList(SyntaxFactory.Space)))
                    .WithOpenBraceToken(SyntaxFactory.Token(
                            SyntaxFactory.TriviaList(SyntaxFactory.Whitespace(tab)),
                            SyntaxKind.OpenBraceToken,
                            SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)))
                    .WithCloseBraceToken(
                        SyntaxFactory.Token(
                            SyntaxFactory.TriviaList(SyntaxFactory.Whitespace(tab)),
                            SyntaxKind.CloseBraceToken,
                            SyntaxFactory.TriviaList()))
                    .AddMembers(
                            methods.Select( m => 
                            SyntaxFactory
                            .MethodDeclaration(
                                SyntaxFactory.PredefinedType(
                                    SyntaxFactory.Token(
                                        SyntaxFactory.TriviaList(),
                                        SyntaxKind.VoidKeyword,
                                        SyntaxFactory.TriviaList(SyntaxFactory.Space))),
                                    SyntaxFactory.Identifier(m.Identifier.Text + "Test"))
                            .WithAttributeLists(SyntaxFactory.SingletonList(SyntaxFactory
                                .AttributeList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Test"))))
                                .WithOpenBracketToken(SyntaxFactory.Token(
                                    SyntaxFactory.TriviaList(SyntaxFactory.Whitespace(tab + tab)),
                                    SyntaxKind.OpenBracketToken,
                                    SyntaxFactory.TriviaList()))
                                .WithCloseBracketToken(SyntaxFactory.Token(
                                    SyntaxFactory.TriviaList(),
                                    SyntaxKind.CloseBracketToken,
                                    SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)))))
                                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxFactory.TriviaList(
                                    SyntaxFactory.Whitespace(tab + tab)),
                                    SyntaxKind.PublicKeyword,
                                    SyntaxFactory.TriviaList(SyntaxFactory.Space))))
                                .WithParameterList(SyntaxFactory.ParameterList().WithCloseParenToken(
                                    SyntaxFactory.Token(SyntaxFactory.TriviaList(),
                                    SyntaxKind.CloseParenToken,
                                    SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed))))
                                .WithBody(SyntaxFactory
                                .Block()
                                .WithOpenBraceToken(SyntaxFactory.Token(SyntaxFactory.TriviaList(
                                    SyntaxFactory.Whitespace(tab + tab)),
                                    SyntaxKind.OpenBraceToken,
                                    SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)))
                                .WithCloseBraceToken(SyntaxFactory.Token(SyntaxFactory.TriviaList(
                                    SyntaxFactory.Whitespace(tab + tab)),
                                    SyntaxKind.CloseBraceToken,
                                    SyntaxFactory.TriviaList(
                                        SyntaxFactory.CarriageReturnLineFeed,
                                        SyntaxFactory.Whitespace(""),
                                        SyntaxFactory.CarriageReturnLineFeed))))
                                .AddBodyStatements(SyntaxFactory.ExpressionStatement(SyntaxFactory
                                    .InvocationExpression(SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(
                                            SyntaxFactory.TriviaList(SyntaxFactory.Whitespace(tab + tab + tab)),
                                        "Assert",
                                            SyntaxFactory.TriviaList())),
                                        SyntaxFactory.IdentifierName("Fail")))
                                    .WithArgumentList(
                                        SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList<ArgumentSyntax>(
                                                    new SyntaxNodeOrToken[]
                                                    {
                                                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                                                                SyntaxKind.StringLiteralExpression,
                                                                SyntaxFactory.Literal("autogenerated")))
                                                    }))))
                                    .WithSemicolonToken(SyntaxFactory.Token(
                                        SyntaxFactory.TriviaList(),
                                        SyntaxKind.SemicolonToken,
                                        SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed))))
                            ).Cast<MethodDeclarationSyntax>()
                            .ToArray()
                        )
                    )
                )
            .NormalizeWhitespace();

            return new TestsFile(classDeclaration.Identifier.Text + "Test.cs", testCode.ToFullString());
        }
    }
}
