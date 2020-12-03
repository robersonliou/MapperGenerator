using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using MapperGenerator.Tests.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace MapperGenerator.Tests
{
    public class MapperGeneratorTest
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void General_Property_Mapping()
        {
            #region Produce two mapping class text

            const string sourceText = @"
namespace Sample.Entities
{
    public class Person
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
";

            const string targetText = @"
using MapperGenerator;
using Sample.Entities;
namespace Sample.Models
{
    [Mapping(typeof(Person))]
    public class PersonViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
";

            #endregion

            var originCompilation = GeneratorTestHelper.CreateCompilation(sourceText, targetText);
            var (resultCompilation, generatorDiagnostics) =
                GeneratorTestHelper.RunGenerators(originCompilation, new Generators.MapperGenerator());

            // verify no errors or warnings are returned
            Assert.IsEmpty(generatorDiagnostics);
            Assert.IsEmpty(resultCompilation.GetDiagnostics());

            // compile and get an assembly along with our methods.
            var assembly = GeneratorTestHelper.GetAssemblyFromCompilation(resultCompilation);
            var mapperType = assembly.GetType("MapperGenerator.Mapper");
            var generalMappingMethod =
                mapperType?.GetMethod("MapToPersonViewModel"); // this one is added via the generator
            var extensionMappingMethod = mapperType?.GetMethod("ToPersonViewModel"); // this is in our source

            Assert.NotNull(generalMappingMethod);
            Assert.NotNull(extensionMappingMethod);

            //var mapper = Activator.CreateInstance(mapperType);
            var personType = assembly.GetType("Sample.Entities.Person");
            var person = Activator.CreateInstance(personType);
            personType.GetProperty("Id")?.SetValue(person, 1);
            personType.GetProperty("Name")?.SetValue(person, "Roberson");


            var result = generalMappingMethod.Invoke(null, new[] {person});

            var expectedType = assembly.GetType("Sample.Models.PersonViewModel");
            Assert.AreEqual(expectedType, result.GetType());

            var actualId = expectedType.GetProperty("Id").GetValue(result);
            var actualName = expectedType.GetProperty("Name").GetValue(result);

            Assert.AreEqual(1, actualId);
            Assert.AreEqual("Roberson", actualName);
        }

        [Test]
        public void Property_Mismatch_Throw_Diagnostic_Error()
        {
            #region Produce two mapping class text

            const string sourceText = @"
namespace Sample.Entities
{
    public class Person
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
";

            const string targetText = @"
using MapperGenerator;
using Sample.Entities;
namespace Sample.Models
{
    [Mapping(typeof(Person))]
    public class PersonViewModel
    {
        public int Sn { get; set; }
        public string Name { get; set; }
    }
}
";

            #endregion

            var originCompilation = GeneratorTestHelper.CreateCompilation(sourceText, targetText);
            var (_, generatorDiagnostics) =
                GeneratorTestHelper.RunGenerators(originCompilation, 
                    new Generators.MapperGenerator());
            var diagnostic = generatorDiagnostics.FirstOrDefault();


            Assert.AreEqual("MPERR001", diagnostic.Id);
        }
    }
}