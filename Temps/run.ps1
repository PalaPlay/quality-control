param(
    [Parameter(Mandatory = $true)][string]$Unity,
    [Parameter(Mandatory = $true)][string]$Project,
    [string]$Log = "Artifacts/quality-unity.log"
)

$ErrorActionPreference = "Stop"
$Unity = (Resolve-Path -LiteralPath $Unity).Path
$Project = (Resolve-Path -LiteralPath $Project).Path
if (-not (Test-Path -LiteralPath (Join-Path $Project "ProjectSettings/PalapalQualitySettings.json"))) {
    throw "Create settings in Unity using Palapal > Quality Control > Settings, then commit the settings file."
}
if (-not [IO.Path]::IsPathRooted($Log)) { $Log = Join-Path $Project $Log }
$logDirectory = Split-Path -Parent $Log
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

# Start-Process waits for the Windows GUI executable and preserves its exit code.
# Unity receives each path as one quoted argument, including paths containing spaces.
$unityArguments = @(
    "-batchmode", "-nographics", "-quit",
    "-projectPath", ('"' + $Project + '"'),
    "-executeMethod", "Palapal.QualityControl.Editor.QualityCli.ValidateProject",
    "-logFile", ('"' + $Log + '"')
)
$process = Start-Process -FilePath $Unity -ArgumentList $unityArguments -Wait -PassThru -WindowStyle Hidden
Write-Host "Quality validation exit code: $($process.ExitCode). Log: $Log"
exit $process.ExitCode
