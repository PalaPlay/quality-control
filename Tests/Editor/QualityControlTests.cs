#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Palapal.QualityControl.Editor.Tests
{
    public sealed class QualityControlTests
    {
        private const string Root = "Assets/Content";
        private static string Source(string fields = "", string properties = "", string methods = "", string nested = "")
        {
            return "using UnityEngine;\nnamespace Palapal.Game\n{\n" +
                "/// <summary>Example.</summary>\n/// <seealso cref=\"MonoBehaviour\"/>\npublic class Player : MonoBehaviour\n{\n" +
                "#region Fields\n" + fields + "\n#endregion\n#region Properties\n" + properties +
                "\n#endregion\n#region Methods\n" + methods + "\n#endregion\n" + nested + "\n}\n}";
        }
        private static List<QualityIssue> Mono(string source, ISet<string> defines = null) =>
            MonoBehaviourSourceValidator.Validate(source, Root + "/Scripts/RunTime/Player.cs", "Player", "Palapal.Game", "Palapal", defines);
        private static List<QualityIssue> Locations(params string[] paths) => new AssetLocationRule().Validate(
            paths.Select(p => new QualityAsset(Root + "/" + p, Root)));
        private static List<QualityIssue> Names(params string[] paths) => new AssetNamingRule().Validate(
            paths.Select(p => new QualityAsset(Root + "/" + p, Root)));

        [Test] public void OriginalTemplatePasses() => Assert.AreEqual(0, Mono(Source()).Count);

        [Test] public void CompleteMemberLayoutPasses()
        {
            var issues = Mono(Source(
                "private const int Max = 3;\nprivate static int count;\n[SerializeField] private int health;\npublic event System.Action Changed;\npublic delegate void Callback();",
                "public int Health => health;\npublic int Value { get; set; } = 2;",
                "public static void ResetAll() {}\npublic Player() {}\nprivate void Awake() {}\nprivate void Update() {}\npublic void Move() {}\nprivate void Stop() {}",
                "private enum State { Idle, Moving }"));
            Assert.AreEqual(0, issues.Count, string.Join("\n", issues.Select(i => i.message)));
        }

        [Test] public void WrongNamespaceAndFileNameAreReported()
        {
            var issues = MonoBehaviourSourceValidator.Validate(Source(), "Wrong.cs", "Player", "PalapalOther.Game");
            Assert.IsTrue(issues.Any(i => i.ruleId == "MONO-001"));
            Assert.IsTrue(issues.Any(i => i.ruleId == "MONO-002"));
        }

        [Test] public void PublicAndDocumentationAreRequired()
        {
            var issues = Mono(Source().Replace("public class", "class").Replace("///", "//"));
            foreach (string id in new[] { "MONO-003", "MONO-004", "MONO-005" }) Assert.IsTrue(issues.Any(i => i.ruleId == id), id);
        }

        [Test] public void MissingDuplicateAndOutOfOrderRegionsAreReported()
        {
            Assert.IsTrue(Mono(Source().Replace("#region Fields", "#region Data")).Any(i => i.ruleId == "MONO-006"));
            Assert.IsTrue(Mono(Source().Replace("#region Properties", "#region Fields")).Any(i => i.ruleId == "MONO-006"));
            Assert.IsTrue(Mono(Source().Replace("#region Fields", "#region Temporary").Replace("#region Methods", "#region Fields").Replace("#region Temporary", "#region Methods")).Any(i => i.ruleId == "MONO-007"));
        }

        [Test] public void WrongMemberRegionsAreReported()
        {
            Assert.IsTrue(Mono(Source("void Move() {}", "private int health;", "public int Health => 1;")).Count(i => i.ruleId == "MONO-008") == 3);
        }

        [Test] public void MemberGroupOrderingIsReported()
        {
            Assert.IsTrue(Mono(Source("int value; const int Max = 1;", "", "private void Stop() {} public void Move() {}")).Count(i => i.ruleId == "MONO-009") == 2);
        }

        [Test] public void NestedTypesMustFollowMethods()
        {
            Assert.IsTrue(Mono(Source("private class Helper {} ")).Any(i => i.ruleId == "MONO-008"));
            Assert.AreEqual(0, Mono(Source(nested: "private class Helper { public void Run() {} }")).Count);
            Assert.AreEqual(0, Mono(Source(nested: "#region NestedTypes\nprivate class Helper {}\n#endregion")).Count);
        }

        [Test] public void CommentsAndLiteralsCannotSupplyRegions()
        {
            string source = Source("string text = @\"\n#region Fields\n#endregion\nclass Pretend {}\";\n/* #region Properties */");
            Assert.AreEqual(0, Mono(source).Count);
            Assert.IsTrue(Mono(source.Replace("#region Methods", "// #region Methods")).Any(i => i.ruleId == "MONO-006"));
        }

        [Test] public void RegionInsideMethodDoesNotCountAsClassRegion()
        {
            string source = Source(methods: "private void Awake() {\n#region Fields\n#endregion\n}").Replace("#region Fields\n\n#endregion", "");
            Assert.IsTrue(Mono(source).Any(i => i.ruleId == "MONO-006"));
        }

        [Test] public void InactivePreprocessorBranchesAreIgnored()
        {
            string source = Source("#if UNITY_EDITOR && (!DISABLED || OTHER)\nprivate int value;\n#else\nThis is not C#;\n#region Fields\n#endif");
            Assert.AreEqual(0, Mono(source, new HashSet<string> { "UNITY_EDITOR" }).Count);
        }

        [Test] public void DefineElifAndEqualityAreSupported()
        {
            string source = "#define ACTIVE\n" + Source("#if ACTIVE == false\ninvalid;\n#elif ACTIVE != false\nprivate int value;\n#else\ninvalid;\n#endif");
            Assert.AreEqual(0, Mono(source).Count);
        }

        [Test] public void AttributesLambdasTuplesAndInitializersPass()
        {
            string source = Source(
                "[SerializeField] private int[] values = new[] { 1, 2 };\nprivate System.Action callback = () => { };\nprivate (int, int) pair;",
                "public (int, int) Pair => (1, 2);\npublic int[] Values { get; } = new[] { 1, 2 };\npublic int this[int i] => values[i];",
                "public (int, int) GetPair() => (1, 2);\npublic void Generic<T>() where T : new() { }");
            var issues = Mono(source);
            Assert.AreEqual(0, issues.Count, string.Join("\n", issues.Select(i => i.message)));
        }

        [Test] public void InterpolatedAndRawStringsDoNotAffectBraces()
        {
            var issues = Mono(Source("private string text = $\"{string.Join(\",\", new[] { \"a\", \"b\" })}\";\nprivate string raw = \"\"\" { #region Methods } \"\"\";"));
            Assert.AreEqual(0, issues.Count, string.Join("\n", issues.Select(i => i.message)));
        }

        [Test] public void MissingCompiledDirectBehaviourIsDetectable()
        {
            Assert.IsTrue(MonoBehaviourSourceValidator.HasDirectMonoBehaviour("class Test : UnityEngine.MonoBehaviour {}"));
            Assert.IsFalse(MonoBehaviourSourceValidator.HasDirectMonoBehaviour("// class Test : MonoBehaviour {}\nclass Helper {}"));
            Assert.IsFalse(MonoBehaviourSourceValidator.HasDirectMonoBehaviour("#if OFF\nclass Test : MonoBehaviour {}\n#endif"));
        }

        [Test] public void NumericNameSegmentsAndAssemblyNamesPass() => Assert.AreEqual(0,
            Names("Arts/Models/SM_Rock_01.fbx", "Arts/Textures/T_Wall_01_BC.png", "Scripts/RunTime/Palapal.Game_01.asmdef").Count);

        [Test] public void InvalidNamesStillWarn() => Assert.AreEqual(2, Names("Arts/Models/rock.fbx").Count);

        [Test] public void RestoredExtensionsPass() => Assert.AreEqual(0,
            Locations("Arts/Textures/RT_Main.rendertexture", "Arts/UIs/Panel.asset", "Arts/UIs/Styles/Theme.tss").Count);

        [Test] public void DirectArtsAndScriptsFilesAreReported()
        {
            var issues = Locations("Arts/Image.png", "Scripts/Player.cs");
            Assert.IsTrue(issues.Any(i => i.ruleId == "ASSET-LOCATION-002"));
            Assert.IsTrue(issues.Any(i => i.ruleId == "ASSET-LOCATION-009"));
        }

        [Test] public void InvalidHierarchyIsDeduplicated()
        {
            Assert.AreEqual(1, Locations("Arts/Unknown/First.png", "Arts/Unknown/Second.png").Count);
            Assert.AreEqual(0, Locations("Arts/Textures/Environment/T_Grass_BC.png").Count);
        }

        [Test] public void ConventionFailuresRemainWarnings() => Assert.IsTrue(
            Locations("Prefabs/Test.txt", "Arts/Models/Model.png").All(i => i.severity == QualitySeverity.Warning));

        [Test] public void MostSpecificRootWinsAndRootItselfIsExcluded()
        {
            string[] roots = { "Assets", Root };
            Assert.AreEqual(Root, QualityAssetScope.FindRoot(Root + "/Scripts/RunTime/Player.cs", roots));
            Assert.IsNull(QualityAssetScope.FindRoot(Root, roots));
            Assert.IsNull(QualityAssetScope.FindRoot("Assets/ContentOther/Test.cs", new[] { Root }));
        }

        [Test] public void SceneDataAndDescendantsAreExcluded()
        {
            var paths = new HashSet<string> { Root + "/Scenes/Levels/Main.unity" };
            Assert.IsTrue(QualityAssetScope.IsSceneData(Root + "/Scenes/Levels/Main/LightingData.asset", paths));
            Assert.IsTrue(QualityAssetScope.IsSceneData(Root + "/Scenes/Levels/Main/Sub/Texture.png", paths));
            Assert.IsFalse(QualityAssetScope.IsSceneData(Root + "/Scenes/Levels/Main.unity", paths));
            Assert.IsFalse(QualityAssetScope.IsSceneData(Root + "/Scenes/Levels/Other/LightingData.asset", paths));
        }

        [Test] public void WarningFailurePolicyIsConfigurable()
        {
            var settings = new QualityProjectSettings();
            var validator = new QualityValidator(settings);
            var issues = new List<QualityIssue> { new QualityIssue("TEST", QualitySeverity.Warning, "Test") };
            Assert.IsFalse(validator.ShouldFail(issues));
            settings.failOnWarnings = true;
            Assert.IsTrue(validator.ShouldFail(issues));
            settings.failOnWarnings = false;
            issues[0].severity = QualitySeverity.Error;
            Assert.IsTrue(validator.ShouldFail(issues));
        }

        [Test] public void ReportPathMustStayInOutputFolder()
        {
            string project = Path.GetFullPath(".");
            Assert.IsTrue(JsonQualityReporter.ResolveReportPath(project, "Artifacts/Quality.json").EndsWith("Quality.json"));
            foreach (string invalid in new[] { "../Outside.json", "Assets/Source.json", "ProjectSettings/Settings.json", "Packages/package.json", "Artifacts/Report.txt" })
                Assert.Throws<InvalidOperationException>(() => JsonQualityReporter.ResolveReportPath(project, invalid));
        }

        [Test, Category("Unity")] public void SettingsRoundTripAndOldConfigDefaults()
        {
            string project = Path.Combine(Path.GetTempPath(), "PalapalQualityTest-" + Guid.NewGuid().ToString("N"));
            try
            {
                var settings = new QualityProjectSettings { targetRoots = new[] { Root }, failOnWarnings = true };
                settings.Save(project);
                var loaded = QualityProjectSettings.Load(project);
                Assert.AreEqual(Root, loaded.targetRoots[0]);
                Assert.IsTrue(loaded.failOnWarnings);
                File.WriteAllText(Path.Combine(project, QualityProjectSettings.SettingsPath), "{\"targetRoots\":[\"Assets/Content\"]}");
                loaded = QualityProjectSettings.Load(project);
                Assert.IsTrue(loaded.validateMonoBehaviourStructure);
                Assert.IsTrue(loaded.validateAssetLocations);
                Assert.AreEqual("Palapal", loaded.monoBehaviourNamespacePrefix);
                File.WriteAllText(Path.Combine(project, QualityProjectSettings.SettingsPath), "[]");
                Assert.Throws<InvalidDataException>(() => QualityProjectSettings.Load(project));
            }
            finally { if (Directory.Exists(project)) Directory.Delete(project, true); }
        }
    }
}
#endif
