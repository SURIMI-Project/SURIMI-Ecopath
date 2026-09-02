using Ecopath.Services;
using FluentAssertions;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using Xunit;

namespace Ecopath.Tests.Services
{
    public class CheckSimulationServiceTests
    {
        private const string SimulationId = "sim-001";
        private const string OtherSimulationId = "sim-002";

        private readonly Mock<ILogger<ICheckSimulationService>> _loggerMock = new();

        private CheckSimulationService CreateSut() => new(_loggerMock.Object);

        private static FakeServerCallContext CreateContext(
            string scheme = "https",
            string ipAddress = "127.0.0.1",
            int port = 5001)
            => new(scheme, ipAddress, port);

        // ── ReserveSimulationAsync ──────────────────────────────────────────────

        [Fact]
        public async Task ReserveSimulationAsync_WithFreeEngine_WritesHostResponseHeader()
        {
            // Arrange
            var sut = CreateSut();
            var context = CreateContext();

            // Act
            await sut.ReserveSimulationAsync(SimulationId, context);

            // Assert
            context.WrittenResponseHeaders.Should().NotBeNull();
            context.WrittenResponseHeaders!.GetValue("host").Should().Be("https://127.0.0.1:5001");
        }

        [Fact]
        public async Task ReserveSimulationAsync_WhenAlreadyReserved_ThrowsUnavailable()
        {
            // Arrange
            var sut = CreateSut();
            await sut.ReserveSimulationAsync(SimulationId, CreateContext());

            // Act
            var act = async () => await sut.ReserveSimulationAsync(OtherSimulationId, CreateContext());

            // Assert
            await act.Should().ThrowAsync<RpcException>()
                .Where(e => e.StatusCode == StatusCode.Unavailable);
        }

