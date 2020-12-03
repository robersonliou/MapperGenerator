using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MapperGenerator.Extensions
{
    public static class SyntaxExtensions
    {
        public static IEnumerable<(string propertyType, string propertyName, PropertyDeclarationSyntax propertySyntax)> GetProperties(this ClassDeclarationSyntax classSyntax, SemanticModel semanticModel)
        {
            (string propertyType, string propertyName, PropertyDeclarationSyntax) GetPropertyInfo(
                PropertyDeclarationSyntax propertySyntax, SemanticModel model)
            {
                var declaredSymbol = model.GetDeclaredSymbol(propertySyntax);
                var propertyType = declaredSymbol.Type.ToString();
                var propertyName = declaredSymbol.Name;

                return (propertyType, propertyName, propertySyntax);
            }

            var propertySyntaxes = classSyntax.SyntaxTree.GetRoot().DescendantNodes().OfType<PropertyDeclarationSyntax>();
            return propertySyntaxes.Select(prop
                => GetPropertyInfo(prop, semanticModel));
        }
    }
}