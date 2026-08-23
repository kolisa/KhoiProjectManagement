using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace KhoiProjectManagement.FunctionalTests
{
    [Collection("Api")]
    public class AuthControllerTests
    {
        private readonly PostgresContainerFixture _fixture;

        public AuthControllerTests(PostgresContainerFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task Login_WithValidCredentials_ReturnsTokenAndPermissions()
        {
            var client = _fixture.Factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email = SeededUsers.Admin.Email,
                password = SeededUsers.Admin.Password
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
            Assert.NotNull(body);
            Assert.False(string.IsNullOrEmpty(body!.Token));
            Assert.False(string.IsNullOrEmpty(body.RefreshToken));
            Assert.Contains("projects.create", body.Permissions); // seeded Admin role grant
            Assert.Equal(SeededUsers.Admin.Email, body.User.Email, ignoreCase: true);
        }

        [Fact]
        public async Task Login_WithWrongPassword_Returns401()
        {
            var client = _fixture.Factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email = SeededUsers.Admin.Email,
                password = "definitely-wrong"
            });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Register_ThenRegisterAgainWithSameEmail_SecondCallReturns400()
        {
            var client = _fixture.Factory.CreateClient();
            var email = $"functest-{Guid.NewGuid():N}@khoitech.africa";
            var payload = new { name = "Func Test User", email, position = "QA", password = "SomeLongPassword1!" };

            var first = await client.PostAsJsonAsync("/api/auth/register", payload);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            var second = await client.PostAsJsonAsync("/api/auth/register", payload);
            Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        }

        [Fact]
        public async Task ForgotPassword_ReturnsNoContent_RegardlessOfWhetherTheEmailExists()
        {
            var client = _fixture.Factory.CreateClient();

            var forReal = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = SeededUsers.Admin.Email });
            var forNobody = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "nobody-registered@khoitech.africa" });

            // Both must be indistinguishable from the outside - a status-code difference here would be
            // an email-enumeration side channel (see AuthController/AuthService comments).
            Assert.Equal(HttpStatusCode.NoContent, forReal.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, forNobody.StatusCode);
        }

        [Fact]
        public async Task ResetPassword_WithGarbageToken_Returns400()
        {
            var client = _fixture.Factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/auth/reset-password", new { token = "not-a-real-token", newPassword = "NewPassword1!" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Me_WithoutBearerToken_Returns401()
        {
            var client = _fixture.Factory.CreateClient();

            var response = await client.GetAsync("/api/auth/me");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Me_WithValidToken_ReturnsUserAndMatchingPermissions()
        {
            var client = _fixture.Factory.CreateClient();
            var login = await (await client.PostAsJsonAsync("/api/auth/login", new
            {
                email = SeededUsers.Admin.Email,
                password = SeededUsers.Admin.Password
            })).Content.ReadFromJsonAsync<LoginResponseDto>();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
            var response = await client.GetAsync("/api/auth/me");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var me = await response.Content.ReadFromJsonAsync<MeResponseDto>();
            Assert.NotNull(me);
            Assert.Equal(login.User.Id, me!.User.Id);
            Assert.Equal(login.Permissions.OrderBy(p => p), me.Permissions.OrderBy(p => p));
        }

        [Fact]
        public async Task Refresh_RotatesToken_AndTheOldRefreshTokenStopsWorking()
        {
            var client = _fixture.Factory.CreateClient();
            var login = await (await client.PostAsJsonAsync("/api/auth/login", new
            {
                email = SeededUsers.Member.Email,
                password = SeededUsers.Member.Password
            })).Content.ReadFromJsonAsync<LoginResponseDto>();

            var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new { token = login!.RefreshToken });
            Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
            var refreshed = await refreshResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
            Assert.NotEqual(login.RefreshToken, refreshed!.RefreshToken);

            var replay = await client.PostAsJsonAsync("/api/auth/refresh", new { token = login.RefreshToken });
            Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        }
    }
}
