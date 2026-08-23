using System.Linq;
using System.Reflection;
using KhoiProjectManagement.Application;
using Microsoft.EntityFrameworkCore;
using NetArchTest.Rules;
using Xunit;

namespace KhoiProjectManagement.ArchitectureTests
{
    // Build-time enforcement of the layering CLAUDE.md documents by hand today - a stray
    // Application -> Infrastructure reference or a controller dropped in the wrong project now fails a
    // test instead of only a code review. One marker type per assembly (rather than AssemblyName
    // strings) so a rename shows up here as a compile error, not a silently-stale test.
    public class LayerDependencyTests
    {
        private const string DomainNamespace = "KhoiProjectManagement.Domain";
        private const string ApplicationNamespace = "KhoiProjectManagement.Application";
        private const string InfrastructureNamespace = "KhoiProjectManagement.Infrastructure";
        private const string QuartzNamespace = "KhoiProjectManagement.Quartz";
        private const string ApiNamespace = "KhoiProjectManagementApi";

        private static readonly Assembly DomainAssembly = typeof(Domain.User).Assembly;
        private static readonly Assembly ApplicationAssembly = typeof(IUserService).Assembly;
        private static readonly Assembly InfrastructureAssembly = typeof(Infrastructure.Data.ProjectManagementContext).Assembly;
        private static readonly Assembly QuartzAssembly = typeof(Quartz.OverdueTaskCheckJob).Assembly;
        // No namespace on the top-level-statements Program.cs - Program lives in the global namespace
        // (see Program.cs's own comment on why it's public at all: WebApplicationFactory<Program>).
        private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

        private static readonly Assembly[] AllLayers =
        {
            DomainAssembly, ApplicationAssembly, InfrastructureAssembly, QuartzAssembly, ApiAssembly
        };

        [Fact]
        public void Domain_Should_Not_Depend_On_Application_Infrastructure_Quartz_Or_Api()
        {
            var result = Types.InAssembly(DomainAssembly)
                .Should()
                .NotHaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, QuartzNamespace, ApiNamespace)
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(result));
        }

        [Fact]
        public void Application_Should_Not_Depend_On_Infrastructure_Quartz_Or_Api()
        {
            var result = Types.InAssembly(ApplicationAssembly)
                .Should()
                .NotHaveDependencyOnAny(InfrastructureNamespace, QuartzNamespace, ApiNamespace)
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(result));
        }

        [Fact]
        public void Infrastructure_Should_Not_Depend_On_Quartz_Or_Api()
        {
            var result = Types.InAssembly(InfrastructureAssembly)
                .Should()
                .NotHaveDependencyOnAny(QuartzNamespace, ApiNamespace)
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(result));
        }

        [Fact]
        public void Quartz_Should_Not_Depend_On_Infrastructure_Or_Api()
        {
            var result = Types.InAssembly(QuartzAssembly)
                .Should()
                .NotHaveDependencyOnAny(InfrastructureNamespace, ApiNamespace)
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(result));
        }

        [Fact]
        public void Controllers_Should_Only_Reside_In_Api()
        {
            var result = Types.InAssemblies(AllLayers)
                .That().HaveNameEndingWith("Controller")
                .Should().ResideInNamespace("KhoiProjectManagementApi.Controllers")
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(result));
        }

        [Fact]
        public void DbContext_Implementations_Should_Only_Reside_In_Infrastructure()
        {
            var result = Types.InAssemblies(AllLayers)
                .That().Inherit(typeof(DbContext))
                .Should().ResideInNamespace("KhoiProjectManagement.Infrastructure.Data")
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(result));
        }

        [Fact]
        public void Repository_Implementations_Should_Only_Reside_In_Infrastructure()
        {
            // .AreClasses() excludes Application's IRepository<T>/IWikiSearchRepository ports - this
            // targets concrete implementations only, matching the port/adapter split CLAUDE.md describes.
            var result = Types.InAssemblies(AllLayers)
                .That().AreClasses()
                .And().HaveNameEndingWith("Repository")
                .Should().ResideInNamespace("KhoiProjectManagement.Infrastructure.Repositories")
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(result));
        }

        private static string Describe(TestResult result)
        {
            if (result.IsSuccessful)
                return string.Empty;

            var offenders = result.FailingTypes?.Select(t => t.FullName) ?? Enumerable.Empty<string>();
            return "Failing types: " + string.Join(", ", offenders);
        }
    }
}
