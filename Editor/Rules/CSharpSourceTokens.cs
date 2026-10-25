#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Palapal.QualityControl.Editor
{
    // A source-layout scanner, not a replacement for the C# compiler. Literal and comment
    // contents never become structural tokens. Offsets/lines always refer to the original file.
    public static class CSharpSourceTokens
    {
        internal sealed class Token
        {
            public string text;
            public int line;
            public int offset;
            public bool doc;
        }

        private sealed class Branch
        {
            public bool parent;
            public bool taken;
        }

        internal static List<Token> Read(string source, ISet<string> initialDefines)
        {
            var tokens = new List<Token>();
            var defines = new HashSet<string>(initialDefines ?? new HashSet<string>(), StringComparer.Ordinal);
            var branches = new Stack<Branch>();
            bool active = true;
            int i = 0, line = 1;
            while (i < source.Length)
            {
                char c = source[i];
                if (c == '\n') { line++; i++; continue; }
                if (char.IsWhiteSpace(c)) { i++; continue; }
                int start = i, startLine = line;
                if (c == '#' && IsLineStart(source, i))
                {
                    while (i < source.Length && source[i] != '\n') i++;
                    string directive = source.Substring(start + 1, i - start - 1).Trim();
                    int comment = directive.IndexOf("//", StringComparison.Ordinal);
                    if (comment >= 0) directive = directive.Substring(0, comment).TrimEnd();
                    var match = Regex.Match(directive, @"^(\w+)\s*(.*)$");
                    string command = match.Groups[1].Value;
                    string value = match.Groups[2].Value;
                    if (command == "if")
                    {
                        bool condition = Evaluate(value, defines);
                        branches.Push(new Branch { parent = active, taken = condition });
                        active = active && condition;
                    }
                    else if (command == "elif" && branches.Count > 0)
                    {
                        Branch branch = branches.Peek();
                        bool condition = Evaluate(value, defines);
                        active = branch.parent && !branch.taken && condition;
                        branch.taken |= condition;
                    }
                    else if (command == "else" && branches.Count > 0)
                    {
                        Branch branch = branches.Peek();
                        active = branch.parent && !branch.taken;
                        branch.taken = true;
                    }
                    else if (command == "endif" && branches.Count > 0) active = branches.Pop().parent;
                    else if (active && command == "define") defines.Add(value);
                    else if (active && command == "undef") defines.Remove(value);
                    else if (active && (command == "region" || command == "endregion"))
                        tokens.Add(new Token { text = "#" + command + (value.Length > 0 ? " " + value : ""), line = line, offset = start });
                    continue;
                }
                if (!active) { while (i < source.Length && source[i] != '\n') i++; continue; }
                if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    while (i < source.Length && source[i] != '\n') i++;
                    if (start + 2 < source.Length && source[start + 2] == '/')
                        tokens.Add(new Token { text = source.Substring(start, i - start), line = line, offset = start, doc = true });
                    continue;
                }
                if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
                {
                    i += 2;
                    while (i < source.Length && !(source[i - 1] == '*' && source[i] == '/')) { if (source[i] == '\n') line++; i++; }
                    if (i < source.Length) i++;
                    if (start + 2 < source.Length && source[start + 2] == '*')
                        tokens.Add(new Token { text = source.Substring(start, i - start), line = startLine, offset = start, doc = true });
                    continue;
                }
                if (TryLiteral(source, ref i, ref line))
                {
                    tokens.Add(new Token { text = "<literal>", line = startLine, offset = start });
                    continue;
                }
                if (char.IsLetterOrDigit(c) || c == '_' || c == '@')
                {
                    i++;
                    while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_')) i++;
                }
                else if (c == '=' && i + 1 < source.Length && source[i + 1] == '>') i += 2;
                else i++;
                tokens.Add(new Token { text = source.Substring(start, i - start).TrimStart('@'), line = startLine, offset = start });
            }
            return tokens;
        }

        private static bool IsLineStart(string source, int i)
        {
            while (--i >= 0 && source[i] != '\n') if (!char.IsWhiteSpace(source[i])) return false;
            return true;
        }

        private static bool TryLiteral(string source, ref int i, ref int line)
        {
            int start = i, quote = i;
            while (quote < source.Length && (source[quote] == '$' || source[quote] == '@')) quote++;
            if (quote >= source.Length || (source[quote] != '"' && !(quote == i && source[quote] == '\''))) return false;
            char delimiter = source[quote];
            bool verbatim = source.Substring(start, quote - start).Contains("@");
            bool interpolated = source.Substring(start, quote - start).Contains("$");
            int quotes = 1;
            if (delimiter == '"') while (quote + quotes < source.Length && source[quote + quotes] == '"') quotes++;
            if (quotes >= 3)
            {
                i = quote + quotes;
                while (i < source.Length)
                {
                    int count = 0;
                    while (i + count < source.Length && source[i + count] == '"') count++;
                    if (count >= quotes) { i += quotes; return true; }
                    if (source[i] == '\n') line++;
                    i++;
                }
                return true;
            }
            i = quote + 1;
            while (i < source.Length)
            {
                char c = source[i++];
                if (c == '\n') line++;
                if (!verbatim && c == '\\') { if (i < source.Length) { if (source[i] == '\n') line++; i++; } continue; }
                if (c == delimiter)
                {
                    if (verbatim && i < source.Length && source[i] == delimiter) { i++; continue; }
                    break;
                }
                if (interpolated && c == '{')
                {
                    if (i < source.Length && source[i] == '{') { i++; continue; }
                    int depth = 1;
                    while (i < source.Length && depth > 0)
                    {
                        if (TryLiteral(source, ref i, ref line)) continue;
                        if (source[i] == '{') depth++;
                        if (source[i] == '}') depth--;
                        if (source[i] == '\n') line++;
                        i++;
                    }
                }
            }
            return true;
        }

        private static bool Evaluate(string expression, ISet<string> defines)
        {
            var parts = Regex.Matches(expression, @"\w+|&&|\|\||==|!=|[!()]");
            int position = 0;
            Func<bool> or = null, and = null, equality = null, unary = null;
            unary = () =>
            {
                if (position >= parts.Count) return false;
                string token = parts[position++].Value;
                if (token == "!") return !unary();
                if (token == "(") { bool value = or(); if (position < parts.Count && parts[position].Value == ")") position++; return value; }
                return token == "true" || (token != "false" && defines.Contains(token));
            };
            equality = () =>
            {
                bool value = unary();
                while (position < parts.Count && (parts[position].Value == "==" || parts[position].Value == "!="))
                { string op = parts[position++].Value; bool right = unary(); value = op == "==" ? value == right : value != right; }
                return value;
            };
            and = () => { bool value = equality(); while (position < parts.Count && parts[position].Value == "&&") { position++; bool right = equality(); value &= right; } return value; };
            or = () => { bool value = and(); while (position < parts.Count && parts[position].Value == "||") { position++; bool right = and(); value |= right; } return value; };
            return or();
        }
    }
}
#endif
