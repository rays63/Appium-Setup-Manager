using System.Threading.Channels;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Platform;
using Serilog;
using Serilog.Core;

namespace AppiumSetupManager.Core.Services;

public interface ILogService
{
    void LogCommand(string command);
    void LogOutput(string line, LogEntryKind kind);
    void LogError(string message, Exception? ex = null);
    ChannelReader<LogEntry> Entries { get; }
}

public sealed class LogService : ILogService, IDisposable
{
    private readonly Channel<LogEntry> _channel;
    private readonly Logger _logger;

    public LogService(IPlatformAdapter platform)
    {
        var logDir = Path.Combine(platform.HomeDirectory, ".appiumsetupmanager", "logs");
        Directory.CreateDirectory(logDir);

        var logPath = Path.Combine(logDir, "app-.log");

        // Instance logger — avoids mutating the global Log.Logger singleton,
        // which would cause cross-test pollution when multiple LogService instances exist.
        _logger = new LoggerConfiguration()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        _channel = Channel.CreateUnbounded<LogEntry>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = false,
        });
    }

    public ChannelReader<LogEntry> Entries => _channel.Reader;

    public void LogCommand(string command)
    {
        _logger.Information("[CMD] {Command}", command);
        _channel.Writer.TryWrite(new LogEntry(DateTimeOffset.UtcNow, LogEntryKind.Command, command));
    }

    public void LogOutput(string line, LogEntryKind kind)
    {
        switch (kind)
        {
            case LogEntryKind.StdErr:
                _logger.Warning("[STDERR] {Line}", line);
                break;
            case LogEntryKind.Error:
                _logger.Error("[ERROR] {Line}", line);
                break;
            default:
                _logger.Information("[{Kind}] {Line}", kind, line);
                break;
        }

        _channel.Writer.TryWrite(new LogEntry(DateTimeOffset.UtcNow, kind, line));
    }

    public void LogError(string message, Exception? ex = null)
    {
        var text = ex is not null ? $"{message}: {ex.Message}" : message;
        _logger.Error(ex, "[ERROR] {Message}", text);
        _channel.Writer.TryWrite(new LogEntry(DateTimeOffset.UtcNow, LogEntryKind.Error, text));
    }

    public void Dispose() => _logger.Dispose();
}
