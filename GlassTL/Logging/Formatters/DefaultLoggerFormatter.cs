internal class DefaultLoggerFormatter : ILoggerFormatter
{
    public string ApplyFormat(LogMessage logMessage)
    {
        return string.Format("{0:MM.dd.yyyy HH:mm:ss}: {1} [line: {2} {3} -> {4}()]: {5}",
                        logMessage.DateTime, logMessage.Level, logMessage.LineNumber, logMessage.CallingClass,
                        logMessage.CallingMethod, logMessage.Text);
    }
}