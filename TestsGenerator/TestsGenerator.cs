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

            return writeBlock.Completion;
        }
        private static TransformManyBlock<string, string> CreateReadDirectoryBlock() =>
            new TransformManyBlock<string, string>(path =>
                {
                    if (!Directory.Exists(path))
                    {
                        throw new ArgumentException("Directory doesn't exists");
                    }

                    return Directory.EnumerateFiles(path, "*.cs");
                }
            );

        private static TransformBlock<string, string> CreateReadFileBlock(int maxParallelism) =>
            new TransformBlock<string, string>( async path =>
                    {
                        if (!File.Exists(path))
                        {
                            throw new ArgumentException("File doesn't exist");
                        }

                        string text = "";
                        using (var file = File.OpenText(path))
                        {
                            text = await file.ReadToEndAsync();
                        }
                        return text;
                    }
                , new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = maxParallelism }
                );

        private static TransformManyBlock<string, MethodInfo> CreateSplitClassesBlock() =>
            new TransformManyBlock<string, MethodInfo>( GetClasses );

        private static TransformBlock<MethodInfo, TestsFile> CreateTestGeneratorBlock(int maxParallelism) =>
            new TransformBlock<MethodInfo, TestsFile>( GenerateTests,new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = maxParallelism });

        private static ActionBlock<TestsFile> CreateWriterBlock(string path, int maxParallelism) =>
            new ActionBlock<TestsFile>( async testFile =>
           {
               if (!Directory.Exists(path))
               {
                   throw new ArgumentException("Directory doesn't exists");
               }

               using (var file = File.CreateText($"{path}/{testFile.Filename}"))
               {
                    await file.WriteAsync(testFile.Content);
               }

               return;
               
           }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = maxParallelism });

        public static IEnumerable<MethodInfo> GetClasses(string code)
        {
            var root = CSharpSyntaxTree.ParseText(code).GetRoot();
            return root.DescendantNodes().OfType<ClassDeclarationSyntax>().Select(c =>
            {
                return new MethodInfo()
                {
                    classDeclaration = c,
                    usingDirectiveDeclaration = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().First().Name.ToFullString()))
                };
            });
        }
        public static TestsFile GenerateTests(MethodInfo mInfo)
        {
            var tab = " ";

            var classDeclaration = mInfo.classDeclaration;

            var methods = classDeclaration
                .DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword)));

            var objectName = classDeclaration.Identifier.Text + "TestObject";

            var methodsDeclars = methods.Select( m =>
            {
                bool hasParams = m.ParameterList.Parameters.Count > 0;

                List<LocalDeclarationStatementSyntax> initVars = null;
                var args = SyntaxFactory.ArgumentList();
                if (hasParams)
                {
                    int n = m.ParameterList.Parameters.Count;

                    foreach (var p in m.ParameterList.Parameters)
                    {
                        n--;
                        if (n > 0)
                        {
                            args = args.AddArguments(SyntaxFactory.SeparatedList<ArgumentSyntax>(
                                                    new SyntaxNodeOrToken[]{
                                                    SyntaxFactory.Argument(
                                                        SyntaxFactory.IdentifierName(p.Identifier.Text)),
                                                    SyntaxFactory.Token(SyntaxKind.CommaToken),
                                                        }).ToArray());
                        }
                        else
                        {
                            args = args.AddArguments(SyntaxFactory.SeparatedList<ArgumentSyntax>(
                                                    new SyntaxNodeOrToken[]{
                                                    SyntaxFactory.Argument(
                                                        SyntaxFactory.IdentifierName(p.Identifier.Text)),
                                                        }).ToArray());
                        }
                    }

                    initVars = m.ParameterList.Parameters.Select(p =>
                    {
                        return SyntaxFactory.LocalDeclarationStatement(
                        SyntaxFactory.VariableDeclaration(
                            p.Type)
                        .WithVariables(
                            SyntaxFactory.SingletonSeparatedList<VariableDeclaratorSyntax>(
                                SyntaxFactory.VariableDeclarator(
                                    SyntaxFactory.Identifier(p.Identifier.Text))
                                .WithInitializer(
                                    SyntaxFactory.EqualsValueClause(
                                        SyntaxFactory.LiteralExpression(
                                            SyntaxKind.DefaultLiteralExpression,
                                            SyntaxFactory.Token(SyntaxKind.DefaultKeyword)))))))
                        .WithSemicolonToken(SyntaxFactory.Token(
                            SyntaxFactory.TriviaList(),
                            SyntaxKind.SemicolonToken,
                            SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)));
                    }).ToList() ;

                    initVars[0] = initVars[0].WithLeadingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed,
                                                SyntaxFactory.Comment("//Arrange")));
                }
                

                bool isVoid = ((PredefinedTypeSyntax)m.ReturnType).Keyword.Text.Equals("void");

                var invokeMethod = isVoid ? 
                SyntaxFactory.ExpressionStatement(SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(
                                            SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.Whitespace(tab + tab + tab)),
                                        objectName,
                                            SyntaxFactory.TriviaList())),
                                        SyntaxFactory.IdentifierName(m.Identifier.Text)))
                                    .WithArgumentList(
                                        args
                                        ))
                    .WithSemicolonToken(SyntaxFactory.Token(
                            SyntaxFactory.TriviaList(),
                            SyntaxKind.SemicolonToken,
                            SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)))
                    .WithLeadingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed,
                                                SyntaxFactory.Comment("//Act")))
                    as StatementSyntax
                :
                SyntaxFactory.LocalDeclarationStatement(
                    SyntaxFactory.VariableDeclaration(
                        m.ReturnType)
                    .WithVariables(
                        SyntaxFactory.SingletonSeparatedList<VariableDeclaratorSyntax>(
                            SyntaxFactory.VariableDeclarator(
                                SyntaxFactory.Identifier(
                                                "actual"
                                                ))
                            .WithInitializer(
                                SyntaxFactory.EqualsValueClause(
                                    SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(
                                            objectName)),
                                        SyntaxFactory.IdentifierName(m.Identifier.Text)))
                                    .WithArgumentList(
                                        args
                                        )
                                    )))))
                .WithSemicolonToken(SyntaxFactory.Token(
                        SyntaxFactory.TriviaList(),
                        SyntaxKind.SemicolonToken,
                        SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.CarriageReturnLineFeed)))
                .WithLeadingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed,
                                                SyntaxFactory.Comment("//Act")))
                ;

                LocalDeclarationStatementSyntax expVar = null;
                ExpressionStatementSyntax assertInvoke = null;
                if (!isVoid)
                {
                    expVar = SyntaxFactory.LocalDeclarationStatement(
                        SyntaxFactory.VariableDeclaration(
                            m.ReturnType)
                        .WithVariables(
                            SyntaxFactory.SingletonSeparatedList<VariableDeclaratorSyntax>(
                                SyntaxFactory.VariableDeclarator(
                                    SyntaxFactory.Identifier("expected"))
                                .WithInitializer(
                                    SyntaxFactory.EqualsValueClause(
                                        SyntaxFactory.LiteralExpression(
                                            SyntaxKind.DefaultLiteralExpression,
                                            SyntaxFactory.Token(SyntaxKind.DefaultKeyword)))))))
                    .WithSemicolonToken(SyntaxFactory.Token(
                            SyntaxFactory.TriviaList(),
                            SyntaxKind.SemicolonToken,
                            SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)));
                    
                    assertInvoke = SyntaxFactory.ExpressionStatement(SyntaxFactory
                        .InvocationExpression(SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(
                                SyntaxFactory.TriviaList(SyntaxFactory.Whitespace(tab + tab + tab)),
                            "Assert",
                                SyntaxFactory.TriviaList())),
                            SyntaxFactory.IdentifierName("That")))
                        .WithArgumentList(
                            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList<ArgumentSyntax>(
                                        new SyntaxNodeOrToken[]
                                        {
                                            SyntaxFactory.Argument(SyntaxFactory.IdentifierName("actual")),
                                            SyntaxFactory.Token(SyntaxKind.CommaToken),
                                            SyntaxFactory.Argument((SyntaxFactory
                                            .InvocationExpression(SyntaxFactory.MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(
                                                    SyntaxFactory.TriviaList(SyntaxFactory.Whitespace(tab + tab + tab)),
                                                "Is",
                                                    SyntaxFactory.TriviaList())),
                                                SyntaxFactory.IdentifierName("EqualTo")))
                                            .WithArgumentList(
                                                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList<ArgumentSyntax>(
                                                            new SyntaxNodeOrToken[]
                                                            {
                                                                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("expected"))
                                                            })))))
                                        }))))
                        .WithSemicolonToken(SyntaxFactory.Token(
                            SyntaxFactory.TriviaList(),
                            SyntaxKind.SemicolonToken,
                            SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)));
                }
                    

                

                var secondAssertInvoke = SyntaxFactory.ExpressionStatement(SyntaxFactory
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
                            SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)));

                List<StatementSyntax> statements = new List<StatementSyntax>();

                if (hasParams)
                    statements.AddRange(initVars);
                statements.Add(invokeMethod);
                if (!isVoid)
                {
                    statements.Add(expVar
                        .WithLeadingTrivia(SyntaxFactory.Comment("//Assert")));
                    statements.Add(assertInvoke);
                    statements.Add(secondAssertInvoke);
                }
                else
                {
                    statements.Add(secondAssertInvoke.WithLeadingTrivia(SyntaxFactory.Comment("//Assert")));
                }
                

                return SyntaxFactory
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
                                .Block(statements)
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
                                        SyntaxFactory.CarriageReturnLineFeed))));
            }
            );

            var ctrs = classDeclaration.DescendantNodes().OfType<ConstructorDeclarationSyntax>().OrderByDescending( ct => ct.ParameterList.Parameters.Count);
            ConstructorDeclarationSyntax ourCtr = null;
            foreach(var ctr in ctrs){
                foreach(var arg in ctr.ParameterList.Parameters)
                {
                    if(arg.Type.ToString()[0] == 'I' && char.IsUpper(arg.Type.ToString()[1]))
                    {
                        ourCtr = ctr;
                        break;
                    }
                }
            }

            IEnumerable<FieldDeclarationSyntax> mockFields = new List<FieldDeclarationSyntax>();
            IEnumerable<ExpressionStatementSyntax> initMocks = new List<ExpressionStatementSyntax>();
            IEnumerable<ArgumentSyntax> initObjArgs = new List<ArgumentSyntax>();

            if(ourCtr != null)
            {
                mockFields = ourCtr.ParameterList.Parameters.Select(p =>
                {
                    return SyntaxFactory.FieldDeclaration(
                                     SyntaxFactory.VariableDeclaration(
                                         SyntaxFactory.GenericName(
                                             SyntaxFactory.Identifier("Mock"))
                                         .WithTypeArgumentList(
                                             SyntaxFactory.TypeArgumentList(
                                                 SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                                     SyntaxFactory.IdentifierName(p.Type.ToString())))))
                                     .WithVariables(
                                         SyntaxFactory.SingletonSeparatedList<VariableDeclaratorSyntax>(
                                             SyntaxFactory.VariableDeclarator(
                                                 SyntaxFactory.Identifier(p.Identifier.Text)))))
                                 .WithModifiers(
                                     SyntaxFactory.TokenList(
                                         SyntaxFactory.Token(SyntaxKind.PrivateKeyword)));
                });

                initMocks = ourCtr.ParameterList.Parameters.Select(p =>
                {
                    return SyntaxFactory.ExpressionStatement(
                                            SyntaxFactory.AssignmentExpression(
                                                SyntaxKind.SimpleAssignmentExpression,
                                                SyntaxFactory.IdentifierName(p.Identifier.Text),
                                                SyntaxFactory.ObjectCreationExpression(
                                                    SyntaxFactory.GenericName(
                                                        SyntaxFactory.Identifier("Mock"))
                                                    .WithTypeArgumentList(
                                                        SyntaxFactory.TypeArgumentList(
                                                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                                                SyntaxFactory.IdentifierName(p.Type.ToString())))))
                                                .WithArgumentList(
                                                    SyntaxFactory.ArgumentList())));
                });

                initObjArgs = ourCtr.ParameterList.Parameters.Select(p =>
                {
                    return SyntaxFactory.Argument(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName(p.Identifier.Text),
                                SyntaxFactory.IdentifierName("Object")));
                });
            }

            var setUpMethod = SyntaxFactory.MethodDeclaration(
                                SyntaxFactory.PredefinedType(
                                    SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                                SyntaxFactory.Identifier("SetUp"))
                            .WithAttributeLists(
                                SyntaxFactory.SingletonList<AttributeListSyntax>(
                                    SyntaxFactory.AttributeList(
                                        SyntaxFactory.SingletonSeparatedList<AttributeSyntax>(
                                            SyntaxFactory.Attribute(
                                                SyntaxFactory.IdentifierName("SetUp"))))))
                            .WithModifiers(
                                SyntaxFactory.TokenList(
                                    SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                            .WithBody(
                                SyntaxFactory.Block(
                                    initMocks
                                    )
                                .AddStatements(SyntaxFactory.ExpressionStatement(
                                        SyntaxFactory.AssignmentExpression(
                                            SyntaxKind.SimpleAssignmentExpression,
                                            SyntaxFactory.IdentifierName(objectName),
                                            SyntaxFactory.ObjectCreationExpression(
                                                SyntaxFactory.IdentifierName(classDeclaration.Identifier.Text))
                                            .WithArgumentList(
                                                SyntaxFactory.ArgumentList(
                                                    )
                                                .AddArguments(initObjArgs.ToArray()))))));

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
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("Moq")),
                SyntaxFactory.UsingDirective(SyntaxFactory.IdentifierName("Autogenerated")),
                mInfo.usingDirectiveDeclaration
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
                    .AddMembers(mockFields.ToArray())
                    .AddMembers(
                            SyntaxFactory.FieldDeclaration(
                                SyntaxFactory.VariableDeclaration(
                                    SyntaxFactory.IdentifierName(classDeclaration.Identifier))
                                .WithVariables(
                                    SyntaxFactory.SingletonSeparatedList<VariableDeclaratorSyntax>(
                                        SyntaxFactory.VariableDeclarator(
                                            SyntaxFactory.Identifier(objectName)))))
                            .WithModifiers(
                                SyntaxFactory.TokenList(
                                    SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
                        )
                    .AddMembers(setUpMethod)
                    .AddMembers(methodsDeclars.ToArray())
                    )
                )
            .NormalizeWhitespace();

            return new TestsFile(classDeclaration.Identifier.Text + "Test.cs", testCode.ToFullString());
        }
    }
}
