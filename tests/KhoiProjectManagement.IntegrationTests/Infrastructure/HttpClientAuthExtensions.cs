using System.Net.Http.Headers;
using System.Net.Http.Json;
using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.IntegrationTests.Infrastructure
{
    public static class HttpClientAuthExtensions
    {
        // Logs in as the given seeded user and attaches the resulting JWT as the client's default bearer
        // token - the same login flow AuthControllerTests exercises directly, reused here so every other
        // functional test can get an authenticated client in one line instead of repeating the POST.
        public static async Task<HttpClient> AuthenticateAsAsync(this HttpClient client, (string Email, string Password) user)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = user.Password });
            response.EnsureSuccessStatusCode();

            var login = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
            if (login == null || string.IsNullOrEmpty(login.Token))
            {
                throw new InvalidOperationException($"Login as {user.Email} did not return a token.");
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
            return client;
        }
    }
}
