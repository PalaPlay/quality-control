#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Palapal.QualityControl.Editor
{
    public sealed class QualityControlWindow : EditorWindow
    {
        private QualityProjectSettings settings;
        private Vector2 scroll;
        private string error;
        private bool dirty;
        private List<QualityIssue> issues;
        private string result;
        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        [MenuItem("Palapal/Quality Control/Settings")]
        public static void Open() => GetWindow<QualityControlWindow>("Quality Control");

        [MenuItem("Palapal/Quality Control/Validate Project")]
        public static void ValidateSavedProject()
        {
            var window = GetWindow<QualityControlWindow>("Quality Control");
            // Never replace unsaved settings as a side effect of running validation.
            try { window.Run(QualityProjectSettings.Load(ProjectRoot)); }
            catch (Exception exception) { window.error = exception.Message; Debug.LogException(exception); }
        }

        private void OnEnable() => Reload();

        private void Reload()
        {
            error = null;
            try
            {
                settings = File.Exists(Path.Combine(ProjectRoot, QualityProjectSettings.SettingsPath))
                    ? QualityProjectSettings.Load(ProjectRoot) : new QualityProjectSettings();
                dirty = false;
            }
            catch (Exception exception)
            {
                // Do not silently overwrite a malformed configuration with defaults.
                settings = null;
                error = exception.Message;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Project Quality Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Saved in " + QualityProjectSettings.SettingsPath +
                ". Commit this file so local validation and CI use the same rules.", MessageType.Info);
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (settings == null)
            {
                if (GUILayout.Button("Reload Settings")) Reload();
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("Validation Roots", EditorStyles.boldLabel);
            var roots = new List<string>(settings.targetRoots ?? Array.Empty<string>());
            for (int i = 0; i < roots.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                roots[i] = EditorGUILayout.TextField(roots[i]);
                if (GUILayout.Button("Remove", GUILayout.Width(65))) { roots.RemoveAt(i--); dirty = true; }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Add Selected Project Folders"))
            {
                foreach (UnityEngine.Object selection in Selection.objects)
                {
                    string assetPath = AssetDatabase.GetAssetPath(selection);
                    if ((assetPath == "Assets" || assetPath.StartsWith("Assets/", StringComparison.Ordinal)) &&
                        AssetDatabase.IsValidFolder(assetPath) && !roots.Contains(assetPath)) roots.Add(assetPath);
                }
                dirty = true;
            }
            if (GUILayout.Button("Add Root Path")) { roots.Add("Assets/"); dirty = true; }
            if (GUILayout.Button("Import Legacy ProjectStructure Roots"))
            {
                foreach (string legacyRoot in EditorPrefs.GetString("FolderValidator_TargetPaths", "").Split(';'))
                    if (!string.IsNullOrWhiteSpace(legacyRoot) && !roots.Contains(legacyRoot)) roots.Add(legacyRoot);
                dirty = true;
            }
            settings.targetRoots = roots.ToArray();
            EditorGUILayout.HelpBox("Select a container such as Assets/_ProjectAssets. It should contain Arts, Scripts, Scenes, etc. Legacy roots are imported only when you click the button; review them before saving.", MessageType.Info);

            settings.validateAssetLocations = EditorGUILayout.Toggle("Asset Locations", settings.validateAssetLocations);
            settings.validateAssetNaming = EditorGUILayout.Toggle("Asset Naming", settings.validateAssetNaming);
            settings.validateMonoBehaviourStructure = EditorGUILayout.Toggle("MonoBehaviour Structure", settings.validateMonoBehaviourStructure);
            settings.monoBehaviourNamespacePrefix = EditorGUILayout.TextField("Namespace Prefix", settings.monoBehaviourNamespacePrefix);
            settings.failOnWarnings = EditorGUILayout.Toggle("Fail On Warnings", settings.failOnWarnings);
            settings.reportPath = EditorGUILayout.TextField("Report Path", settings.reportPath);
            if (EditorGUI.EndChangeCheck()) dirty = true;

            EditorGUILayout.Space();
            bool exists = File.Exists(Path.Combine(ProjectRoot, QualityProjectSettings.SettingsPath));
            EditorGUILayout.LabelField(!exists ? "Settings have not been created yet." : dirty ? "Unsaved changes" : "Settings saved");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(exists ? "Save Settings" : "Create Settings")) Save();
            if (GUILayout.Button("Save and Validate") && Save()) Run(settings);
            if (GUILayout.Button("Reload"))
            {
                if (!dirty || EditorUtility.DisplayDialog("Reload Settings", "Discard unsaved settings?", "Reload", "Cancel")) Reload();
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, MessageType.Info);
            if (issues != null)
            {
                foreach (QualityIssue issue in issues)
                {
                    if (GUILayout.Button($"{issue.severity} [{issue.ruleId}] {issue.message}\n{issue.assetPath} {issue.location}", EditorStyles.helpBox))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(issue.assetPath);
                        if (asset != null)
                        {
                            EditorGUIUtility.PingObject(asset);
                            if (int.TryParse(issue.location, out int line)) AssetDatabase.OpenAsset(asset, line);
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private bool Save()
        {
            try
            {
                JsonQualityReporter.ResolveReportPath(ProjectRoot, settings.reportPath);
                settings.Save(ProjectRoot);
                dirty = false;
                error = null;
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        private void Run(QualityProjectSettings runSettings)
        {
            try
            {
                error = null;
                var validator = new QualityValidator(runSettings);
                issues = validator.ValidateProject();
                string report = JsonQualityReporter.WriteReport(issues, ProjectRoot, runSettings.reportPath);
                result = $"{(validator.ShouldFail(issues) ? "Failed" : "Passed")}: " +
                    $"{issues.Count(i => i.severity == QualitySeverity.Error)} errors, " +
                    $"{issues.Count(i => i.severity == QualitySeverity.Warning)} warnings. Report: {report}";
                Debug.Log("[Palapal Quality] " + result);
            }
            catch (Exception exception) { error = exception.Message; Debug.LogException(exception); }
        }
    }
}
#endif
