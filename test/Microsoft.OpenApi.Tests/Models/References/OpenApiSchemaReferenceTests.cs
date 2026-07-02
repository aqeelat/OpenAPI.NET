// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. 

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using VerifyXunit;
using Xunit;

namespace Microsoft.OpenApi.Tests.Models.References
{
    [Collection("DefaultSettings")]
    public class OpenApiSchemaReferenceTests
    {
        [Fact]
        public void SchemaReferenceWithAnnotationsShouldWork()
        {
            // Arrange
            var workingDocument = new OpenApiDocument()
            {
                Components = new OpenApiComponents(),
            };
            const string referenceId = "targetSchema";
            var targetSchema = new OpenApiSchema()
            {
                Type = JsonSchemaType.Object,
                Title = "Target Title",
                Description = "Target Description",
                ReadOnly = false,
                WriteOnly = false,
                Deprecated = false,
                Default = JsonValue.Create("target default"),
                Examples = new List<JsonNode> { JsonValue.Create("target example") },
                Properties = new Dictionary<string, IOpenApiSchema>()
                {
                    ["prop1"] = new OpenApiSchema()
                    {
                        Type = JsonSchemaType.String
                    }
                }
            };
            workingDocument.Components.Schemas = new Dictionary<string, IOpenApiSchema>()
            {
                [referenceId] = targetSchema
            };
            workingDocument.Workspace.RegisterComponents(workingDocument);

            // Act
            var schemaReference = new OpenApiSchemaReference(referenceId, workingDocument)
            {
                Title = "Override Title",
                Description = "Override Description",
                ReadOnly = true,
                WriteOnly = true,
                Deprecated = true,
                Default = JsonValue.Create("override default"),
                Examples = new List<JsonNode> { JsonValue.Create("override example") },
            };

            // Assert
            Assert.Equal("Override Title", schemaReference.Title);
            Assert.Equal("Override Description", schemaReference.Description);
            Assert.True(schemaReference.ReadOnly);
            Assert.True(schemaReference.WriteOnly);
            Assert.True(schemaReference.Deprecated);
            Assert.Equal("override default", schemaReference.Default?.GetValue<string>());
            Assert.Single(schemaReference.Examples);
            Assert.Equal("override example", schemaReference.Examples.First()?.GetValue<string>());
        }

        [Fact]
        public void SchemaReferenceWithoutAnnotationsShouldFallbackToTarget()
        {
            // Arrange
            var workingDocument = new OpenApiDocument()
            {
                Components = new OpenApiComponents(),
            };
            const string referenceId = "targetSchema";
            var targetSchema = new OpenApiSchema()
            {
                Type = JsonSchemaType.Object,
                Title = "Target Title",
                Description = "Target Description",
                ReadOnly = true,
                WriteOnly = false,
                Deprecated = true,
                Default = JsonValue.Create("target default"),
                Examples = new List<JsonNode> { JsonValue.Create("target example") },
                Properties = new Dictionary<string, IOpenApiSchema>()
                {
                    ["prop1"] = new OpenApiSchema()
                    {
                        Type = JsonSchemaType.String
                    }
                }
            };
            workingDocument.Components.Schemas = new Dictionary<string, IOpenApiSchema>()
            {
                [referenceId] = targetSchema
            };
            workingDocument.Workspace.RegisterComponents(workingDocument);

            // Act
            var schemaReference = new OpenApiSchemaReference(referenceId, workingDocument);

            // Assert - should fallback to target values
            Assert.Equal("Target Title", schemaReference.Title);
            Assert.Equal("Target Description", schemaReference.Description);
            Assert.True(schemaReference.ReadOnly);
            Assert.False(schemaReference.WriteOnly);
            Assert.True(schemaReference.Deprecated);
            Assert.Equal("target default", schemaReference.Default?.GetValue<string>());
            Assert.Single(schemaReference.Examples);
            Assert.Equal("target example", schemaReference.Examples.First()?.GetValue<string>());
        }

