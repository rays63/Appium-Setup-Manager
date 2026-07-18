using System.Net;
using AppiumSetupManager.Core.Services;
using FluentAssertions;
using Xunit;

namespace AppiumSetupManager.Tests.Services;

/// <summary>
/// UpdateCheckService owns its own internal HttpClient rather than accepting one via DI (matching
/// the codebase's existing manual-injection style — see App.axaml.cs), so these tests use the
/// internal HttpMessageHandler-accepting constructor (enabled via InternalsVisibleTo) rather than a
/// mocking library that isn't already a dependency of this project.
/// </summary>
public class UpdateCheckServiceTests
{
    /// <summary>A minimal fake HttpMessageHandler that returns a canned response or throws.</summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        private readonly Exception? _throws;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        public FakeHandler(Exception throws)
        {
            _throws = throws;
            _responder = _ => throw throws;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_throws is not null)
                throw _throws;

            return Task.FromResult(_responder(request));
        }
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body),
    };

    [Fact]
    public async Task GetLatestNpmVersionAsync_SuccessResponse_ParsesVersion()
    {
        var handler = new FakeHandler(_ => JsonResponse(HttpStatusCode.OK, "{\"name\":\"appium\",\"version\":\"2.11.4\"}"));
        using var sut = new UpdateCheckService(handler);

        var result = await sut.GetLatestNpmVersionAsync("appium");

        result.Should().Be("2.11.4");
    }

    [Fact]
    public async Task GetLatestNpmVersionAsync_NotFoundResponse_ReturnsNull()
    {
        var handler = new FakeHandler(_ => JsonResponse(HttpStatusCode.NotFound, string.Empty));
        using var sut = new UpdateCheckService(handler);

        var result = await sut.GetLatestNpmVersionAsync("this-package-does-not-exist");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestNpmVersionAsync_MalformedJson_ReturnsNull()
    {
        var handler = new FakeHandler(_ => JsonResponse(HttpStatusCode.OK, "{ this is not valid json"));
        using var sut = new UpdateCheckService(handler);

        var result = await sut.GetLatestNpmVersionAsync("appium");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestNpmVersionAsync_MissingVersionField_ReturnsNull()
    {
        var handler = new FakeHandler(_ => JsonResponse(HttpStatusCode.OK, "{\"name\":\"appium\"}"));
        using var sut = new UpdateCheckService(handler);

        var result = await sut.GetLatestNpmVersionAsync("appium");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestNpmVersionAsync_HandlerThrowsTaskCanceled_ReturnsNull()
    {
        var handler = new FakeHandler(new TaskCanceledException("simulated timeout"));
        using var sut = new UpdateCheckService(handler);

        var result = await sut.GetLatestNpmVersionAsync("appium");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestNpmVersionAsync_HandlerThrowsHttpRequestException_ReturnsNull()
    {
        var handler = new FakeHandler(new HttpRequestException("simulated network error"));
        using var sut = new UpdateCheckService(handler);

        var result = await sut.GetLatestNpmVersionAsync("appium");

        result.Should().BeNull();
    }
}
