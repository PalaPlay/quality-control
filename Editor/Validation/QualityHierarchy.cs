#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Palapal.QualityControl.Editor
{
    public static class QualityHierarchy
    {
        // Leaves may contain arbitrary project-specific subfolders.
        public static readonly Dictionary<string, string[]> Children = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "", new[] { "Arts", "Scripts", "Scenes", "Prefabs", "Presets", "GameData" } },
            { "Arts", new[] { "Fonts", "Models", "Materials", "Textures", "Videos", "Graphs", "UIs", "SFXs", "Animations" } },
            { "Arts/UIs", new[] { "Styles", "Templates" } },
            { "Arts/Animations", new[] { "Clips", "Animators" } },
            { "Scripts", new[] { "Editor", "RunTime", "ECS", "ScriptableObjects", "Shaders", "Helpers" } },
            { "Scenes", new[] { "Templates", "Levels" } },
            { "GameData", new[] { "InputActions", "VolumeProfiles" } }
        };

        public static bool IsRoot(string name) => Array.IndexOf(Children[""], name) >= 0;

        public static string FindInvalidFolder(string relativePath, bool isFolder)
        {
            string[] parts = relativePath.Split('/');
            int count = isFolder ? parts.Length : parts.Length - 1;
            string parent = "";
            for (int i = 0; i < count; i++)
            {
                if (!Children.TryGetValue(parent, out string[] allowed)) break;
                if (Array.IndexOf(allowed, parts[i]) < 0) return parent.Length == 0 ? parts[i] : parent + "/" + parts[i];
                parent = parent.Length == 0 ? parts[i] : parent + "/" + parts[i];
            }
            return null;
        }
    }
}
#endif
