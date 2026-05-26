using Ecopath;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ecopath.IntegrationTests;

/// <summary>
/// Spins up the full SURIMI-Ecopath gRPC host in-process for integration tests.
/// Override <see cref="ConfigureWebHost"/> to replace heavyweight services (e.g. IBlobStore,
/// IEwEController) with test doubles when a real EwE engine is not available.
/// </summary>
public class EcopathWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        // Replace services with test doubles here when needed, e.g.:
        //
        // builder.ConfigureServices(services =>
        // {
        //     services.RemoveAll<IEwEController>();
        //     services.AddSingleton<IEwEController, FakeEwEController>();
        // });
    }
}