        [Fact]
        public void SchemaReferenceExposesMissingPropertiesFromTarget()
        {
            var workingDocument = new OpenApiDocument
            {
                Components = new OpenApiComponents(),
            };
            const string referenceId = "targetSchema";
            workingDocument.Components.Schemas = new Dictionary<string, IOpenApiSchema>
            {
                [referenceId] = new OpenApiSchema
                {
                    Anchor = "root",
                    UnevaluatedProperties = false,
                    ContentEncoding = "base64",
                    ContentMediaType = "application/jwt",
                    ContentSchema = new OpenApiSchema { Type = JsonSchemaType.Array },
                    PropertyNames = new OpenApiSchema { Pattern = "^[a-z]+$" },
                    DependentSchemas = new Dictionary<string, IOpenApiSchema>
                    {
                        ["token"] = new OpenApiSchema { Type = JsonSchemaType.String }
                    },
                    If = new OpenApiSchema { Required = new HashSet<string> { "token" } },
                    Then = new OpenApiSchema { MinProperties = 1 },
                    Else = new OpenApiSchema { MaxProperties = 0 }
                }
            };
            workingDocument.Workspace.RegisterComponents(workingDocument);

            var schemaReference = new OpenApiSchemaReference(referenceId, workingDocument);
            var missingProperties = Assert.IsAssignableFrom<IOpenApiSchemaMissingProperties>(schemaReference);

            Assert.Equal("root", missingProperties.Anchor);
            Assert.False(missingProperties.UnevaluatedProperties);
            Assert.Equal("base64", missingProperties.ContentEncoding);
            Assert.Equal("application/jwt", missingProperties.ContentMediaType);
            Assert.Equal(JsonSchemaType.Array, missingProperties.ContentSchema?.Type);
            Assert.Equal("^[a-z]+$", missingProperties.PropertyNames?.Pattern);
            Assert.Equal(JsonSchemaType.String, missingProperties.DependentSchemas?["token"].Type);
            Assert.NotNull(missingProperties.If?.Required);
            Assert.Contains("token", missingProperties.If.Required);
            Assert.Equal(1, missingProperties.Then?.MinProperties);
            Assert.Equal(0, missingProperties.Else?.MaxProperties);
        }

        [Fact]
        public void SchemaReferenceWithKeywordSiblingsDoesNotOverrideTargetValues()
        {
            var workingDocument = new OpenApiDocument
            {
                Components = new OpenApiComponents(),
            };
            const string referenceId = "targetSchema";
            workingDocument.Components.Schemas = new Dictionary<string, IOpenApiSchema>
            {
                [referenceId] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Format = "target-format",
                    MaxLength = 20,
                    Required = new HashSet<string> { "target" },
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["target"] = new OpenApiSchema { Type = JsonSchemaType.String }
                    },
                    AdditionalPropertiesAllowed = true,
                    ContentEncoding = "gzip",
                    If = new OpenApiSchema { Required = new HashSet<string> { "target" } }
                }
            };
            workingDocument.Workspace.RegisterComponents(workingDocument);

