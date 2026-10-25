
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Palapal.QualityControl.Editor
{
    public static class QualityCli
    {
        public static void ValidateProject()
        {
            string projectRoot = Directory.GetParent(
                Application.dataPath)?.FullName;

            if (string.IsNullOrEmpty(projectRoot))
            {
                Debug.LogError(
                    "[Palapal Quality] Could not resolve project root.");

                EditorApplication.Exit(2);
                return;
            }

            try
            {
                QualityProjectSettings settings =
                    QualityValidator.LoadSettings(projectRoot);

                var validator = new QualityValidator(settings);
                var issues = validator.ValidateProject();

                string reportPath = JsonQualityReporter.WriteReport(
                    issues,
                    projectRoot,
                    settings.reportPath);

                foreach (QualityIssue issue in issues)
                {
                    string message =
                        $"[{issue.ruleId}] {issue.message} " +
                        $"Asset: {issue.assetPath}";

                    switch (issue.severity)
                    {
                        case QualitySeverity.Error:
                            Debug.LogError(message);
                            break;

                        case QualitySeverity.Warning:
                            Debug.LogWarning(message);
                            break;

                        default:
                            Debug.Log(message);
                            break;
                    }
                }

                Debug.Log(
                    $"[Palapal Quality] Report written to: {reportPath}");

                if (validator.ShouldFail(issues))
                {
                    Debug.LogError(
                        "[Palapal Quality] Validation failed.");

                    EditorApplication.Exit(1);
                    return;
                }

                Debug.Log(
                    "[Palapal Quality] Validation passed.");

                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);

                Debug.LogError(
                    "[Palapal Quality] Internal tool failure.");

                EditorApplication.Exit(2);
            }
        }
    }
}
#endif
