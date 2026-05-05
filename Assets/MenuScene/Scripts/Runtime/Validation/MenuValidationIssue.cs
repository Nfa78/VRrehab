namespace VRStrokeRehab.MenuScene
{
    public enum MenuValidationSeverity
    {
        Warning,
        Error
    }

    public struct MenuValidationIssue
    {
        public MenuValidationIssue(MenuValidationSeverity severity, string source, string message)
        {
            Severity = severity;
            Source = source;
            Message = message;
        }

        public MenuValidationSeverity Severity { get; private set; }
        public string Source { get; private set; }
        public string Message { get; private set; }
    }
}
