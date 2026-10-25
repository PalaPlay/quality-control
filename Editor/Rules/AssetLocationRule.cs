#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace Palapal.QualityControl.Editor
{
    public sealed class AssetLocationRule
    {
        private const string RulePrefix = "ASSET-LOCATION-";

        private static readonly HashSet<string> TextureExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".jpg", ".jpeg", ".tga", ".psd",
                ".tif", ".tiff", ".exr", ".hdr", ".bmp", ".rendertexture"
            };

        private static readonly HashSet<string> ModelExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".fbx", ".obj"
            };

        private static readonly HashSet<string> AudioExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".wav", ".mp3", ".ogg", ".mixer"
            };

        private static readonly HashSet<string> UiExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".uxml", ".uss", ".asset", ".tss"
            };

        private static readonly HashSet<string> MaterialExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".mat"
            };

        private static readonly HashSet<string> GameDataExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".asset", ".inputactions", ".physicmaterial"
            };

        private static readonly HashSet<string> ScriptExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".cs", ".dll", ".asmdef", ".asmref"
            };

        private readonly List<QualityIssue> issues =
            new List<QualityIssue>();

        public List<QualityIssue> Validate(string[] roots) => Validate(QualityAssetScope.Collect(roots));

        public List<QualityIssue> Validate(IEnumerable<QualityAsset> assets)
        {
            issues.Clear();
            var invalidFolders = new HashSet<string>(StringComparer.Ordinal);
            foreach (QualityAsset asset in assets)
            {
                string relative = asset.path.Substring(asset.root.Length + 1);
                string invalid = QualityHierarchy.FindInvalidFolder(relative, asset.isFolder);
                if (invalid != null)
                {
                    string folder = asset.root + "/" + invalid;
                    if (invalidFolders.Add(folder))
                        Add("012", QualitySeverity.Warning, "Folder is not in the approved hierarchy.", folder);
                    continue;
                }
                if (!asset.isFolder) ValidateAsset(asset.root, asset.path);
            }
            return new List<QualityIssue>(issues);
        }

        private void ValidateAsset(string root, string assetPath)
        {
            string relative = assetPath.Substring(root.Length)
                .TrimStart('/');

            if (string.IsNullOrEmpty(relative))
                return;

            string[] parts = relative.Split('/');

            if (parts.Length == 0)
                return;

            string rootFolder = parts[0];
            string extension = Path.GetExtension(assetPath);

            if (parts.Length < 2 || !QualityHierarchy.IsRoot(rootFolder))
            {
                Add(
                    "001",
                    QualitySeverity.Warning,
                    $"Root folder '{rootFolder}' is not in the approved hierarchy.",
                    assetPath);

                return;
            }

            if (parts.Length == 2 && rootFolder == "Arts")
            {
                Add(
                    "002",
                    QualitySeverity.Warning,
                    "Files are not allowed directly inside Arts. " +
                    "Place them in an approved subfolder.",
                    assetPath);
            }

            if (rootFolder == "Arts")
            {
                ValidateArts(parts, extension, assetPath);
            }
            else if (rootFolder == "Scripts")
            {
                ValidateScripts(parts, extension, assetPath);
            }
            else if (rootFolder == "Prefabs")
            {
                RequireExtension(
                    extension, ".prefab", assetPath,
                    "Prefabs must use the .prefab extension.");
            }
            else if (rootFolder == "Presets")
            {
                RequireExtension(
                    extension, ".preset", assetPath,
                    "Presets must use the .preset extension.");
            }
            else if (rootFolder == "GameData")
            {
                if (parts.Length > 1 &&
                    !GameDataExtensions.Contains(extension))
                {
                    Add(
                        "003",
                        QualitySeverity.Warning,
                        $"Unexpected GameData extension '{extension}'.",
                        assetPath);
                }
            }
        }

        private void ValidateArts(
            string[] parts,
            string extension,
            string assetPath)
        {
            if (parts.Length < 3)
                return;

            string category = parts[1];

            if (category == "Textures")
            {
                if (!TextureExtensions.Contains(extension))
                {
                    Add(
                        "004",
                        QualitySeverity.Warning,
                        $"Unexpected texture extension '{extension}'.",
                        assetPath);
                }
            }
            else if (category == "Models")
            {
                if (!ModelExtensions.Contains(extension))
                {
                    Add(
                        "005",
                        QualitySeverity.Warning,
                        $"Unexpected model extension '{extension}'.",
                        assetPath);
                }
            }
            else if (category == "SFXs")
            {
                if (!AudioExtensions.Contains(extension))
                {
                    Add(
                        "006",
                        QualitySeverity.Warning,
                        $"Unexpected audio extension '{extension}'.",
                        assetPath);
                }
            }
            else if (category == "Materials")
            {
                if (!MaterialExtensions.Contains(extension))
                {
                    Add(
                        "007",
                        QualitySeverity.Warning,
                        $"Unexpected material extension '{extension}'.",
                        assetPath);
                }
            }
            else if (category == "UIs")
            {
                if (!UiExtensions.Contains(extension))
                {
                    Add(
                        "008",
                        QualitySeverity.Warning,
                        $"Unexpected UI extension '{extension}'.",
                        assetPath);
                }
            }
        }

        private void ValidateScripts(
            string[] parts,
            string extension,
            string assetPath)
        {
            if (parts.Length == 2)
            {
                Add(
                    "009",
                    QualitySeverity.Warning,
                    "Files directly inside Scripts are discouraged. " +
                    "Use an approved Scripts subfolder.",
                    assetPath);

                return;
            }

            if (!ScriptExtensions.Contains(extension) &&
                !extension.Equals(".shader", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".compute", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".hlsl", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".cginc", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".glsl", StringComparison.OrdinalIgnoreCase))
            {
                Add(
                    "010",
                    QualitySeverity.Warning,
                    $"Unexpected file extension under Scripts: '{extension}'.",
                    assetPath);
            }
        }

        private void RequireExtension(
            string actual,
            string expected,
            string assetPath,
            string message)
        {
            if (!actual.Equals(
                    expected,
                    StringComparison.OrdinalIgnoreCase))
            {
                Add(
                    "011",
                    QualitySeverity.Warning,
                    message,
                    assetPath);
            }
        }

        private void Add(
            string suffix,
            QualitySeverity severity,
            string message,
            string assetPath)
        {
            issues.Add(new QualityIssue(
                RulePrefix + suffix,
                severity,
                message,
                assetPath));
        }

    }
}
#endif
