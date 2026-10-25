#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Token = Palapal.QualityControl.Editor.CSharpSourceTokens.Token;

namespace Palapal.QualityControl.Editor
{
    public static class MonoBehaviourSourceValidator
    {
        private static readonly string[] RequiredRegions = { "Fields", "Properties", "Methods" };
        private static readonly HashSet<string> LifecycleMethods = new HashSet<string>(StringComparer.Ordinal)
        {
            "Awake", "OnEnable", "Start", "FixedUpdate", "Update", "LateUpdate", "OnDisable", "OnDestroy",
            "Reset", "OnValidate", "OnGUI", "OnDrawGizmos", "OnDrawGizmosSelected",
            "OnApplicationFocus", "OnApplicationPause", "OnApplicationQuit", "OnAnimatorIK", "OnAnimatorMove",
            "OnCollisionEnter", "OnCollisionStay", "OnCollisionExit", "OnCollisionEnter2D", "OnCollisionStay2D", "OnCollisionExit2D",
            "OnTriggerEnter", "OnTriggerStay", "OnTriggerExit", "OnTriggerEnter2D", "OnTriggerStay2D", "OnTriggerExit2D",
            "OnMouseDown", "OnMouseUp", "OnMouseUpAsButton", "OnMouseDrag", "OnMouseEnter", "OnMouseExit", "OnMouseOver",
            "OnBecameVisible", "OnBecameInvisible", "OnPreCull", "OnPreRender", "OnPostRender", "OnRenderObject",
            "OnWillRenderObject", "OnRenderImage", "OnAudioFilterRead", "OnTransformChildrenChanged", "OnTransformParentChanged",
            "OnBeforeTransformParentChanged", "OnRectTransformDimensionsChange", "OnCanvasGroupChanged", "OnDidApplyAnimationProperties",
            "OnParticleCollision", "OnParticleSystemStopped", "OnParticleTrigger", "OnParticleUpdateJobScheduled"
        };

        private sealed class Region
        {
            public string name;
            public int start;
            public int end;
            public bool topLevel;
        }

