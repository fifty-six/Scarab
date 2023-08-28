using System;
using Avalonia.Logging;
using Microsoft.Extensions.Logging;

namespace Scarab.Util;

public class MicrosoftLogSink : ILogSink
{
    private ILogger _logger;

    public MicrosoftLogSink(ILogger logger) => _logger = logger;

    public bool IsEnabled(LogEventLevel level, string area)
    {
        return _logger.IsEnabled((LogLevel) (int) level);
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
    {
        _logger.Log((LogLevel) (int) level, messageTemplate);
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        _logger.Log((LogLevel) (int) level, messageTemplate, propertyValues);
    }
}