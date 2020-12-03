using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MapperGenerator.Tests.Helper
{
    internal class GeneratorTestHelper
    {
        internal static (Compilation, ImmutableArray<Diagnostic> diagnostics) RunGenerators(
            Compilation originCompilation,
            params ISourceGenerator[] generators)
        {
            CreateDriver(originCompilation, generators).RunGeneratorsAndUpdateCompilation(originCompilation,
                out var resultCompilation, out var diagnostics);
            return (resultCompilation, diagnostics);
        }

        internal static Compilation CreateCompilation(params string[] source)
        {
            var dd = typeof(Enumerable).GetTypeInfo().Assembly.Location;
            var coreDir = Directory.GetParent(dd) ?? throw new Exception("Couldn't find location of coredir");

            var references = GetReferneces(coreDir);

            var syntaxTrees = source.Select(x =>
                CSharpSyntaxTree.ParseText(x, new CSharpParseOptions(LanguageVersion.Preview)));
            return CSharpCompilation.Create(
                "compilation",
                syntaxTrees,
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }

        internal static Assembly GetAssemblyFromCompilation(Compilation resultCompilation)
        {
            using var stream = new MemoryStream();
            resultCompilation.Emit(stream);
            var assembly = Assembly.Load(stream.ToArray());
            return assembly;
        }

        private static GeneratorDriver CreateDriver(Compilation c, params ISourceGenerator[] generators)
        {
            var parseOptions = (CSharpParseOptions) c.SyntaxTrees.First().Options;

            return CSharpGeneratorDriver.Create(
                ImmutableArray.Create(generators),
                ImmutableArray<AdditionalText>.Empty,
                parseOptions);
        }

        private static PortableExecutableReference[] GetReferneces(DirectoryInfo coreDir)
        {
            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).GetTypeInfo().Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Dictionary<,>).GetTypeInfo().Assembly.Location),
                MetadataReference.CreateFromFile($"{coreDir.FullName}{Path.DirectorySeparatorChar}mscorlib.dll"),
                MetadataReference.CreateFromFile($"{coreDir.FullName}{Path.DirectorySeparatorChar}System.Runtime.dll"),
                MetadataReference.CreateFromFile(
                    $"{coreDir.FullName}{Path.DirectorySeparatorChar}System.Collections.dll"),
            };
            return references;
        }
    }
}