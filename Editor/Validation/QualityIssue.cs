
#if UNITY_EDITOR
using System;

namespace Palapal.QualityControl.Editor
{
    public enum QualitySeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    [Serializable]
    public sealed class QualityIssue
    {
        public string ruleId;
        public QualitySeverity severity;
        public string message;
        public string assetPath;
        public string location;

        public QualityIssue(
            string ruleId,
            QualitySeverity severity,
            string message,
            string assetPath = "",
            string location = "")
        {
            this.ruleId = ruleId ?? string.Empty;
            this.severity = severity;
            this.message = message ?? string.Empty;
            this.assetPath = assetPath ?? string.Empty;
            this.location = location ?? string.Empty;
        }
    }
}
#endif
