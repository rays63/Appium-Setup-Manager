using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Services;

namespace AppiumSetupManager.Core.Infrastructure;

public interface ICommandRunner
{
    Task<CommandResult> RunAsync(string command, string arguments, CancellationToken ct = default);
    IAsyncEnumerable<(string Line, LogEntryKind Kind)> StreamAsync(string command, string arguments, CancellationToken ct = default);
}

public sealed class CommandRunner : ICommandRunner
{
    private readonly ILogService _log;
    private readonly TimeSpan _timeout;

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public CommandRunner(ILogService log) : this(log, DefaultTimeout) { }

    internal CommandRunner(ILogService log, TimeSpan timeout)
    {
        _log = log;
        _timeout = timeout;
    }

    public async Task<CommandResult> RunAsync(string command, string arguments, CancellationToken ct = default)
    {
        _log.LogCommand($"{command} {arguments}");

        using var process = CreateProcess(command, arguments);

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            // The executable isn't installed / isn't on PATH — this is an expected outcome for a
            // setup-manager app (that's exactly what it's meant to detect), not an exceptional one.
            _log.LogOutput(ex.Message, LogEntryKind.StdErr);
            return new CommandResult($"{command} {arguments}", -1, string.Empty, ex.Message, TimedOut: false);
        }

        var stdOutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stdErrTask = process.StandardError.ReadToEndAsync(ct);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            return new CommandResult($"{command} {arguments}", -1, string.Empty, "Timed out.", TimedOut: true);
        }

        var stdOut = await stdOutTask.ConfigureAwait(false);
        var stdErr = await stdErrTask.ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(stdOut))
            _log.LogOutput(stdOut.Trim(), LogEntryKind.StdOut);
        if (!string.IsNullOrWhiteSpace(stdErr))
            _log.LogOutput(stdErr.Trim(), LogEntryKind.StdErr);

        return new CommandResult($"{command} {arguments}", process.ExitCode, stdOut.Trim(), stdErr.Trim(), TimedOut: false);
    }

    public async IAsyncEnumerable<(string Line, LogEntryKind Kind)> StreamAsync(
        string command,
        string arguments,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _log.LogCommand($"{command} {arguments}");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_timeout);
        var linkedCt = timeoutCts.Token;

        using var process = CreateProcess(command, arguments);

        string? startFailure = null;
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            startFailure = ex.Message;
        }

        if (startFailure is not null)
        {
            _log.LogOutput(startFailure, LogEntryKind.StdErr);
            yield return (startFailure, LogEntryKind.StdErr);
            yield break;
        }

        var lineChannel = Channel.CreateUnbounded<(string Line, LogEntryKind Kind)>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = true,
        });

        // Readers catch cancellation internally so Task.WhenAll always completes,
        // which lets the ContinueWith call lineChannel.Writer.Complete() reliably.
        var stdOutReader = Task.Run(async () =>
        {
            try
            {
                string? line;
                while ((line = await process.StandardOutput.ReadLineAsync(linkedCt).ConfigureAwait(false)) is not null)
                    lineChannel.Writer.TryWrite((line, LogEntryKind.StdOut));
            }
            catch (OperationCanceledException) { }
        }, CancellationToken.None);

        var stdErrReader = Task.Run(async () =>
        {
            try
            {
                string? line;
                while ((line = await process.StandardError.ReadLineAsync(linkedCt).ConfigureAwait(false)) is not null)
                    lineChannel.Writer.TryWrite((line, LogEntryKind.StdErr));
            }
            catch (OperationCanceledException) { }
        }, CancellationToken.None);

        _ = Task.WhenAll(stdOutReader, stdErrReader)
            .ContinueWith(_ => lineChannel.Writer.Complete(), TaskScheduler.Default);

        // C# CS1626: yield return is not allowed inside a try-catch block.
        // The channel is completed by the ContinueWith above when both readers
        // finish (including on timeout/cancellation), so ReadAllAsync terminates cleanly.
        await foreach (var entry in lineChannel.Reader.ReadAllAsync(ct))
        {
            _log.LogOutput(entry.Line, entry.Kind);
            yield return entry;
        }

        // If timeout fired but caller did not cancel, kill the process and log.
        if (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            _log.LogError($"{command} {arguments}: stream timed out after {_timeout.TotalSeconds}s");
        }

        try { await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); }
        catch (InvalidOperationException) { }
    }

    private static Process CreateProcess(string command, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Every command this app runs is meant to complete unattended — this is a setup-manager,
        // not an interactive shell. NONINTERACTIVE is Homebrew's own documented opt-out of prompts
        // (e.g. its install script); harmless for every other command, which never reads it.
        startInfo.Environment["NONINTERACTIVE"] = "1";

        return new Process { StartInfo = startInfo };
    }
}
