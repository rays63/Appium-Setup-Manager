using System.Runtime.InteropServices;
using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Services;
using FluentAssertions;
using NSubstitute;

namespace AppiumSetupManager.Tests.Infrastructure;

public class CommandRunnerTests
{
    private static ILogService CreateLogMock() => Substitute.For<ILogService>();

    private static (string Command, string Arguments) EchoCommand(string text) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ("cmd", $"/c echo {text}")
            : ("echo", text);

    private static (string Command, string Arguments) SleepCommand(int seconds) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ("cmd", $"/c ping -n {seconds + 1} 127.0.0.1 > nul")
            : ("sleep", $"{seconds}");

    private static (string Command, string Arguments) ExitOneCommand() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ("cmd", "/c exit 1")
            : ("bash", "-c \"exit 1\"");

    [Fact]
    public async Task RunAsync_SuccessfulCommand_ReturnsExitCodeZero()
    {
        var log = CreateLogMock();
        var sut = new CommandRunner(log);
        var (cmd, args) = EchoCommand("hello");

        var result = await sut.RunAsync(cmd, args);

        result.Success.Should().BeTrue();
        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_NonZeroExitCode_ReturnsFailure()
    {
        var log = CreateLogMock();
        var sut = new CommandRunner(log);
        var (cmd, args) = ExitOneCommand();

        var result = await sut.RunAsync(cmd, args);

        result.Success.Should().BeFalse();
        result.ExitCode.Should().NotBe(0);
    }

    [Fact]
    public async Task RunAsync_CapturesStdOut()
    {
        var log = CreateLogMock();
        var sut = new CommandRunner(log);
        var (cmd, args) = EchoCommand("hello");

        var result = await sut.RunAsync(cmd, args);

        result.StdOut.Should().Contain("hello");
    }

    [Fact]
    public async Task RunAsync_TimedOut_ReturnsTimedOutResult()
    {
        var log = CreateLogMock();
        var sut = new CommandRunner(log, TimeSpan.FromSeconds(1));
        var (cmd, args) = SleepCommand(5);

        var result = await sut.RunAsync(cmd, args);

        result.TimedOut.Should().BeTrue();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task StreamAsync_YieldsLines()
    {
        var log = CreateLogMock();
        var sut = new CommandRunner(log);

        string cmd, args;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            cmd = "cmd";
            args = "/c echo line1 && echo line2 && echo line3";
        }
        else
        {
            cmd = "bash";
            args = "-c \"printf 'line1\\nline2\\nline3\\n'\"";
        }

        var results = new List<(string Line, LogEntryKind Kind)>();
        await foreach (var entry in sut.StreamAsync(cmd, args))
            results.Add(entry);

        results.Should().HaveCountGreaterOrEqualTo(3);
        results.Where(e => e.Kind == LogEntryKind.StdOut).Select(e => e.Line)
               .Should().Contain("line1").And.Contain("line2").And.Contain("line3");
    }

    [Fact]
    public async Task StreamAsync_YieldsStdErrLines()
    {
        var log = CreateLogMock();
        var sut = new CommandRunner(log);

        string cmd, args;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            cmd = "cmd";
            args = "/c echo errline 1>&2";
        }
        else
        {
            cmd = "bash";
            args = "-c \"echo errline >&2\"";
        }

        var results = new List<(string Line, LogEntryKind Kind)>();
        await foreach (var entry in sut.StreamAsync(cmd, args))
            results.Add(entry);

        results.Should().Contain(e => e.Kind == LogEntryKind.StdErr && e.Line.Contains("errline"));
    }

    [Fact]
    public async Task RunAsync_LogCommandCalledOnce()
    {
        var log = CreateLogMock();
        var sut = new CommandRunner(log);
        var (cmd, args) = EchoCommand("hello");

        await sut.RunAsync(cmd, args);

        log.Received(1).LogCommand(Arg.Any<string>());
    }

    [Fact]
    public async Task RunAsync_CapturesStdErr()
    {
        var log = CreateLogMock();
        var sut = new CommandRunner(log);

        string cmd, args;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            cmd = "cmd";
            args = "/c echo errormsg 1>&2";
        }
        else
        {
            cmd = "bash";
            args = "-c \"echo errormsg >&2\"";
        }

        var result = await sut.RunAsync(cmd, args);

        result.StdErr.Should().Contain("errormsg");
    }
}
