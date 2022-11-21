﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TestsGenerator
{
    public class MethodInfo
    {
        public ClassDeclarationSyntax classDeclaration;
        public UsingDirectiveSyntax usingDirectiveDeclaration;
    }
}
