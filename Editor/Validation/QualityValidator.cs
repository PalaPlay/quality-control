#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;

namespace Palapal.QualityControl.Editor
{
    public sealed class QualityValidator
    {
        private readonly QualityProjectSettings settings;

        public QualityValidator(QualityProjectSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public List<QualityIssue> ValidateProject()
        {
            var issues = new List<QualityIssue>();
            var roots = new List<string>();
            foreach (string input in settings.targetRoots ?? Array.Empty<string>())
            {
                string root = (input ?? "").Trim().Replace('\\', '/').TrimEnd('/');
                if ((root != "Assets" && !root.StartsWith("Assets/", StringComparison.Ordinal)) ||
                    Array.Exists(root.Split('/'), part => part == "." || part == ".." || part.Length == 0))
                {
                    issues.Add(new QualityIssue("CONFIG-003", QualitySeverity.Error,
                        "Validation roots must be project-relative Assets folders without empty, '.' or '..' segments.", input));
                    continue;
                }
                if (!AssetDatabase.IsValidFolder(root))
                {
                    issues.Add(new QualityIssue("CONFIG-002", QualitySeverity.Error,
                        "Configured root folder does not exist: " + root, root));
                    continue;
                }
                if (!roots.Contains(root)) roots.Add(root);
            }

            if (roots.Count == 0)
                issues.Add(new QualityIssue("CONFIG-001", QualitySeverity.Error,
                    "No valid targetRoots configured. Open Palapal > Quality Control > Settings."));
            if (!settings.validateAssetLocations && !settings.validateAssetNaming && !settings.validateMonoBehaviourStructure)
                issues.Add(new QualityIssue("CONFIG-004", QualitySeverity.Error, "Enable at least one validation rule."));

            var assets = QualityAssetScope.Collect(roots.ToArray());
            if (settings.validateAssetLocations) issues.AddRange(new AssetLocationRule().Validate(assets));
            if (settings.validateAssetNaming) issues.AddRange(new AssetNamingRule().Validate(assets));
            if (settings.validateMonoBehaviourStructure)
                issues.AddRange(new MonoBehaviourStructureRule(settings.monoBehaviourNamespacePrefix).Validate(assets));

            issues.Sort((a, b) =>
            {
                int order = string.CompareOrdinal(a.assetPath, b.assetPath);
                if (order == 0) order = string.CompareOrdinal(a.ruleId, b.ruleId);
                if (order == 0) order = string.CompareOrdinal(a.location, b.location);
                if (order == 0) order = string.CompareOrdinal(a.message, b.message);
                return order;
            });
            return issues;
        }

        public bool ShouldFail(List<QualityIssue> issues)
        {
            return issues.Exists(issue => issue.severity == QualitySeverity.Error ||
                (settings.failOnWarnings && issue.severity == QualitySeverity.Warning));
        }

        public static QualityProjectSettings LoadSettings(string projectRoot)
        {
            return QualityProjectSettings.Load(projectRoot);
        }
    }
}
#endif