        [Fact]
        public async Task ReserveSimulationAsync_WhenAlreadyReserved_TrailerContainsActiveSimulationId()
        {
            // Arrange
            var sut = CreateSut();
            await sut.ReserveSimulationAsync(SimulationId, CreateContext());

            // Act
            var act = async () => await sut.ReserveSimulationAsync(OtherSimulationId, CreateContext());

            // Assert
            var ex = await act.Should().ThrowAsync<RpcException>();
            ex.Which.Trailers.GetValue("active-simulation-id").Should().Be(SimulationId);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task ReserveSimulationAsync_WithNullOrEmptyId_ThrowsRpcException(string? simulationId)
        {
            // Arrange
            var sut = CreateSut();

            // Act
            var act = async () => await sut.ReserveSimulationAsync(simulationId!, CreateContext());

            // Assert
            await act.Should().ThrowAsync<RpcException>()
                .Where(e => e.StatusCode == StatusCode.InvalidArgument);
        }

        // ── ReleaseSimulation ───────────────────────────────────────────────────

        [Fact]
        public async Task ReleaseSimulation_WithMatchingId_AllowsSubsequentReservation()
        {
            // Arrange
            var sut = CreateSut();
            await sut.ReserveSimulationAsync(SimulationId, CreateContext());

            // Act
            sut.ReleaseSimulation(SimulationId);

            // Assert
            var act = async () => await sut.ReserveSimulationAsync(OtherSimulationId, CreateContext());
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task ReleaseSimulation_WithNonMatchingId_DoesNotReleaseReservation()
        {
            // Arrange
            var sut = CreateSut();
            await sut.ReserveSimulationAsync(SimulationId, CreateContext());

            // Act
            sut.ReleaseSimulation(OtherSimulationId);

            // Assert
            var act = async () => await sut.ReserveSimulationAsync(OtherSimulationId, CreateContext());
            await act.Should().ThrowAsync<RpcException>()
                .Where(e => e.StatusCode == StatusCode.Unavailable);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void ReleaseSimulation_WithNullOrEmptyId_ThrowsRpcException(string? simulationId)
        {
            // Arrange
            var sut = CreateSut();

            // Act
            var act = () => sut.ReleaseSimulation(simulationId!);

            // Assert
            act.Should().Throw<RpcException>()
                .Where(e => e.StatusCode == StatusCode.InvalidArgument);
        }

        // ── CheckIfCorrectSimulationAsync ───────────────────────────────────────

        [Fact]
        public async Task CheckIfCorrectSimulationAsync_WhenReservedForMatchingId_WritesHostResponseHeader()
        {
            // Arrange
            var sut = CreateSut();
            await sut.ReserveSimulationAsync(SimulationId, CreateContext());
            var context = CreateContext();

            // Act
            await sut.CheckIfCorrectSimulationAsync(SimulationId, context);

            // Assert
            context.WrittenResponseHeaders.Should().NotBeNull();
            context.WrittenResponseHeaders!.GetValue("host").Should().Be("https://127.0.0.1:5001");
        }

        [Fact]
        public async Task CheckIfCorrectSimulationAsync_WhenNotReserved_ThrowsUnavailable()
        {
            // Arrange
            var sut = CreateSut();

            // Act
            var act = async () => await sut.CheckIfCorrectSimulationAsync(SimulationId, CreateContext());

            // Assert
            await act.Should().ThrowAsync<RpcException>()
                .Where(e => e.StatusCode == StatusCode.Unavailable);
        }

        [Fact]
        public async Task CheckIfCorrectSimulationAsync_WhenNotReserved_TrailerHasEmptyActiveSimulationId()
        {
            // Arrange
            var sut = CreateSut();

            // Act
            var act = async () => await sut.CheckIfCorrectSimulationAsync(SimulationId, CreateContext());

            // Assert
            var ex = await act.Should().ThrowAsync<RpcException>();
            ex.Which.Trailers.GetValue("active-simulation-id").Should().BeNullOrEmpty();
        }

        [Fact]
        public async Task CheckIfCorrectSimulationAsync_WhenReservedForDifferentId_ThrowsUnavailableWithActiveSimulationIdTrailer()
        {
            // Arrange
            var sut = CreateSut();
            await sut.ReserveSimulationAsync(SimulationId, CreateContext());

            // Act
            var act = async () => await sut.CheckIfCorrectSimulationAsync(OtherSimulationId, CreateContext());

            // Assert
            var ex = await act.Should().ThrowAsync<RpcException>();
            ex.Which.StatusCode.Should().Be(StatusCode.Unavailable);
            ex.Which.Trailers.GetValue("active-simulation-id").Should().Be(SimulationId);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task CheckIfCorrectSimulationAsync_WithNullOrEmptyId_ThrowsRpcException(string? simulationId)
        {
            // Arrange
            var sut = CreateSut();

            // Act
            var act = async () => await sut.CheckIfCorrectSimulationAsync(simulationId!, CreateContext());

            // Assert
            await act.Should().ThrowAsync<RpcException>()
                .Where(e => e.StatusCode == StatusCode.InvalidArgument);
        }
    }

    /// <summary>
    /// Minimal <see cref="ServerCallContext"/> test double that provides an
    /// <see cref="HttpContext"/> through <c>UserState["__HttpContext"]</c> so that
    /// the <c>GetHttpContext()</c> extension method resolves correctly without a
    /// real ASP.NET Core pipeline.
    /// </summary>
    internal sealed class FakeServerCallContext : ServerCallContext
    {
        private const string HttpContextKey = "__HttpContext";

        public Metadata? WrittenResponseHeaders { get; private set; }

        public FakeServerCallContext(string scheme, string ipAddress, int port)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = scheme;
            httpContext.Connection.LocalIpAddress = IPAddress.Parse(ipAddress);
            httpContext.Connection.LocalPort = port;

            UserState[HttpContextKey] = httpContext;
        }

        protected override string MethodCore => string.Empty;
        protected override string HostCore => string.Empty;
        protected override string PeerCore => string.Empty;
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore => [];
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore => [];
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => new(null, []);

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
        {
            WrittenResponseHeaders = responseHeaders;
            return Task.CompletedTask;
        }

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
            throw new NotSupportedException();
    }
}
