﻿using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MapperGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MapperGenerator.Generators
{
    [Generator]
    public class MapperGenerator : ISourceGenerator
    {
        private const string NameSpace = "MapperGenerator";
        private const string MappingAttributeText = @"
using System;
namespace MapperGenerator
{
    public class MappingAttribute : Attribute
    {
        public MappingAttribute(Type targetType)
        {
            this.TargetType = targetType;
        }

        public Type TargetType { get; set; }
    }
}";

        public void Initialize(GeneratorInitializationContext context)
        {
#region Manually toggle debugger
            //Debugger.Launch();
#endregion
            //context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            context.AddSource("MapperAttribute", SourceText.From(MappingAttributeText, Encoding.UTF8));

            //Create a new compilation that contains the attribute
            var options = (context.Compilation as CSharpCompilation).SyntaxTrees[0].Options as CSharpParseOptions;
            var compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(MappingAttributeText, Encoding.UTF8), options));

            var allNodes = compilation.SyntaxTrees.SelectMany(s => s.GetRoot().DescendantNodes());
            var allAttributes = allNodes.Where((d) => d.IsKind(SyntaxKind.Attribute)).OfType<AttributeSyntax>();
            var attributes = allAttributes.Where(d => d.Name.ToString() == "Mapping"
                                                      || d.Name.ToString() == "Mapper.Mapping").ToImmutableArray();
            var allClasses = compilation.SyntaxTrees.
                SelectMany(x => x.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>());


            var sourceBuilder = new StringBuilder(@"
//<auto-generated>
using System;
namespace Mapper
{
    public static class GeneratorMapper
    {");
            foreach (AttributeSyntax attr in attributes)
            {
                if (attr.ArgumentList is null) throw new Exception("Can't be null here");

                #region Get Mapping Source Class Info

                //todo: add diagnostic when ArgumentList is null
                //get type of mapping target from constructor argument 
                var mappedTypeArgSyntax = attr.ArgumentList.Arguments.First();
                var mappedTypeArgSyntaxExpr = mappedTypeArgSyntax.Expression.NormalizeWhitespace().ToFullString();

                var sourceClassName = GetContentInParentheses(mappedTypeArgSyntaxExpr);
                var sourceClassSyntax = allClasses.First(x => x.Identifier.ToString() == sourceClassName);
                var sourceClassModel = compilation.GetSemanticModel(sourceClassSyntax.SyntaxTree);
                var sourceClassNamedTypeSymbol = ModelExtensions.GetDeclaredSymbol(sourceClassModel, sourceClassSyntax);
                var sourceClassFullName = sourceClassNamedTypeSymbol.OriginalDefinition.ToString();
                var sourceClassProperties = sourceClassSyntax.GetProperties(sourceClassModel);

                #endregion

                #region Get Mapping Target Class Info

                var targetClassSyntax = attr.SyntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Last();
                var targetClassModel = compilation.GetSemanticModel(attr.SyntaxTree);
                var targetClassNamedTypeSymbol = ModelExtensions.GetDeclaredSymbol(targetClassModel, targetClassSyntax);
                var targetClassFullName = targetClassNamedTypeSymbol.OriginalDefinition.ToString();
                var targetClassName = targetClassFullName.Split('.').Last();
                var targetClassProperties = targetClassSyntax.GetProperties(targetClassModel);

                #endregion

                #region Create diagnostic erroes if any property of target doesn't match to source properties.

                //source class properties should match all of target class properties
                //should use same name and type of property
                var targetPropertiesMatchedResult = targetClassProperties.Select(target => new {
                    TargetPropertyName = target.propertyName,
                    IsMatched = sourceClassProperties.Any(source =>
                        source.propertyName == target.propertyName &&
                        source.propertyType == target.propertyType)
                });
                if (targetPropertiesMatchedResult.Any(x => x.IsMatched == false))
                {
                    foreach (var target in targetPropertiesMatchedResult.Where(x => x.IsMatched == false))
                    {
                        var diagnostic = Diagnostic.Create(new DiagnosticDescriptor("MPERR001", "Property mapping error",
                            $"{targetClassName}.{target.TargetPropertyName} couldn't match to {sourceClassName}, please check if the name and type of properties are the same.", "source generator", DiagnosticSeverity.Error, true), Location.None);
                        context.ReportDiagnostic(diagnostic);                    
                    }
                    break;
                }

                #endregion

                #region Build mapper method

                sourceBuilder.Append(@$"
        public static {targetClassFullName} MapTo{targetClassName}({sourceClassFullName} source)
        {{
            var target = new {targetClassFullName}();");

                foreach (var (_, propertyName) in targetClassProperties)
                {
                    sourceBuilder.Append(@$"
            target.{propertyName} = source.{propertyName};");
                }


                sourceBuilder.Append(@"
            return target;
        }
");


                sourceBuilder.Append(@$"
        public static {targetClassFullName} To{targetClassName}(this {sourceClassFullName} source)
        {{
            var target = new {targetClassFullName}();");

                foreach (var (_, propertyName) in targetClassProperties)
                {
                    sourceBuilder.Append(@$"
            target.{propertyName} = source.{propertyName};");
                }


                sourceBuilder.Append(@"
            return target;
        }
");

                #endregion

            }
            sourceBuilder.Append(@"
    }
}");

            context.AddSource("GeneratorMapper", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));

        }

        private string GetContentInParentheses(string value)
        {
            var match = Regex.Match(value, @"\(([^)]*)\)");
            return match.Groups[1].Value;
        }
    }
}
