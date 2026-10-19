using System.Text;

namespace GameLauncher.Scanners;

/// <summary>
/// Leitor simples do formato de texto KeyValues (VDF/ACF) usado pela Steam.
/// Cada bloco vira um dicionário; valores são strings ou dicionários aninhados.
/// </summary>
public static class VdfParser
{
    public sealed class Node : Dictionary<string, object>
    {
        public Node() : base(StringComparer.OrdinalIgnoreCase) { }

        public string? Str(string key) => TryGetValue(key, out var v) ? v as string : null;
        public Node? Child(string key) => TryGetValue(key, out var v) ? v as Node : null;
    }

    public static Node ParseFile(string path) => Parse(File.ReadAllText(path));

    public static Node Parse(string text)
    {
        int i = 0;
        return ParseBlock(text, ref i, topLevel: true);
    }

    private static Node ParseBlock(string s, ref int i, bool topLevel)
    {
        var node = new Node();
        while (true)
        {
            var key = NextToken(s, ref i);
            if (key is null) return node;            // fim do ficheiro
            if (key.Value.IsCloseBrace)
            {
                if (topLevel) continue;               // chaveta a mais: ignora
                return node;
            }
            if (key.Value.IsOpenBrace) continue;      // inesperado: ignora

            var value = NextToken(s, ref i);
            if (value is null) return node;
            if (value.Value.IsOpenBrace)
                node[key.Value.Text] = ParseBlock(s, ref i, topLevel: false);
            else if (value.Value.IsCloseBrace)
                return node;
            else
                node[key.Value.Text] = value.Value.Text;
        }
    }

    private readonly record struct Token(string Text, bool IsOpenBrace, bool IsCloseBrace);

    private static Token? NextToken(string s, ref int i)
    {
        while (i < s.Length)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            // comentários // até ao fim da linha
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
            {
                while (i < s.Length && s[i] != '\n') i++;
                continue;
            }

            if (c == '{') { i++; return new Token("{", true, false); }
            if (c == '}') { i++; return new Token("}", false, true); }

            if (c == '"')
            {
                i++;
                var sb = new StringBuilder();
                while (i < s.Length && s[i] != '"')
                {
                    if (s[i] == '\\' && i + 1 < s.Length)
                    {
                        char n = s[i + 1];
                        sb.Append(n switch { 'n' => '\n', 't' => '\t', _ => n });
                        i += 2;
                    }
                    else sb.Append(s[i++]);
                }
                i++; // aspa final
                return new Token(sb.ToString(), false, false);
            }

            // token sem aspas (raro, mas existe)
            int start = i;
            while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] != '{' && s[i] != '}' && s[i] != '"') i++;
            return new Token(s[start..i], false, false);
        }
        return null;
    }
}
