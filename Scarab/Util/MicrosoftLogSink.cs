using Avalonia.Logging;

namespace Scarab.Util;

#pragma warning disable CA2254

public class MicrosoftLogSink : ILogSink
{
    private readonly ILogger _logger;
    private readonly string[] _areas;

    public MicrosoftLogSink(ILogger logger, params string[] areas)
    {
        _logger = logger;
        _areas = areas.ToArray();
    }

    public bool IsEnabled(LogEventLevel level, string area)
    {
        return _logger.IsEnabled((LogLevel) (int) level) && _areas.Contains(area);
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
    {
        if (!IsEnabled(level, area))
            return;

        if (source is null)
        {
            _logger.Log((LogLevel) (int) level, messageTemplate);
        }
        else
        {
            _logger.Log(
                (LogLevel) (int) level,
                messageTemplate + " ({Source}#{Hash})",
                source.GetType().Name,
                source.GetHashCode()
            );
        }
    }

    public void Log(
        LogEventLevel level,
        string area,
        object? source,
        string messageTemplate,
        params object?[] propertyValues
    )
    {
        if (!IsEnabled(level, area))
            return;

        if (source is null)
        {
            _logger.Log((LogLevel) (int) level, messageTemplate, propertyValues);
        }
        else
        {
            _logger.Log(
                (LogLevel) (int) level,
                messageTemplate + " ({Source}#{Hash})",
                propertyValues,
                source.GetType().Name,
                source.GetHashCode()
            );
        }
    }
}