namespace TickestPristine.ArchitectureTests.Conventions;

/// <summary>
/// Lê os arquivos reais do repositório para os testes de produção. A raiz é o diretório que contém
/// <c>TickestPristine.slnx</c>, achado subindo a partir do diretório do teste. Ficam fora as pastas
/// <c>bin</c> e <c>obj</c> e as pastas que começam com ponto (<c>.git</c>, <c>.vs</c>, <c>.claude</c>),
/// que não contêm código do projeto.
/// </summary>
internal static class RepositoryFiles
{
    private const string SolutionFileName = "TickestPristine.slnx";

    public static string Root { get; } = FindRoot();

    public static IReadOnlyList<SourceFile> Read(string relativeDirectory, Func<string, bool> fileNameFilter)
    {
        string directory = Path.Combine(Root, relativeDirectory);

        return EnumerateFiles(directory)
            .Where(path => fileNameFilter(Path.GetFileName(path)))
            .Select(path => new SourceFile(
                Path.GetRelativePath(Root, path).Replace('\\', '/'),
                File.ReadAllText(path)))
            .ToList();
    }

    private static IEnumerable<string> EnumerateFiles(string directory)
    {
        foreach (string file in Directory.EnumerateFiles(directory))
        {
            yield return file;
        }

        foreach (string subdirectory in Directory.EnumerateDirectories(directory))
        {
            string name = Path.GetFileName(subdirectory);

            if (name is "bin" or "obj" || name.StartsWith('.'))
            {
                continue;
            }

            foreach (string file in EnumerateFiles(subdirectory))
            {
                yield return file;
            }
        }
    }

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException(
                $"Raiz do repositório não encontrada: nenhum {SolutionFileName} acima de {AppContext.BaseDirectory}.");
    }
}
