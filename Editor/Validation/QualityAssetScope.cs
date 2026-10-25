#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;

namespace Palapal.QualityControl.Editor
{
    public sealed class QualityAsset
    {
        public readonly string path;
        public readonly string root;
        public readonly bool isFolder;
        public QualityAsset(string path, string root, bool isFolder = false)
        {
            this.path = path;
            this.root = root;
            this.isFolder = isFolder;
        }
    }

    public static class QualityAssetScope
    {
        public static List<QualityAsset> Collect(string[] roots)
        {
            string[] paths = AssetDatabase.GetAllAssetPaths();
            var knownPaths = new HashSet<string>(paths, StringComparer.Ordinal);
            var assets = new List<QualityAsset>();
            foreach (string path in paths)
            {
                string root = FindRoot(path, roots);
                if (root == null || path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                    IsSceneData(path, knownPaths)) continue;
                assets.Add(new QualityAsset(path, root, AssetDatabase.IsValidFolder(path)));
            }
            return assets;
        }

        public static string FindRoot(string path, string[] roots)
        {
            string best = null;
            foreach (string root in roots ?? Array.Empty<string>())
            {
                // Configured roots are containers, not assets governed by their own conventions.
                if (path == root) return null;
                if (!string.IsNullOrEmpty(root) && path.StartsWith(root + "/", StringComparison.Ordinal) &&
                    (best == null || root.Length > best.Length)) best = root;
            }
            return best;
        }

        public static bool IsSceneData(string path, ISet<string> knownPaths)
        {
            int scenes = path.IndexOf("/Scenes/", StringComparison.Ordinal);
            if (scenes < 0) return false;
            for (int end = scenes + 8; end <= path.Length; end++)
            {
                if (end != path.Length && path[end] != '/') continue;
                if (knownPaths.Contains(path.Substring(0, end) + ".unity")) return true;
            }
            return false;
        }
    }
}
#endif
