using System.Text.RegularExpressions;

namespace TickestPristine.ArchitectureTests.Conventions;

/// <summary>
/// Confere, classe por classe, que cada endpoint (classe que implementa <c>IEndpoint</c>) declara a tag com uma
/// constante de <c>Tags</c> (<c>.WithTags(Tags.…)</c>), fora de comentários e strings; tag literal não atende.
/// Classes que não implementam <c>IEndpoint</c> não são avaliadas.
/// </summary>
internal static partial class EndpointTagChecker
{
    public static IReadOnlyList<EndpointClass> FindEndpoints(SourceFile file)
    {
        string code = CSharpSource.MaskCommentsAndStrings(file.Content);
        var endpoints = new List<EndpointClass>();

        foreach (Match match in ClassDeclarationRegex().Matches(code))
        {
            if (!EndpointInterfaceRegex().IsMatch(match.Groups["header"].Value))
            {
                continue;
            }

            int openBrace = match.Index + match.Length - 1;
            int closeBrace = CSharpSource.FindClosingBrace(code, openBrace);

            endpoints.Add(new EndpointClass(
                file.Path,
                match.Groups["name"].Value,
                CSharpSource.GetLine(code, match.Index),
                code[openBrace..(closeBrace + 1)]));
        }

        return endpoints;
    }

    public static IReadOnlyList<Violation> Check(IEnumerable<SourceFile> files) =>
        files.SelectMany(FindEndpoints).SelectMany(Check).ToList();

    public static IReadOnlyList<Violation> Check(SourceFile file) =>
        FindEndpoints(file).SelectMany(Check).ToList();

    private static IEnumerable<Violation> Check(EndpointClass endpoint)
    {
        if (LiteralTagRegex().IsMatch(endpoint.Body))
        {
            yield return new Violation(
                endpoint.Path, endpoint.Line, endpoint.Name, "tag literal: use uma constante de Tags (.WithTags(Tags.…))");
        }
        else if (!ConstantTagRegex().IsMatch(endpoint.Body))
        {
            yield return new Violation(
                endpoint.Path, endpoint.Line, endpoint.Name, "sem tag: falta .WithTags(Tags.…)");
        }
    }

    [GeneratedRegex(
        @"\b(?:class|record)\s+(?<name>\w+)(?<header>[^{;]*)\{",
        RegexOptions.None,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex ClassDeclarationRegex();

    [GeneratedRegex(@"\bIEndpoint\b", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EndpointInterfaceRegex();

    [GeneratedRegex(@"\.WithTags\s*\(\s*Tags\.\w+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ConstantTagRegex();

    [GeneratedRegex(@"\.WithTags\s*\(\s*[@$]*""", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LiteralTagRegex();
}

/// <summary>
/// Classe <c>IEndpoint</c> encontrada num arquivo, com o corpo já sem comentários e strings.
/// </summary>
internal sealed record EndpointClass(string Path, string Name, int Line, string Body);
