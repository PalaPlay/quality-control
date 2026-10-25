#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Palapal.QualityControl.Editor
{
    [Serializable]
    public sealed class QualityReport
    {
        public int schemaVersion = 1;
        public string project;
        public string generatedAtUtc;
        public QualityReportSummary summary;
        public List<QualityIssue> issues = new List<QualityIssue>();
    }

    [Serializable]
    public sealed class QualityReportSummary
    {
        public int errors;
        public int warnings;
        public int infos;
    }

    public static class JsonQualityReporter
    {
        public static string WriteReport(
            List<QualityIssue> issues,
            string projectRoot,
            string relativeReportPath)
        {
            if (issues == null)
                issues = new List<QualityIssue>();

            string fullProjectRoot = Path.GetFullPath(projectRoot);
            string fullReportPath = ResolveReportPath(projectRoot, relativeReportPath);

            var report = new QualityReport
            {
                project = Path.GetFileName(fullProjectRoot),
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                issues = new List<QualityIssue>(issues),
                summary = new QualityReportSummary()
            };

            foreach (QualityIssue issue in issues)
            {
                switch (issue.severity)
                {
                    case QualitySeverity.Error:
                        report.summary.errors++;
                        break;

                    case QualitySeverity.Warning:
                        report.summary.warnings++;
                        break;

                    case QualitySeverity.Info:
                        report.summary.infos++;
                        break;
                }
            }

            string directory = Path.GetDirectoryName(fullReportPath);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText(fullReportPath, json);

            return fullReportPath;
        }
        public static string ResolveReportPath(string projectRoot, string relativeReportPath)
        {
            if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentException(nameof(projectRoot));
            if (string.IsNullOrWhiteSpace(relativeReportPath)) relativeReportPath = "Artifacts/QualityReport.json";
            if (Path.IsPathRooted(relativeReportPath))
                throw new InvalidOperationException("The report path must be relative to the Unity project.");
            string fullProjectRoot = Path.GetFullPath(projectRoot);
            string report = Path.GetFullPath(Path.Combine(fullProjectRoot, relativeReportPath));
            string prefix = fullProjectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!report.StartsWith(prefix, comparison))
                throw new InvalidOperationException("The report path must be inside the Unity project.");
            // Keep generated output away from source assets and project configuration.
            string relative = report.Substring(prefix.Length).Replace('\\', '/');
            foreach (string protectedFolder in new[] { "Assets", "Packages", "ProjectSettings", "Library" })
                if (relative.Equals(protectedFolder, comparison) || relative.StartsWith(protectedFolder + "/", comparison))
                    throw new InvalidOperationException("Choose a report path outside Assets, Packages, ProjectSettings and Library.");
            if (!report.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The report path must end in .json.");
            return report;
        }
    }
}
#endif