        public static bool HasDirectMonoBehaviour(string source, ISet<string> defines = null)
        {
            var tokens = Editor.CSharpSourceTokens.Read(source, defines).Where(t => !t.doc).ToList();
            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].text != "class") continue;
                bool bases = false;
                for (int j = i + 1; j < tokens.Count && tokens[j].text != "{"; j++)
                {
                    if (tokens[j].text == ":") bases = true;
                    if (bases && tokens[j].text == "MonoBehaviour") return true;
                }
            }
            return false;
        }

        public static List<QualityIssue> Validate(string source, string assetPath, string className,
            string classNamespace, string namespacePrefix = "Palapal", ISet<string> defines = null)
        {
            var issues = new List<QualityIssue>();
            var tokens = CSharpSourceTokens.Read(source, defines);
            int declaration = -1;
            for (int i = 0; i + 1 < tokens.Count; i++)
                if (tokens[i].text == "class" && tokens[i + 1].text == className) { declaration = i; break; }
            Action<string, string, int> add = (id, message, line) => issues.Add(new QualityIssue(
                "MONO-" + id, QualitySeverity.Warning, message, assetPath, line.ToString()));
            if (declaration < 0)
            {
                add("010", "Could not find the compiled MonoBehaviour declaration in the active source.", 1);
                return issues;
            }
            int classLine = tokens[declaration].line;
            namespacePrefix = (namespacePrefix ?? "").Trim().TrimEnd('.');
            if (string.IsNullOrEmpty(classNamespace) || (namespacePrefix.Length > 0 &&
                classNamespace != namespacePrefix && !classNamespace.StartsWith(namespacePrefix + ".", StringComparison.Ordinal)))
                add("001", namespacePrefix.Length == 0 ? "MonoBehaviour must be inside a namespace." :
                    "MonoBehaviour namespace must be '" + namespacePrefix + "' or a child namespace.", classLine);
            if (Path.GetFileNameWithoutExtension(assetPath) != className)
                add("002", "MonoBehaviour class name must match its file name.", classLine);

            int headerStart = declaration;
            while (headerStart > 0 && !new[] { "{", "}", ";" }.Contains(tokens[headerStart - 1].text) &&
                !tokens[headerStart - 1].text.StartsWith("#", StringComparison.Ordinal)) headerStart--;
            var header = tokens.Skip(headerStart).Take(declaration - headerStart).ToList();
            if (!header.Any(t => !t.doc && t.text == "public")) add("003", "MonoBehaviour class must be public.", classLine);
            string docs = string.Join("\n", header.Where(t => t.doc).Select(t => t.text));
            if (!Regex.IsMatch(docs, @"<summary\s*>[\s\S]*?</summary\s*>"))
                add("004", "Add an XML <summary> above the MonoBehaviour class.", classLine);
            if (!Regex.IsMatch(docs, "<seealso\\s+cref\\s*=\\s*[\"'](?:global::)?(?:UnityEngine\\.)?MonoBehaviour[\"']\\s*/>"))
                add("005", "Add XML <seealso cref=\"MonoBehaviour\"/> above the class.", classLine);

            int open = declaration + 2;
            while (open < tokens.Count && tokens[open].text != "{") open++;
            if (open == tokens.Count) { add("010", "Cannot locate the class body.", classLine); return issues; }
            int close = Matching(tokens, open, "{", "}");
            if (close < 0) { add("010", "Cannot locate the end of the class body.", classLine); return issues; }

            var regions = new List<Region>();
            var stack = new Stack<Region>();
            int depth = 1;
            for (int i = open + 1; i < close; i++)
            {
                Token token = tokens[i];
                if (token.text == "{") depth++;
                if (token.text == "}") depth--;
                if (token.text == "#region" || token.text.StartsWith("#region ", StringComparison.Ordinal))
                {
                    var region = new Region { name = token.text.Substring(7).Trim(), start = i, end = close,
                        topLevel = depth == 1 && stack.Count == 0 };
                    regions.Add(region);
                    stack.Push(region);
                }
                else if (token.text.StartsWith("#endregion", StringComparison.Ordinal) && stack.Count > 0)
                    stack.Pop().end = i;
            }
            int previous = open;
            foreach (string name in RequiredRegions)
            {
                var found = regions.Where(r => r.topLevel && r.name == name).ToList();
                if (found.Count != 1)
                    add("006", "Declare exactly one class-level #region " + name + ".", classLine);
                if (found.Count > 0)
                {
                    Region region = found[0];
                    if (region.start < previous) add("007", "Regions must appear in order: Fields, Properties, Methods.", tokens[region.start].line);
                    if (region.end == close) add("006", "Close #region " + name + " inside the class.", tokens[region.start].line);
                    previous = region.start;
                }
            }

            int fieldRank = -1, methodRank = -1;
            for (int i = open + 1; i < close;)
            {
                if (tokens[i].doc || tokens[i].text.StartsWith("#", StringComparison.Ordinal) || tokens[i].text == ";") { i++; continue; }
                int start = i;
                // Attributes may contain parentheses/braces that are unrelated to the member body.
                while (i < close && tokens[i].text == "[")
                {
                    int endAttribute = Matching(tokens, i, "[", "]");
                    if (endAttribute < 0) break;
                    i = endAttribute + 1;
                }
                int signatureStart = i, parens = 0, brackets = 0;
                bool initializer = false, expression = false;
                int firstParen = -1, boundary = -1;
                for (; i < close; i++)
                {
                    string text = tokens[i].text;
                    if (text == "(" && parens == 0 && brackets == 0 && !initializer && !expression && firstParen < 0 &&
                        i > signatureStart && (Regex.IsMatch(tokens[i - 1].text, @"^\w+$") || tokens[i - 1].text == ">") &&
                        !new[] { "public", "private", "protected", "internal", "static", "readonly", "virtual", "override", "async", "new" }.Contains(tokens[i - 1].text)) firstParen = i;
                    if (text == "(") parens++;
                    if (text == ")") parens--;
                    if (text == "[") brackets++;
                    if (text == "]") brackets--;
                    if (parens != 0 || brackets != 0) continue;
                    if (text == "=" && !expression) initializer = true;
                    if (text == "=>") expression = true;
                    if (text == "{")
                    {
                        int endBody = Matching(tokens, i, "{", "}");
                        if (endBody < 0) break;
                        if (!initializer && !expression)
                        {
                            boundary = i;
                            i = endBody + 1;
                            // Auto-property initializer belongs to this property.
                            if (i < close && tokens[i].text == "=")
                            {
                                while (i < close && tokens[i].text != ";")
                                {
                                    if (tokens[i].text == "{") { int end = Matching(tokens, i, "{", "}"); if (end >= 0) i = end; }
                                    i++;
                                }
                                if (i < close) i++;
                            }
                            break;
                        }
                        i = endBody;
                    }
                    else if (text == ";") { boundary = i++; break; }
                }
                if (boundary < 0) { add("010", "Could not inspect a member's layout; check its declaration.", tokens[start].line); break; }
                var signature = tokens.Skip(signatureStart).Take(boundary - signatureStart).Where(t => !t.doc && !t.text.StartsWith("#", StringComparison.Ordinal)).Select(t => t.text).ToList();
                // Only declaration tokens before the initializer/expression determine the member kind.
                int assignment = signature.FindIndex(t => t == "=" || t == "=>");
                if (assignment >= 0) signature = signature.Take(assignment).ToList();
                bool isDelegate = signature.Contains("delegate");
                bool nestedType = !isDelegate && signature.Any(t => t == "class" || t == "struct" || t == "interface" || t == "enum" || t == "record");
                bool isEvent = signature.Contains("event");
                bool method = firstParen >= 0 && !nestedType && !isDelegate && !isEvent;
                bool property = !nestedType && !isDelegate && !isEvent && !method && (tokens[boundary].text == "{" || (expression && !initializer));
                string required = nestedType ? null : property ? "Properties" : method ? "Methods" : "Fields";
                Region enclosing = regions.FirstOrDefault(r => r.topLevel && start > r.start && start < r.end);
                if (nestedType)
                {
                    Region methods = regions.FirstOrDefault(r => r.topLevel && r.name == "Methods");
                    if ((enclosing != null && RequiredRegions.Contains(enclosing.name)) || methods == null || start < methods.end)
                        add("008", "Place nested types after the Methods region, outside the three required regions.", tokens[start].line);
                }
                else if (enclosing == null || enclosing.name != required || i - 1 > enclosing.end)
                    add("008", "Place this " + (method ? "method/constructor" : property ? "property" : "field/event/delegate") + " entirely inside #region " + required + ".", tokens[start].line);

                if (required == "Fields")
                {
                    int rank = isEvent || isDelegate ? 3 : signature.Contains("const") ? 0 : signature.Contains("static") ? 1 : 2;
                    if (rank < fieldRank) add("009", "Fields order: constants, static fields, instance fields, events/delegates.", tokens[start].line);
                    fieldRank = Math.Max(fieldRank, rank);
                }
                if (method)
                {
                    string name = firstParen > 0 ? tokens[firstParen - 1].text : "";
                    bool constructor = name == className;
                    int rank = constructor ? 1 : signature.Contains("static") ? 0 : LifecycleMethods.Contains(name) ? 2 : signature.Contains("public") ? 3 : 4;
                    if (rank < methodRank) add("009", "Methods order: static methods, constructors, Unity lifecycle methods, public methods, non-public methods.", tokens[start].line);
                    methodRank = Math.Max(methodRank, rank);
                }
            }
            return issues;
        }

        private static int Matching(List<Token> tokens, int start, string open, string close)
        {
            int depth = 0;
            for (int i = start; i < tokens.Count; i++)
            {
                if (tokens[i].text == open) depth++;
                if (tokens[i].text == close && --depth == 0) return i;
            }
            return -1;
        }
    }
}
#endif
