# Palapal Quality Control

Unity Editor and CI validation for asset hierarchy, asset naming, and MonoBehaviour source layout. The package is self-contained and does not require ProjectStructure or Issue Console. Unity 2021.3 or newer is required; the implementation is checked against Unity 6.6.

## Set up each project

1. Install this folder as a Unity package (Package Manager > Add package from disk > select `package.json`), or embed it in the project's `Packages` directory.
2. Open **Palapal > Quality Control > Settings**.
3. Select your content container folders in the Project window and click **Add Selected Project Folders**, or enter their `Assets/...` paths. Choose containers such as `Assets/_ProjectAssets`, with `Arts`, `Scripts`, etc. underneath.
4. Enable the desired rules. The MonoBehaviour namespace prefix defaults to `Palapal`; change it for other projects. An empty prefix allows any namespace, but still requires a namespace.
5. Click **Create Settings** or **Save Settings**. This writes `ProjectSettings/PalapalQualitySettings.json` in the consuming project.
6. Commit that JSON file. Click **Save and Validate** to save changes and run the same rules as CI. Click an issue to locate its asset or source line.

**Palapal > Quality Control > Validate Project** uses the last saved configuration. It does not exit Unity. **Reload** reloads the file from disk. When migrating, **Import Legacy ProjectStructure Roots** copies the old EditorPrefs selections into the window; review and save them. CI never reads EditorPrefs. Malformed settings are displayed as an error instead of being silently replaced.

The example in `Temps/PalapalQualitySettings.json` is documentation only; the menu creates the actual project configuration. Do not configure another developer's machine-specific paths.

## Rules

Asset convention and MonoBehaviour findings are **warnings**, preserving the original validator's severity. Configuration problems are **errors**. Enable **Fail On Warnings** for a strict CI gate. Disabling every rule or configuring no valid roots produces an error.

### Locations and names

- Approved roots: `Arts`, `Scripts`, `Scenes`, `Prefabs`, `Presets`, `GameData`.
- The original hierarchy is owned by this package. Approved intermediate folders are now checked, including empty folders. Once a hierarchy leaf is reached, arbitrary named subfolders are allowed. For example, `Arts/Textures/Environment` is allowed; `Arts/Unknown` is not.
- `Arts` requires a category subfolder; direct files under `Scripts` generate a warning. At other hierarchy levels, files are allowed directly if they satisfy the type rule. This permits, for example, UI panel settings under `Arts/UIs` and data assets under `GameData`.
- Models: `.fbx`, `.obj`; audio: `.wav`, `.mp3`, `.ogg`, `.mixer`; materials: `.mat`; UI: `.uxml`, `.uss`, `.asset`, `.tss`.
- Textures include common image formats and `.rendertexture`. Shader source files are allowed under `Scripts`. Prefabs and presets require their corresponding extensions. GameData permits `.asset`, `.inputactions`, `.physicmaterial`.
- Asset/folder names use uppercase-leading alphanumeric underscore segments. Later segments may start with digits, so `SM_Rock_01` is valid. Assembly file names validate each dot-separated segment.
- Model prefixes: `SM_`, `DM_`. Audio suffixes: `_S`, `_D` (mixers exempt). Texture prefixes: `TC_`, `RT_`, `T_`; `T_` textures require `_BC`, `_N`, `_MS`, `_H`, `_AO`, `_E`, `_I`, or `_EX`.
- Configured root names themselves are exempt. Overlapping roots use the most specific root and each asset is scanned once.
- Generated scene-data folders under `Scenes` with a matching sibling `.unity` scene, including their descendants, are excluded. `.meta` files are excluded.

`QualityHierarchy.cs` defines the complete hierarchy. The rules do not infer texture/material semantics or enforce a `.unity` extension for every file under `Scenes`.

### MonoBehaviour structure

For the compiled MonoBehaviour associated with each `.cs` asset in the configured roots (including components inheriting from your own MonoBehaviour base class), validation checks:

1. A namespace equal to the configured prefix or one of its child namespaces.
2. A public class matching the file name.
3. XML `<summary>...</summary>` and `<seealso cref="MonoBehaviour"/>` above the class. Fully qualified `UnityEngine.MonoBehaviour` is accepted. The summary may be empty, matching the supplied template.
4. Exactly one class-level region each, in this order: `Fields`, `Properties`, `Methods`.
5. Fields, events and delegates inside `Fields`; properties and indexers inside `Properties`; methods and constructors inside `Methods`.
6. Field groups ordered: constants, static fields, instance fields, events/delegates.
7. Method groups ordered: static methods, constructors, Unity lifecycle methods, public methods, non-public methods. Common Unity callbacks such as `Awake`, `Start`, `Update`, `OnDisable`, and `OnDestroy` count as lifecycle methods. Ordering within each group is unrestricted.
8. Nested classes, structs, interfaces and enums after the `Methods` region and outside the three required regions.

Comments, whitespace and literal contents are not layout rules. The template's explanatory comments are optional. Empty required regions are valid. A fully qualified base type works without a `using UnityEngine;` statement. Conditional compilation uses the script assembly's active Editor defines; inactive branches are ignored.

This is a source-layout check, not a C# compiler or semantic analyzer. Unity must compile successfully before CI can execute. Validation targets the primary component returned by `MonoScript.GetClass()`, not every secondary class or every partial-class file. A directly declared MonoBehaviour that cannot be resolved produces `MONO-010` instead of silently passing. Use one component class per matching file, as in the supplied template. Other build-target branches need validation under their own configuration.

Import **MonoBehaviour Template** from the Package Manager's Samples section for an example.

## CI

Commit the project configuration and run a licensed Unity Editor with the project closed in other Editor instances:

```text
Unity -batchmode -nographics -quit -projectPath "<project>" -executeMethod Palapal.QualityControl.Editor.QualityCli.ValidateProject -logFile "<log-file>"
```

Windows wrapper:

```powershell
./Temps/run.ps1 -Unity "D:/UnityEditor/6000.6.2f1/Editor/Unity.exe" -Project "D:/Projects/MyUnityProject"
```

The wrapper accepts `-Log` (default `Artifacts/quality-unity.log`, relative to the project), waits for Unity, and forwards the exit code. Match the Editor version to the consuming project.

| Exit code | Meaning |
| --- | --- |
| `0` | Validation passed according to the configured warning policy |
| `1` | Validation failed |
| `2` | Settings/reporting failure or another exception in the validation tool |

Unity startup, licensing, or compilation failures can prevent the entry point from running and have their own failure behavior. Always retain the Unity log as a CI artifact.

The default report is `Artifacts/QualityReport.json`. It contains `schemaVersion`, project name, UTC timestamp, severity counts, and issues with stable rule IDs, asset paths and source line numbers (the `location` string). Severity is serialized as `0` = Info, `1` = Warning, `2` = Error. Findings are sorted for reproducible ordering. Reports are written even when validation fails; initialization/report-writing exceptions may prevent a report from being produced.

Choose a project-relative `.json` report path outside `Assets`, `Packages`, `ProjectSettings`, and `Library`. Archive the current run's report rather than treating an old report as evidence that a failed startup completed validation.

## Tests and scope

Editor regression tests are in `Tests/Editor`. In a project with Unity Test Framework installed, add `"com.palapal.qualitycontrol"` to the project's `testables` list in `Packages/manifest.json` and run the EditMode tests.

The package provides explicit validation and reporting. The original package's folder creation, automatic import hooks, PascalCase renaming/Undo and Issue Console integration are not required for CI and are not duplicated here. The two packages can coexist, but only this package's saved JSON and report determine this package's CI outcome.
