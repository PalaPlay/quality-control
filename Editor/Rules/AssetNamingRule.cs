#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Palapal.QualityControl.Editor
{
    public sealed class AssetNamingRule
    {
        private const string RulePrefix = "ASSET-NAME-";

        private static readonly string[] ValidModelPrefixes =
        {
            "SM_", "DM_"
        };

        private static readonly string[] ValidTexturePrefixes =
        {
            "TC_", "RT_", "T_"
        };

        private static readonly string[] ValidTextureSuffixes =
        {
            "_BC", "_N", "_MS", "_H", "_AO", "_E", "_I", "_EX"
        };

        private readonly List<QualityIssue> issues =
            new List<QualityIssue>();

        public List<QualityIssue> Validate(string[] roots) => Validate(QualityAssetScope.Collect(roots));

        public List<QualityIssue> Validate(IEnumerable<QualityAsset> assets)
        {
            issues.Clear();
            foreach (QualityAsset asset in assets)
            {
                if (asset.isFolder) ValidateFolderName(asset.path);
                else ValidateAssetName(asset.root, asset.path);
            }
            return new List<QualityIssue>(issues);
        }

        private void ValidateAssetName(string root, string assetPath)
        {
            string relative = assetPath.Substring(root.Length)
                .TrimStart('/');

            string[] parts = relative.Split('/');

            if (parts.Length == 0)
                return;

            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            string extension = Path.GetExtension(assetPath);

            bool isAssembly =
                extension.Equals(".asmdef", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".asmref", StringComparison.OrdinalIgnoreCase);

            if (isAssembly)
            {
                ValidateAssemblyName(fileName, assetPath);
            }
            else if (!IsPascalCase(fileName))
            {
                Add(
                    "001",
                    QualitySeverity.Warning,
                    $"Asset name '{fileName}' must follow PascalCase.",
                    assetPath);
            }

            ValidateSpecialNames(parts, fileName, extension, assetPath);
        }

        private void ValidateFolderName(string folderPath)
        {
            string folderName = Path.GetFileName(folderPath);

            if (string.IsNullOrEmpty(folderName))
                return;

            // Unity special folders and internal folders are not
            // automatically governed by the company's naming rules.
            if (folderName.StartsWith(".", StringComparison.Ordinal))
                return;

            if (!IsPascalCase(folderName))
            {
                Add(
                    "002",
                    QualitySeverity.Warning,
                    $"Folder name '{folderName}' must follow PascalCase.",
                    folderPath);
            }
        }

        private void ValidateSpecialNames(
            string[] parts,
            string fileName,
            string extension,
            string assetPath)
        {
            if (parts.Length < 2)
                return;

            string rootFolder = parts[0];
            string category = parts.Length > 1 ? parts[1] : string.Empty;

            if (rootFolder != "Arts")
                return;

            if (category == "Models")
            {
                if (!StartsWithAny(fileName, ValidModelPrefixes))
                {
                    Add(
                        "003",
                        QualitySeverity.Warning,
                        "Model names must start with SM_ or DM_.",
                        assetPath);
                }
            }
            else if (category == "SFXs")
            {
                if (extension.Equals(
                        ".mixer",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (!fileName.EndsWith(
                        "_S",
                        StringComparison.Ordinal) &&
                    !fileName.EndsWith(
                        "_D",
                        StringComparison.Ordinal))
                {
                    Add(
                        "004",
                        QualitySeverity.Warning,
                        "Audio names must end with _S or _D.",
                        assetPath);
                }
            }
            else if (category == "Textures")
            {
                ValidateTextureName(fileName, assetPath);
            }
        }

        private void ValidateTextureName(string fileName, string assetPath)
        {
            if (fileName.StartsWith("T_", StringComparison.Ordinal))
            {
                if (!EndsWithAny(fileName, ValidTextureSuffixes))
                {
                    Add(
                        "005",
                        QualitySeverity.Warning,
                        "Texture names starting with T_ must end with " +
                        "a recognized suffix such as _BC, _N or _AO.",
                        assetPath);
                }

                return;
            }

            if (!StartsWithAny(fileName, ValidTexturePrefixes))
            {
                Add(
                    "006",
                    QualitySeverity.Warning,
                    "Texture names must start with TC_, RT_ or T_.",
                    assetPath);
            }
        }

        private void ValidateAssemblyName(string fileName, string assetPath)
        {
            string[] segments = fileName.Split('.');

            foreach (string segment in segments)
            {
                if (!IsPascalCase(segment))
                {
                    Add(
                        "007",
                        QualitySeverity.Warning,
                        $"Assembly name segment '{segment}' must follow " +
                        "the naming convention.",
                        assetPath);

                    return;
                }
            }
        }

        private static bool IsPascalCase(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            // The existing company convention permits underscores
            // as separators, e.g. SM_Rock and Texture_BC.
            string[] segments = value.Split('_');

            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];
                if (string.IsNullOrEmpty(segment))
                    return false;

                if (!Regex.IsMatch(
                        segment,
                        i == 0 ? @"^[A-Z][a-zA-Z0-9]*$" : @"^[A-Z0-9][a-zA-Z0-9]*$"))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool StartsWithAny(
            string value,
            string[] prefixes)
        {
            foreach (string prefix in prefixes)
            {
                if (value.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool EndsWithAny(
            string value,
            string[] suffixes)
        {
            foreach (string suffix in suffixes)
            {
                if (value.EndsWith(suffix, StringComparison.Ordinal))
                    return true;
            }

            return false;
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
