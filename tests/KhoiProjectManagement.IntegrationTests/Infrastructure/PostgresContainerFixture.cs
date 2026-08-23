using Testcontainers.PostgreSql;
using Xunit;

namespace KhoiProjectManagement.IntegrationTests.Infrastructure
{
    // One Postgres 16 container (matching docker-compose.yml's image) for the entire test run, shared via
    // ApiCollection - starting a fresh container per test class would be needlessly slow, and every test
    // in the collection runs serially against it anyway (see ApiCollection).
    public class PostgresContainerFixture : IAsyncLifetime
    {
        private PostgreSqlContainer? _container;
        public ApiWebApplicationFactory Factory { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            _container = new PostgreSqlBuilder("postgres:16")
                .WithDatabase("ProjectManagementDB")
                .WithUsername("khoi_test")
                .WithPassword("khoi_test_pw")
                .Build();

            await _container.StartAsync();

            // Program.cs runs its own MigrateAsync()+DatabaseSeeder.SeedAsync() against this connection
            // string the first time the factory's host is built (on first CreateClient()/Services access
            // below) - no separate migration step needed here.
            Factory = new ApiWebApplicationFactory(_container.GetConnectionString());
        }

        public async Task DisposeAsync()
        {
            Factory?.Dispose();
            if (_container != null)
            {
                await _container.DisposeAsync();
            }
        }
    }

    // xUnit only parallelizes across different collections, never within one - putting every functional/
    // integration test class in this collection makes them share the one seeded database serially
    // instead of racing each other over the same rows (an accepted simplicity trade-off for a
    // representative test slice; see the plan this was built from for the reasoning).
    [CollectionDefinition("Api")]
    public class ApiCollection : ICollectionFixture<PostgresContainerFixture>
    {
    }
}
