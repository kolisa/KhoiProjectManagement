using KhoiProjectManagement.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KhoiProjectManagement.IntegrationTests.Infrastructure
{
    // Boots the real Api in-process (Program.cs runs unmodified - migrations + DatabaseSeeder.SeedAsync
    // included) against a throwaway Testcontainers Postgres instance instead of the dev docker-compose
    // database. See PostgresContainerFixture for container lifecycle and ApiCollection for how this is
    // shared (one instance, one seed run) across every functional/integration test class.
    public class ApiWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;
        public FakeEmailService FakeEmailService { get; } = new();

        public ApiWebApplicationFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _connectionString
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailService>();
                services.AddSingleton<IEmailService>(FakeEmailService);
            });
        }
    }
}
