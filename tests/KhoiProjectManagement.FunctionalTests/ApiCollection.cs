using KhoiProjectManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace KhoiProjectManagement.FunctionalTests
{
    // xUnit resolves [CollectionDefinition]/[Collection] within the same test assembly only - a
    // definition declared in KhoiProjectManagement.IntegrationTests isn't visible here even though this
    // project references it, so it has to be redeclared per assembly. Each test assembly therefore gets
    // its own PostgresContainerFixture instance (its own container) - fine, since IntegrationTests and
    // FunctionalTests are normally run as separate `dotnet test` invocations anyway (see the plan/README
    // this was built from).
    [CollectionDefinition("Api")]
    public class ApiCollection : ICollectionFixture<PostgresContainerFixture>
    {
    }
}
