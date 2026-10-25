#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Palapal.QualityControl.Editor
{
    public sealed class MonoBehaviourStructureRule
    {
        private readonly string namespacePrefix;
        public MonoBehaviourStructureRule(string namespacePrefix = "Palapal")
        {
            this.namespacePrefix = namespacePrefix ?? "";
        }

        public List<QualityIssue> Validate(IEnumerable<QualityAsset> assets)
        {
            var issues = new List<QualityIssue>();
            var assemblies = CompilationPipeline.GetAssemblies();
            foreach (QualityAsset asset in assets)
            {
                if (asset.isFolder || !asset.path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(asset.path);
                Type type = script != null ? script.GetClass() : null;
                string assemblyName = CompilationPipeline.GetAssemblyNameFromScriptPath(asset.path);
                var assembly = assemblies.FirstOrDefault(a => a.name == Path.GetFileNameWithoutExtension(assemblyName));
                var defines = new HashSet<string>(assembly != null ? assembly.defines : Array.Empty<string>(), StringComparer.Ordinal);
                string source = File.ReadAllText(asset.path);
                if (type == null)
                {
                    if (MonoBehaviourSourceValidator.HasDirectMonoBehaviour(source, defines))
                        issues.Add(new QualityIssue("MONO-010", QualitySeverity.Warning,
                            "Cannot resolve this MonoBehaviour's compiled class. Check compilation and the class/file name before validating structure.", asset.path));
                    continue;
                }
                if (!typeof(MonoBehaviour).IsAssignableFrom(type)) continue;
                issues.AddRange(MonoBehaviourSourceValidator.Validate(source, asset.path, type.Name.Split('`')[0],
                    type.Namespace ?? "", namespacePrefix, defines));
            }
            return issues;
        }
    }
}
#endif
