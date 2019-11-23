using System;

public class ConsoleLoggerHandler : ILoggerHandler
{
    private readonly ILoggerFormatter _loggerFormatter;

    public ConsoleLoggerHandler() : this(new DefaultLoggerFormatter()) { }

    public ConsoleLoggerHandler(ILoggerFormatter loggerFormatter)
    {
        _loggerFormatter = loggerFormatter;
    }

    public void Publish(LogMessage logMessage)
    {
        Console.WriteLine(_loggerFormatter.ApplyFormat(logMessage));
    }
}