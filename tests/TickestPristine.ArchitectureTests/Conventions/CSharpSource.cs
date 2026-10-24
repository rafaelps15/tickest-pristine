namespace TickestPristine.ArchitectureTests.Conventions;

/// <summary>
/// Prepara texto C# para a busca de padrões: troca por espaços o conteúdo dos comentários de linha
/// (<c>//</c>, <c>///</c>) e de bloco (<c>/* */</c>) e, se pedido, o conteúdo de strings e literais de caractere,
/// preservando as quebras de linha para que o número da linha continue certo. É uma varredura de texto,
/// não análise semântica: strings brutas (<c>"""</c>) e interpoladas com aspas aninhadas não são tratadas.
/// </summary>
internal static class CSharpSource
{
    public static string MaskComments(string content) => Mask(content, maskStrings: false);

    public static string MaskCommentsAndStrings(string content) => Mask(content, maskStrings: true);

    public static int GetLine(string content, int index)
    {
        int line = 1;

        for (int i = 0; i < index && i < content.Length; i++)
        {
            if (content[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    /// <summary>
    /// Devolve o índice da chave que fecha a chave aberta em <paramref name="openIndex"/>, ou o fim do texto.
    /// O texto deve vir de <see cref="MaskCommentsAndStrings"/>, para que chaves em comentários e strings não contem.
    /// </summary>
    public static int FindClosingBrace(string maskedContent, int openIndex)
    {
        int depth = 0;

        for (int i = openIndex; i < maskedContent.Length; i++)
        {
            if (maskedContent[i] == '{')
            {
                depth++;
            }
            else if (maskedContent[i] == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return maskedContent.Length - 1;
    }

    private static string Mask(string content, bool maskStrings)
    {
        char[] result = content.ToCharArray();
        int i = 0;

        while (i < content.Length)
        {
            char current = content[i];
            char next = i + 1 < content.Length ? content[i + 1] : '\0';

            if (current == '/' && next == '/')
            {
                int end = content.IndexOf('\n', i);
                i = Blank(result, i, end < 0 ? content.Length : end);
            }
            else if (current == '/' && next == '*')
            {
                int end = content.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = Blank(result, i, end < 0 ? content.Length : end + 2);
            }
            else if (current is '"' or '\'')
            {
                int end = FindLiteralEnd(content, i, current == '"' && IsVerbatim(content, i));

                if (maskStrings)
                {
                    Blank(result, i + 1, Math.Max(i + 1, end - 1));
                }

                i = end;
            }
            else
            {
                i++;
            }
        }

        return new string(result);
    }

    private static int Blank(char[] content, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            if (content[i] is not ('\r' or '\n'))
            {
                content[i] = ' ';
            }
        }

        return end;
    }

    private static bool IsVerbatim(string content, int quoteIndex) =>
        quoteIndex > 0 &&
        (content[quoteIndex - 1] == '@' ||
         quoteIndex > 1 && content[quoteIndex - 1] == '$' && content[quoteIndex - 2] == '@');

    private static int FindLiteralEnd(string content, int start, bool verbatim)
    {
        char quote = content[start];
        int i = start + 1;

        while (i < content.Length)
        {
            char current = content[i];

            if (!verbatim && current == '\\')
            {
                i += 2;
            }
            else if (current == quote)
            {
                if (verbatim && i + 1 < content.Length && content[i + 1] == quote)
                {
                    i += 2;
                }
                else
                {
                    return i + 1;
                }
            }
            else if (!verbatim && current == '\n')
            {
                return i;
            }
            else
            {
                i++;
            }
        }

        return content.Length;
    }
}
