using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MapperGenerator.Extensions
{
    public static class SyntaxExtensions
    {
        public static IEnumerable<(string propertyType, string propertyName)> GetProperties(this ClassDeclarationSyntax classSyntax, SemanticModel semanticModel)
        {
            (string propertyType, string propertyName) GetPropertyInfo(
                PropertyDeclarationSyntax prop, SemanticModel model)
            {
                var declaredSymbol = model.GetDeclaredSymbol(prop);
                var propertyType = declaredSymbol.Type.ToString();
                var propertyName = declaredSymbol.Name;

                return (propertyType, propertyName);
            }

            var propertySyntaxes = classSyntax.SyntaxTree.GetRoot().DescendantNodes().OfType<PropertyDeclarationSyntax>();
            return propertySyntaxes.Select(prop
                => GetPropertyInfo(prop, semanticModel));
        }
    }
}