            var schemaReference = new OpenApiSchemaReference(referenceId, workingDocument)
            {
                Type = JsonSchemaType.String,
                Format = "reference-format",
                MaxLength = 10,
                Required = new HashSet<string> { "reference" },
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    ["reference"] = new OpenApiSchema { Type = JsonSchemaType.Integer }
                },
                AdditionalPropertiesAllowed = false,
                ContentEncoding = "base64",
                If = new OpenApiSchema { Required = new HashSet<string> { "reference" } }
            };

            // Non-lossless keywords remain reference-first convenience accessors.
            Assert.Equal(JsonSchemaType.String, schemaReference.Type);
            Assert.Equal("reference-format", schemaReference.Format);
            Assert.Equal(JsonSchemaType.Integer, schemaReference.Properties?["reference"].Type);
            Assert.NotNull(schemaReference.If?.Required);
            Assert.Contains("reference", schemaReference.If.Required);

            // Numeric bounds: stricter value wins (both constraints apply).
            Assert.Equal(10, schemaReference.MaxLength); // Math.Min(20, 10)

            // Required is a mutable sibling collection, not an effective merged set.
            Assert.NotNull(schemaReference.Required);
            Assert.Contains("reference", schemaReference.Required);
            Assert.DoesNotContain("target", schemaReference.Required);

            // AdditionalPropertiesAllowed: false if either says false.
            Assert.False(schemaReference.AdditionalPropertiesAllowed);

            // ContentEncoding is an annotation — sibling value takes precedence.
            Assert.Equal("base64", schemaReference.ContentEncoding);

            // Authored sibling values are still stored on the Reference.
            Assert.Equal(JsonSchemaType.String, schemaReference.Reference.SchemaType);
            Assert.Equal("reference-format", schemaReference.Reference.Format);
            Assert.Equal(10, schemaReference.Reference.MaxLength);
            Assert.Contains("reference", schemaReference.Reference.Required ?? new HashSet<string>());
            Assert.False(schemaReference.Reference.AdditionalPropertiesAllowed ?? true);
        }

        [Fact]
        public void ParseSchemaReferenceWithKeywordSiblingsWorks()
        {
            var jsonContent = @"{
  ""openapi"": ""3.1.0"",
  ""info"": {
    ""title"": ""Test API"",
    ""version"": ""1.0.0""
  },
  ""paths"": {
    ""/test"": {
      ""get"": {
        ""responses"": {
          ""200"": {
            ""description"": ""OK"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""$ref"": ""#/components/schemas/Pet"",
                  ""type"": ""string"",
                  ""format"": ""uuid"",
                  ""maxLength"": 36,
                  ""required"": [""id""],
                  ""properties"": {
                    ""id"": {
                      ""type"": ""string""
                    }
                  },
                  ""additionalProperties"": false,
                  ""contentEncoding"": ""base64"",
                  ""if"": {
                    ""required"": [""id""]
                  }
                }
              }
            }
          }
        }
      }
    }
  },
  ""components"": {
    ""schemas"": {
      ""Pet"": {
        ""type"": ""object"",
        ""format"": ""target-format"",
        ""maxLength"": 10,
        ""additionalProperties"": true
      }
    }
  }
}";

            var readResult = OpenApiDocument.Parse(jsonContent, "json");

            Assert.Empty(readResult.Diagnostic.Errors);
            var schemaReference = Assert.IsType<OpenApiSchemaReference>(readResult.Document?.Paths["/test"].Operations[HttpMethod.Get]
                .Responses["200"].Content["application/json"].Schema);

            // Type/Format: non-lossless convenience getters remain reference-first.
            Assert.Equal(JsonSchemaType.String, schemaReference.Type);
            Assert.Equal("uuid", schemaReference.Format);

            // MaxLength: stricter wins — Math.Min(10, 36) = 10.
            Assert.Equal(10, schemaReference.MaxLength);

            // Required: target has none, sibling has ["id"] → union = ["id"].
            Assert.NotNull(schemaReference.Required);
            Assert.Contains("id", schemaReference.Required);

            // Properties: target has none → sibling fallback.
            Assert.Equal(JsonSchemaType.String, schemaReference.Properties?["id"].Type);

            // AdditionalPropertiesAllowed: target true, sibling false → false.
            Assert.False(schemaReference.AdditionalPropertiesAllowed);

            // If: target has none → sibling fallback.
            Assert.NotNull(schemaReference.If?.Required);
            Assert.Contains("id", schemaReference.If.Required);

            // ContentEncoding is an annotation — sibling value takes precedence.
            Assert.Equal("base64", schemaReference.ContentEncoding);

            // Authored sibling structural values are preserved on the Reference.
            Assert.Equal(JsonSchemaType.String, schemaReference.Reference.SchemaType);
            Assert.Equal("uuid", schemaReference.Reference.Format);
            Assert.Equal(36, schemaReference.Reference.MaxLength);
            Assert.Contains("id", schemaReference.Reference.Required ?? new HashSet<string>());
            Assert.False(schemaReference.Reference.AdditionalPropertiesAllowed ?? true);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SerializeSchemaReferenceAsV31JsonWorks(bool produceTerseOutput)
        {
            // Arrange
            var reference = new OpenApiSchemaReference("Pet", null)
            {
                Title = "Reference Title",
                Description = "Reference Description",
                ReadOnly = true,
                WriteOnly = false,
                Deprecated = true,
                Default = JsonValue.Create("reference default"),
                Examples = new List<JsonNode> { JsonValue.Create("reference example") },
                Extensions = new Dictionary<string, IOpenApiExtension>
                {
                    ["x-custom"] = new JsonNodeExtension(JsonValue.Create("custom value"))
                }
            };

            var outputStringWriter = new StringWriter(CultureInfo.InvariantCulture);
            var writer = new OpenApiJsonWriter(outputStringWriter, new OpenApiJsonWriterSettings { Terse = produceTerseOutput });

            // Act
            reference.SerializeAsV31(writer);
            await writer.FlushAsync();

            // Assert
            await Verifier.Verify(outputStringWriter).UseParameters(produceTerseOutput);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SerializeSchemaReferenceAsV32JsonWorks(bool produceTerseOutput)
        {
            // Arrange
            var reference = new OpenApiSchemaReference("Pet", null)
            {
                Title = "Reference Title",
                Description = "Reference Description",
                ReadOnly = true,
                WriteOnly = false,
                Deprecated = true,
                Default = JsonValue.Create("reference default"),
                Examples = new List<JsonNode> { JsonValue.Create("reference example") },
                Extensions = new Dictionary<string, IOpenApiExtension>
                {
                    ["x-custom"] = new JsonNodeExtension(JsonValue.Create("custom value"))
                }
            };

            var outputStringWriter = new StringWriter(CultureInfo.InvariantCulture);
            var writer = new OpenApiJsonWriter(outputStringWriter, new OpenApiJsonWriterSettings { Terse = produceTerseOutput });

            // Act
            reference.SerializeAsV32(writer);
            await writer.FlushAsync();

            // Assert
            await Verifier.Verify(outputStringWriter).UseParameters(produceTerseOutput);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SerializeSchemaReferenceAsV3JsonWorks(bool produceTerseOutput)
        {
            // Arrange - Extensions should NOT appear in v3.0 output
            var reference = new OpenApiSchemaReference("Pet", null)
            {
                Title = "Reference Title",
                Description = "Reference Description",
                ReadOnly = true,
                WriteOnly = false,
                Deprecated = true,
                Default = JsonValue.Create("reference default"),
                Examples = new List<JsonNode> { JsonValue.Create("reference example") },
                Extensions = new Dictionary<string, IOpenApiExtension>
                {
                    ["x-custom"] = new JsonNodeExtension(JsonValue.Create("custom value"))
                }
            };

            var outputStringWriter = new StringWriter(CultureInfo.InvariantCulture);
            var writer = new OpenApiJsonWriter(outputStringWriter, new OpenApiJsonWriterSettings { Terse = produceTerseOutput });

            // Act
            reference.SerializeAsV3(writer);
            await writer.FlushAsync();

            // Assert
            await Verifier.Verify(outputStringWriter).UseParameters(produceTerseOutput);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SerializeSchemaReferenceAsV2JsonWorks(bool produceTerseOutput)
        {
            // Arrange - Extensions should NOT appear in v2 output
            var reference = new OpenApiSchemaReference("Pet", null)
            {
                Title = "Reference Title",
                Description = "Reference Description",
                ReadOnly = true,
                WriteOnly = false,
                Deprecated = true,
                Default = JsonValue.Create("reference default"),
                Examples = new List<JsonNode> { JsonValue.Create("reference example") },
                Extensions = new Dictionary<string, IOpenApiExtension>
                {
                    ["x-custom"] = new JsonNodeExtension(JsonValue.Create("custom value"))
                }
            };

            var outputStringWriter = new StringWriter(CultureInfo.InvariantCulture);
            var writer = new OpenApiJsonWriter(outputStringWriter, new OpenApiJsonWriterSettings { Terse = produceTerseOutput });

            // Act
            reference.SerializeAsV2(writer);
            await writer.FlushAsync();

            // Assert
            await Verifier.Verify(outputStringWriter).UseParameters(produceTerseOutput);
        }

        [Fact]
        public void ParseSchemaReferenceWithAnnotationsWorks()
        {
            // Arrange
            var jsonContent = @"{
  ""openapi"": ""3.1.0"",
  ""info"": {
    ""title"": ""Test API"",
    ""version"": ""1.0.0""
  },
  ""paths"": {
    ""/test"": {
      ""get"": {
        ""responses"": {
          ""200"": {
            ""description"": ""OK"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""$ref"": ""#/components/schemas/Pet"",
                  ""title"": ""Pet Response Schema"",
                  ""description"": ""A pet object returned from the API"",
                  ""summary"": ""Pet Response"",
                  ""deprecated"": true,
                  ""readOnly"": true,
                  ""writeOnly"": false,
                  ""default"": {""name"": ""default pet""},
                  ""examples"": [{""name"": ""example pet""}]
                }
              }
            }
          }
        }
      }
    }
  },
  ""components"": {
    ""schemas"": {
      ""Pet"": {
        ""type"": ""object"",
        ""title"": ""Original Pet Title"",
        ""description"": ""Original Pet Description"",
        ""properties"": {
          ""name"": {
            ""type"": ""string""
          }
        }
      }
    }
  }
}";

            // Act
            var readResult = OpenApiDocument.Parse(jsonContent, "json");
            var document = readResult.Document;

            // Assert
            Assert.NotNull(document);
            Assert.Empty(readResult.Diagnostic.Errors);

            var schema = document.Paths["/test"].Operations[HttpMethod.Get]
                .Responses["200"].Content["application/json"].Schema;

            Assert.IsType<OpenApiSchemaReference>(schema);
            var schemaRef = (OpenApiSchemaReference)schema;

            // Test that reference annotations override target values
            Assert.Equal("Pet Response Schema", schemaRef.Title);
            Assert.Equal("A pet object returned from the API", schemaRef.Description);
            Assert.True(schemaRef.Deprecated);
            Assert.True(schemaRef.ReadOnly);
            Assert.False(schemaRef.WriteOnly);
            Assert.NotNull(schemaRef.Default);
            Assert.Single(schemaRef.Examples);

            // Test that target schema still has original values
            var targetSchema = schemaRef.Target;
            Assert.NotNull(targetSchema);
            Assert.Equal("Original Pet Title", targetSchema.Title);
            Assert.Equal("Original Pet Description", targetSchema.Description);
        }

        [Fact]
        public void ParseSchemaReferenceWithExtensionsWorks()
        {
            // Arrange
            var jsonContent = @"{
  ""openapi"": ""3.1.0"",
  ""info"": {
    ""title"": ""Test API"",
    ""version"": ""1.0.0""
  },
  ""paths"": {
    ""/test"": {
      ""get"": {
        ""responses"": {
          ""200"": {
            ""description"": ""OK"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""$ref"": ""#/components/schemas/Pet"",
                  ""description"": ""A pet object"",
                  ""x-custom-extension"": ""custom value"",
                  ""x-another-extension"": 42
                }
              }
            }
          }
        }
      }
    }
  },
  ""components"": {
    ""schemas"": {
      ""Pet"": {
        ""type"": ""object"",
        ""properties"": {
          ""name"": {
            ""type"": ""string""
          }
        }
      }
    }
  }
}";

            // Act
            var readResult = OpenApiDocument.Parse(jsonContent, "json");
            var document = readResult.Document;

            // Assert
            Assert.NotNull(document);
            Assert.Empty(readResult.Diagnostic.Errors);

            var schema = document.Paths["/test"].Operations[HttpMethod.Get]
                .Responses["200"].Content["application/json"].Schema;

            Assert.IsType<OpenApiSchemaReference>(schema);
            var schemaRef = (OpenApiSchemaReference)schema;

            // Test that reference-level extensions are parsed
            Assert.NotNull(schemaRef.Extensions);
            Assert.Contains("x-custom-extension", schemaRef.Extensions.Keys);
            Assert.Contains("x-another-extension", schemaRef.Extensions.Keys);
        }

        [Fact]
        public async Task SchemaReferenceExtensionsNotWrittenInV30()
        {
            // Arrange
            var reference = new OpenApiSchemaReference("Pet", null)
            {
                Description = "Local description",
                Extensions = new Dictionary<string, IOpenApiExtension>
                {
                    ["x-custom"] = new JsonNodeExtension(JsonValue.Create("custom value"))
                }
            };

            var outputStringWriter = new StringWriter(CultureInfo.InvariantCulture);
            var writer = new OpenApiJsonWriter(outputStringWriter, new OpenApiJsonWriterSettings { Terse = true });

            // Act
            reference.SerializeAsV3(writer);
            await writer.FlushAsync();
            var output = outputStringWriter.ToString();

            // Assert: In v3.0, ONLY $ref should appear - no description, no extensions
            Assert.Equal(@"{""$ref"":""#/components/schemas/Pet""}", output);
        }

        [Fact]
        public async Task SchemaReferenceExtensionsNotWrittenInV2()
        {
            // Arrange
            var reference = new OpenApiSchemaReference("Pet", null)
            {
                Description = "Local description",
                Extensions = new Dictionary<string, IOpenApiExtension>
                {
                    ["x-custom"] = new JsonNodeExtension(JsonValue.Create("custom value"))
                }
            };

            var outputStringWriter = new StringWriter(CultureInfo.InvariantCulture);
            var writer = new OpenApiJsonWriter(outputStringWriter, new OpenApiJsonWriterSettings { Terse = true });

            // Act
            reference.SerializeAsV2(writer);
            await writer.FlushAsync();
            var output = outputStringWriter.ToString();

            // Assert: In v2, ONLY $ref should appear - no description, no extensions
            Assert.Equal(@"{""$ref"":""#/definitions/Pet""}", output);
        }

        [Fact]
        public void StructuralSiblingDoesNotRelaxTargetAssertion()
        {
            // Per JSON Schema 2020-12 (and handrews' comment on #2919):
            // assertion keywords alongside $ref MUST be evaluated independently.
            // A sibling minProperties: 2 does NOT relax a target minProperties: 5.
            var workingDocument = new OpenApiDocument
            {
                Components = new OpenApiComponents(),
            };
            workingDocument.Components.Schemas = new Dictionary<string, IOpenApiSchema>
            {
                ["Target"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    MinProperties = 5
                }
            };
            workingDocument.Workspace.RegisterComponents(workingDocument);

            var schemaRef = new OpenApiSchemaReference("Target", workingDocument)
            {
                MinProperties = 2
            };

            // Math.Max(5, 2) = 5 — the stricter constraint applies.
            Assert.Equal(5, schemaRef.MinProperties);
            // Sibling value is preserved on the Reference.
            Assert.Equal(2, schemaRef.Reference.MinProperties);
        }

        [Fact]
        public void PropertiesSiblingRemainsVisibleOnConvenienceGetter()
        {
            // Per handrews: applicators like "properties" MUST be evaluated independently.
            // A sibling properties keyword does not replace the target's properties.
            var workingDocument = new OpenApiDocument
            {
                Components = new OpenApiComponents(),
            };
            workingDocument.Components.Schemas = new Dictionary<string, IOpenApiSchema>
            {
                ["Target"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["name"] = new OpenApiSchema { Type = JsonSchemaType.String }
                    }
                }
            };
            workingDocument.Workspace.RegisterComponents(workingDocument);

            var schemaRef = new OpenApiSchemaReference("Target", workingDocument)
            {
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    ["id"] = new OpenApiSchema { Type = JsonSchemaType.Integer }
                }
            };

            // Properties cannot be losslessly collapsed when both sides define the same property.
            // Keep the convenience getter reference-first so the sibling applicator remains visible.
            Assert.Contains("id", schemaRef.Properties?.Keys ?? new Dictionary<string, IOpenApiSchema>().Keys);
            Assert.DoesNotContain("name", schemaRef.Properties?.Keys ?? new Dictionary<string, IOpenApiSchema>().Keys);
            // Target value remains available separately.
            Assert.Contains("name", schemaRef.Target?.Properties?.Keys ?? new Dictionary<string, IOpenApiSchema>().Keys);
            // Sibling value is preserved on the Reference.
            Assert.Contains("id", schemaRef.Reference.Properties?.Keys ?? new Dictionary<string, IOpenApiSchema>().Keys);
        }

        [Fact]
        public void ContainsCountKeywordsStayAlignedWithSiblingContainsApplicator()
        {
            // contains/minContains/maxContains communicate as a group. Since contains
            // cannot be losslessly collapsed, the related count keywords remain
            // reference-first too.
            var workingDocument = new OpenApiDocument
            {
                Components = new OpenApiComponents(),
            };
            workingDocument.Components.Schemas = new Dictionary<string, IOpenApiSchema>
            {
                ["Target"] = new OpenApiSchema
                {
                    Contains = new OpenApiSchema { Type = JsonSchemaType.String },
                    MinContains = 5,
                    MaxContains = 10
                }
            };
            workingDocument.Workspace.RegisterComponents(workingDocument);

            var schemaRef = new OpenApiSchemaReference("Target", workingDocument)
            {
                Contains = new OpenApiSchema { Type = JsonSchemaType.Number },
                MinContains = 2,
                MaxContains = 3
            };

            Assert.Equal(JsonSchemaType.Number, schemaRef.Contains?.Type);
            Assert.Equal(2U, schemaRef.MinContains);
            Assert.Equal(3U, schemaRef.MaxContains);
            Assert.Equal(JsonSchemaType.String, schemaRef.Target is IOpenApiSchemaMissingProperties target ? target.Contains?.Type : null);
            Assert.Equal(5U, schemaRef.Target is IOpenApiSchemaMissingProperties targetMin ? targetMin.MinContains : null);
            Assert.Equal(10U, schemaRef.Target is IOpenApiSchemaMissingProperties targetMax ? targetMax.MaxContains : null);
        }

        [Fact]
        public void ReadOnlyIsTrueIfEitherSourceIsTrue()
        {
            // Per handrews on #2919: if anything marks an instance location
            // readOnly: true then it SHOULD be considered read-only even if
            // some other subschema marks it readOnly: false.
            var workingDocument = new OpenApiDocument
            {
                Components = new OpenApiComponents(),
            };
            workingDocument.Components.Schemas = new Dictionary<string, IOpenApiSchema>
            {
                ["TargetRO"] = new OpenApiSchema { ReadOnly = true },
                ["TargetNotRO"] = new OpenApiSchema { ReadOnly = false },
            };
            workingDocument.Workspace.RegisterComponents(workingDocument);

            // Target readOnly=true, sibling not set → true
            var ref1 = new OpenApiSchemaReference("TargetRO", workingDocument);
            Assert.True(ref1.ReadOnly);

            // Target readOnly=true, sibling readOnly=false → still true
            var ref2 = new OpenApiSchemaReference("TargetRO", workingDocument) { ReadOnly = false };
            Assert.True(ref2.ReadOnly);

            // Target readOnly=false, sibling readOnly=true → true
            var ref3 = new OpenApiSchemaReference("TargetNotRO", workingDocument) { ReadOnly = true };
            Assert.True(ref3.ReadOnly);

            // Target readOnly=false, sibling not set → false
            var ref4 = new OpenApiSchemaReference("TargetNotRO", workingDocument);
            Assert.False(ref4.ReadOnly);
        }

        [Fact]
        public void WriteOnlyIsTrueIfEitherSourceIsTrue()
        {
            var workingDocument = new OpenApiDocument
            {
                Components = new OpenApiComponents(),
            };
            workingDocument.Components.Schemas = new Dictionary<string, IOpenApiSchema>
            {
                ["TargetWO"] = new OpenApiSchema { WriteOnly = true },
                ["TargetNotWO"] = new OpenApiSchema { WriteOnly = false },
            };
            workingDocument.Workspace.RegisterComponents(workingDocument);

            var ref1 = new OpenApiSchemaReference("TargetWO", workingDocument);
            Assert.True(ref1.WriteOnly);

            var ref2 = new OpenApiSchemaReference("TargetWO", workingDocument) { WriteOnly = false };
            Assert.True(ref2.WriteOnly);

            var ref3 = new OpenApiSchemaReference("TargetNotWO", workingDocument) { WriteOnly = true };
            Assert.True(ref3.WriteOnly);

            var ref4 = new OpenApiSchemaReference("TargetNotWO", workingDocument);
            Assert.False(ref4.WriteOnly);
        }

        [Fact]
        public async Task ParseSchemaReferenceSiblingSerializationRoundTrip()
        {
            // Verifies that $ref siblings survive parse → serialize round-trip
            // even though structural getters now resolve from Target.
            var jsonContent = @"{
  ""openapi"": ""3.1.0"",
  ""info"": {
    ""title"": ""Test API"",
    ""version"": ""1.0.0""
  },
  ""paths"": {
    ""/test"": {
      ""get"": {
        ""responses"": {
          ""200"": {
            ""description"": ""OK"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""$ref"": ""#/components/schemas/Target"",
                  ""minProperties"": 2,
                  ""description"": ""sibling description""
                }
              }
            }
          }
        }
      }
    }
  },
  ""components"": {
    ""schemas"": {
      ""Target"": {
        ""type"": ""object"",
        ""minProperties"": 5
      }
    }
  }
}";

            var readResult = OpenApiDocument.Parse(jsonContent, "json");
            Assert.Empty(readResult.Diagnostic.Errors);

            var doc = readResult.Document!;
            var schemaRef = Assert.IsType<OpenApiSchemaReference>(doc.Paths["/test"].Operations[HttpMethod.Get]
                .Responses["200"].Content["application/json"].Schema);

            // Structural getter resolves from Target.
            Assert.Equal(5, schemaRef.MinProperties);
            // Annotation getter resolves from sibling.
            Assert.Equal("sibling description", schemaRef.Description);
            // Sibling structural value preserved on Reference.
            Assert.Equal(2, schemaRef.Reference.MinProperties);

            // Serialize back and verify siblings round-trip on the $ref wire format.
            using var outputStringWriter = new StringWriter(CultureInfo.InvariantCulture);
            var writer = new OpenApiJsonWriter(outputStringWriter, new OpenApiJsonWriterSettings { Terse = true });
            schemaRef.SerializeAsV31(writer);
            await writer.FlushAsync();
            var output = outputStringWriter.ToString();

            Assert.Contains("\"$ref\":\"#/components/schemas/Target\"", output);
            Assert.Contains("\"minProperties\":2", output);
            Assert.Contains("\"description\":\"sibling description\"", output);
        }

        [Fact]
        public void StricterSiblingEnforcesTighterBound()
        {
            // A sibling minProperties: 7 is stricter than target minProperties: 5.
            // Both apply, so the effective constraint is Math.Max(5, 7) = 7.
            var workingDocument = new OpenApiDocument
            {
                Components = new OpenApiComponents(),
            };
            workingDocument.Components.Schemas = new Dictionary<string, IOpenApiSchema>
            {
                ["Target"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    MinProperties = 5,
                    MaxProperties = 20
                }
            };
            workingDocument.Workspace.RegisterComponents(workingDocument);

            var schemaRef = new OpenApiSchemaReference("Target", workingDocument)
            {
                MinProperties = 7,
                MaxProperties = 10
            };

            Assert.Equal(7, schemaRef.MinProperties);  // Math.Max(5, 7)
            Assert.Equal(10, schemaRef.MaxProperties); // Math.Min(20, 10)
        }

        [Fact]
        public void RawNumericBoundSiblingsRemainVisible()
        {
            // maximum/minimum are stored as raw strings to preserve JSON number precision.
            // Do not parse and compare them here; keep sibling-authored values visible.
            var workingDocument = new OpenApiDocument
            {
                Components = new OpenApiComponents(),
            };
            workingDocument.Components.Schemas = new Dictionary<string, IOpenApiSchema>
            {
                ["Target"] = new OpenApiSchema
                {
                    Maximum = "100",
                    Minimum = "1"
                }
            };
            workingDocument.Workspace.RegisterComponents(workingDocument);

            var schemaRef = new OpenApiSchemaReference("Target", workingDocument)
            {
                Maximum = "10",
                Minimum = "5"
            };

            Assert.Equal("10", schemaRef.Maximum);
            Assert.Equal("5", schemaRef.Minimum);
            Assert.Equal("100", schemaRef.Target?.Maximum);
            Assert.Equal("1", schemaRef.Target?.Minimum);

            var relaxedSibling = new OpenApiSchemaReference("Target", workingDocument)
            {
                Maximum = "200",
                Minimum = "0"
            };

            Assert.Equal("200", relaxedSibling.Maximum);
            Assert.Equal("0", relaxedSibling.Minimum);
        }

        [Fact]
        public void RequiredSiblingCollectionRemainsMutableAndStable()
        {
            var workingDocument = new OpenApiDocument
            {
                Components = new OpenApiComponents(),
            };
            workingDocument.Components.Schemas = new Dictionary<string, IOpenApiSchema>
            {
                ["Target"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Required = new HashSet<string> { "name" }
                }
            };
            workingDocument.Workspace.RegisterComponents(workingDocument);

            var schemaRef = new OpenApiSchemaReference("Target", workingDocument)
            {
                Required = new HashSet<string> { "id" }
            };

            Assert.NotNull(schemaRef.Required);
            Assert.Contains("id", schemaRef.Required);
            Assert.DoesNotContain("name", schemaRef.Required);

            schemaRef.Required.Add("email");

            Assert.Contains("email", schemaRef.Required);
            Assert.Contains("email", schemaRef.Reference.Required ?? new HashSet<string>());
            Assert.DoesNotContain("email", schemaRef.Target?.Required ?? new HashSet<string>());
        }

        [Fact]
        public void AdditionalPropertiesAllowedFalseIfEitherFalse()
        {
            var workingDocument = new OpenApiDocument
            {
                Components = new OpenApiComponents(),
            };
            workingDocument.Components.Schemas = new Dictionary<string, IOpenApiSchema>
            {
                ["TargetAPA"] = new OpenApiSchema { AdditionalPropertiesAllowed = true },
                ["TargetNoAPA"] = new OpenApiSchema { AdditionalPropertiesAllowed = false },
            };
            workingDocument.Workspace.RegisterComponents(workingDocument);

            // Target true, sibling false → false.
            var ref1 = new OpenApiSchemaReference("TargetAPA", workingDocument) { AdditionalPropertiesAllowed = false };
            Assert.False(ref1.AdditionalPropertiesAllowed);

            // Target false, sibling not set → false.
            var ref2 = new OpenApiSchemaReference("TargetNoAPA", workingDocument);
            Assert.False(ref2.AdditionalPropertiesAllowed);

            // Target true, sibling not set → true.
            var ref3 = new OpenApiSchemaReference("TargetAPA", workingDocument);
            Assert.True(ref3.AdditionalPropertiesAllowed);
        }

        [Fact]
        public void UnresolvedReferenceSiblingValuesAreReadable()
        {
            // When there is no host document, Target is null.
            // Structural getters must fall back to the authored sibling values.
            var schemaRef = new OpenApiSchemaReference("Pet", null)
            {
                Type = JsonSchemaType.String,
                Format = "uuid",
                MaxLength = 36,
                Required = new HashSet<string> { "id" }
            };

            Assert.Equal(JsonSchemaType.String, schemaRef.Type);
            Assert.Equal("uuid", schemaRef.Format);
            Assert.Equal(36, schemaRef.MaxLength);
            Assert.Contains("id", schemaRef.Required ?? new HashSet<string>());
        }

        [Fact]
        public void AllOfSiblingCollectionRemainsMutableAndStable()
        {
            var workingDocument = new OpenApiDocument
            {
                Components = new OpenApiComponents(),
            };
            workingDocument.Components.Schemas = new Dictionary<string, IOpenApiSchema>
            {
                ["Target"] = new OpenApiSchema
                {
                    AllOf = new List<IOpenApiSchema>
                    {
                        new OpenApiSchema { Type = JsonSchemaType.Object }
                    }
                }
            };
            workingDocument.Workspace.RegisterComponents(workingDocument);

            var schemaRef = new OpenApiSchemaReference("Target", workingDocument)
            {
                AllOf = new List<IOpenApiSchema>
                {
                    new OpenApiSchema { Type = JsonSchemaType.String }
                }
            };

            Assert.NotNull(schemaRef.AllOf);
            Assert.Single(schemaRef.AllOf);
            Assert.Equal(JsonSchemaType.String, schemaRef.AllOf[0].Type);

            schemaRef.AllOf.Add(new OpenApiSchema { Type = JsonSchemaType.Boolean });

            Assert.Equal(2, schemaRef.AllOf.Count);
            Assert.Equal(2, schemaRef.Reference.AllOf?.Count);
            Assert.Single(schemaRef.Target?.AllOf ?? []);
        }
    }
}
