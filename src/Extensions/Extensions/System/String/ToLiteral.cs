﻿using System.CodeDom;
using System.CodeDom.Compiler;
using System.IO;

namespace Xpand.Source.Extensions.System.String{
    internal static partial class StringExtensions{

        internal static string ToLiteral(this string input){
            using (var writer = new StringWriter()){
                using (var codeDomProvider = CodeDomProvider.CreateProvider("CSharp")){
                    codeDomProvider.GenerateCodeFromExpression(new CodePrimitiveExpression(input), writer, null);
                    return writer.ToString();
                }
            }
        }
    }
}