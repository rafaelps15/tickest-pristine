using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;

namespace TickestPristine.IntegrationTests.Swagger;

public sealed class SwaggerTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const string DocumentRoute = "swagger/v1/swagger.json";
    private const string ApprovedSnapshotFile = "openapi-v1.approved.json";
    private const string UpdateSnapshotVariable = "UPDATE_OPENAPI_SNAPSHOT";
    private const int MaxReportedDifferences = 100;

    private static readonly JsonSerializerOptions SnapshotWriteOptions = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    [Fact]
    public async Task GetDocument_Should_RequireTokenOnlyOnProtectedEndpoints()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("swagger/v1/swagger.json");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement paths = document.RootElement.GetProperty("paths");

        JsonElement login = paths.GetProperty("/users/login").GetProperty("post");
        login.TryGetProperty("security", out _).ShouldBeFalse();

        JsonElement getUsers = paths.GetProperty("/users").GetProperty("get");
        getUsers.GetProperty("security")[0].TryGetProperty("Bearer", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task GetDocument_Should_MatchApprovedSnapshot_WhenApiIsBuilt()
    {
        // Arrange
        string approvedPath = ApprovedSnapshotPath();
        JsonNode current = await GetNormalizedDocumentAsync();

        if (Environment.GetEnvironmentVariable(UpdateSnapshotVariable) == "1")
        {
            await File.WriteAllTextAsync(approvedPath, current.ToJsonString(SnapshotWriteOptions) + "\n");
        }

        // Act
        List<string> differences = File.Exists(approvedPath)
            ? Differences(JsonNode.Parse(await File.ReadAllTextAsync(approvedPath))!, current)
            : [$"arquivo de referência ausente: {approvedPath}"];

        // Assert
        ShouldMatchApprovedSnapshot(differences);
    }

    [Fact]
    public async Task SnapshotCheck_Should_FailWithPath_WhenConsumedFieldIsRenamed()
    {
        // Arrange
        JsonNode approved = await GetNormalizedDocumentAsync();
        JsonNode current = approved.DeepClone();
        string schemaId = CreateTicketRequestSchemaId(current);
        JsonObject properties = current["components"]!["schemas"]![schemaId]!["properties"]!.AsObject();
        JsonNode? title = properties["title"];
        properties.Remove("title");
        properties["subject"] = title;

        string schemaPath = $"$.components.schemas[\"{schemaId}\"].properties";

        // Act
        List<string> differences = Differences(approved, current);

        // Assert
        ShouldAssertException failure = Should.Throw<ShouldAssertException>(() => ShouldMatchApprovedSnapshot(differences));
        failure.Message.ShouldContain($"{schemaPath}.title: removido");
        failure.Message.ShouldContain($"{schemaPath}.subject: adicionado");
    }

    private async Task<JsonNode> GetNormalizedDocumentAsync()
    {
        HttpResponseMessage response = await HttpClient.GetAsync(DocumentRoute);
        response.EnsureSuccessStatusCode();

        JsonNode document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        return Sorted(document)!;
    }

    private static JsonNode? Sorted(JsonNode? node)
    {
        return node switch
        {
            null => null,
            JsonObject json => new JsonObject(json
                .OrderBy(property => property.Key, StringComparer.Ordinal)
                .Select(property => KeyValuePair.Create(property.Key, Sorted(property.Value)))),
            JsonArray array => new JsonArray(array.Select(Sorted).ToArray()),
            _ => node.DeepClone(),
        };
    }

    private static List<string> Differences(JsonNode approved, JsonNode current)
    {
        var differences = new List<string>();
        CollectDifferences(approved, current, "$", differences);

        return differences;
    }

    private static void CollectDifferences(JsonNode? approved, JsonNode? current, string path, List<string> differences)
    {
        if (approved is JsonObject approvedObject && current is JsonObject currentObject)
        {
            IEnumerable<string> keys = approvedObject.Select(property => property.Key)
                .Union(currentObject.Select(property => property.Key))
                .Order(StringComparer.Ordinal);

            foreach (string key in keys)
            {
                string child = ChildPath(path, key);

                if (!currentObject.ContainsKey(key))
                {
                    differences.Add($"{child}: removido");
                }
                else if (!approvedObject.ContainsKey(key))
                {
                    differences.Add($"{child}: adicionado");
                }
                else
                {
                    CollectDifferences(approvedObject[key], currentObject[key], child, differences);
                }
            }

            return;
        }

        if (approved is JsonArray approvedArray && current is JsonArray currentArray)
        {
            for (int index = 0; index < Math.Max(approvedArray.Count, currentArray.Count); index++)
            {
                string child = $"{path}[{index}]";

                if (index >= currentArray.Count)
                {
                    differences.Add($"{child}: removido");
                }
                else if (index >= approvedArray.Count)
                {
                    differences.Add($"{child}: adicionado");
                }
                else
                {
                    CollectDifferences(approvedArray[index], currentArray[index], child, differences);
                }
            }

            return;
        }

        if (!JsonNode.DeepEquals(approved, current))
        {
            differences.Add($"{path}: era {approved?.ToJsonString() ?? "null"}, agora {current?.ToJsonString() ?? "null"}");
        }
    }

    private static string ChildPath(string path, string key)
    {
        return key.All(character => char.IsAsciiLetterOrDigit(character) || character == '_')
            ? $"{path}.{key}"
            : $"{path}[\"{key}\"]";
    }

    private static void ShouldMatchApprovedSnapshot(List<string> differences)
    {
        string reported = string.Join(Environment.NewLine, differences.Take(MaxReportedDifferences));
        string remaining = differences.Count > MaxReportedDifferences
            ? $"{Environment.NewLine}... e mais {differences.Count - MaxReportedDifferences} diferenças"
            : string.Empty;

        differences.ShouldBeEmpty(
            $"O documento OpenAPI difere do contrato aprovado ({ApprovedSnapshotFile}). Se a mudança for intencional, "
            + $"rode os testes com {UpdateSnapshotVariable}=1 e revise o diff do arquivo.{Environment.NewLine}{reported}{remaining}");
    }

    private static string CreateTicketRequestSchemaId(JsonNode document)
    {
        JsonObject content = document["paths"]!["/tickets"]!["post"]!["requestBody"]!["content"]!.AsObject();
        string reference = content.GetAt(0).Value!["schema"]!["$ref"]!.GetValue<string>();

        return reference["#/components/schemas/".Length..];
    }

    private static string ApprovedSnapshotPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TickestPristine.slnx")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull();

        return Path.Combine(directory.FullName, "tests", "TickestPristine.IntegrationTests", "Swagger", ApprovedSnapshotFile);
    }
}
