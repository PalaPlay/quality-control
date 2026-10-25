#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

namespace Palapal.QualityControl.Editor
{
    [Serializable]
    public sealed class QualityProjectSettings
    {
        public const string SettingsPath = "ProjectSettings/PalapalQualitySettings.json";
        public string[] targetRoots = Array.Empty<string>();
        public bool validateAssetLocations = true;
        public bool validateAssetNaming = true;
        public bool validateMonoBehaviourStructure = true;
        public bool failOnWarnings = false;
        public string monoBehaviourNamespacePrefix = "Palapal";
        public string reportPath = "Artifacts/QualityReport.json";

        public static QualityProjectSettings Load(string projectRoot)
        {
            string path = Path.Combine(projectRoot, SettingsPath);
            if (!File.Exists(path))
                throw new FileNotFoundException("Create project settings using Palapal > Quality Control > Settings. Expected: " + path);
            string json = File.ReadAllText(path).Trim();
            if (!json.StartsWith("{", StringComparison.Ordinal) || !json.EndsWith("}", StringComparison.Ordinal))
                throw new InvalidDataException("Quality settings must be a JSON object: " + path);
            // Retain initialized defaults when older configurations omit new fields.
            var settings = new QualityProjectSettings();
            JsonUtility.FromJsonOverwrite(json, settings);
            settings.targetRoots = settings.targetRoots ?? Array.Empty<string>();
            return settings;
        }

        public void Save(string projectRoot)
        {
            string path = Path.Combine(projectRoot, SettingsPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(this, true) + Environment.NewLine);
        }
    }
}
#endif
