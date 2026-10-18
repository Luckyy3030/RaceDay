
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace RaceDay.Api.Tests
{
    public class AuthTests
    {
        private const string TestPassword = "TestPassword123!";

        [Fact]
        public async Task Register_WithValidParticipant_ReturnsCreated()
        {
            using var factory = new RaceDayWebApplicationFactory();
            await factory.InitializeDatabaseAsync();
            using var client = factory.CreateClient();

            var request = new
            {
                FirstName = "Test",
                LastName = "Participant",
                Email = "participant.test@example.com",
                Password = TestPassword,
                Role = "Participant",
                DateOfBirth = "2000-05-15",
                EmergencyContact = "0712345678"
            };

            var response = await client.PostAsJsonAsync(
                "/api/auth/register", request);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task Register_WithInvalidRole_ReturnsBadRequest()
        {
            using var factory = new RaceDayWebApplicationFactory();
            await factory.InitializeDatabaseAsync();
            using var client = factory.CreateClient();

            var request = new
            {
                FirstName = "Test",
                LastName = "User",
                Email = "invalid.role@example.com",
                Password = TestPassword,
                Role = "Administrator"
            };

            var response = await client.PostAsJsonAsync(
                "/api/auth/register", request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Login_WithValidCredentials_ReturnsOk()
        {
            using var factory = new RaceDayWebApplicationFactory();
            await factory.InitializeDatabaseAsync();
            using var client = factory.CreateClient();

            var registration = new
            {
                FirstName = "Login",
                LastName = "Participant",
                Email = "login.test@example.com",
                Password = TestPassword,
                Role = "Participant",
                DateOfBirth = "2000-05-15",
                EmergencyContact = "0712345678"
            };

            var registerResponse = await client.PostAsJsonAsync(
                "/api/auth/register", registration);

            Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

            var response = await client.PostAsJsonAsync(
                "/api/auth/login",
                new
                {
                    Email = "login.test@example.com",
                    Password = TestPassword
                });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Login_WithIncorrectPassword_ReturnsUnauthorized()
        {
            using var factory = new RaceDayWebApplicationFactory();
            await factory.InitializeDatabaseAsync();
            using var client = factory.CreateClient();

            var registration = new
            {
                FirstName = "Login",
                LastName = "Participant",
                Email = "wrong.password@example.com",
                Password = TestPassword,
                Role = "Participant",
                DateOfBirth = "2000-05-15",
                EmergencyContact = "0712345678"
            };

            var registerResponse = await client.PostAsJsonAsync(
                "/api/auth/register", registration);

            Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

            var response = await client.PostAsJsonAsync(
                "/api/auth/login",
                new
                {
                    Email = "wrong.password@example.com",
                    Password = "WrongPassword123!"
                });

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                response.StatusCode);
        }

        [Fact]
        public async Task GetMyProfile_WithoutToken_ReturnsUnauthorized()
        {
            using var factory = new RaceDayWebApplicationFactory();
            await factory.InitializeDatabaseAsync();
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/users/me");

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                response.StatusCode);
        }

        [Fact]
        public async Task Participant_CannotCreateEvent_ReturnsForbidden()
        {
            using var factory = new RaceDayWebApplicationFactory();
            await factory.InitializeDatabaseAsync();
            using var client = factory.CreateClient();

            var registration = new
            {
                FirstName = "Role",
                LastName = "Tester",
                Email = "role.tester@example.com",
                Password = TestPassword,
                Role = "Participant",
                DateOfBirth = "2000-05-15",
                EmergencyContact = "0712345678"
            };

            var registerResponse = await client.PostAsJsonAsync(
                "/api/auth/register", registration);

            Assert.Equal(
                HttpStatusCode.Created,
                registerResponse.StatusCode);

            var loginResponse = await client.PostAsJsonAsync(
                "/api/auth/login",
                new
                {
                    Email = "role.tester@example.com",
                    Password = TestPassword
                });

            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            var loginJson = await loginResponse.Content
                .ReadFromJsonAsync<JsonElement>();

            Assert.True(
                loginJson.TryGetProperty("token", out var tokenProperty),
                "The login response did not contain a token.");

            var token = tokenProperty.GetString();

            Assert.False(string.IsNullOrWhiteSpace(token));

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var eventRequest = new
            {
                EventName = "Test Running Event",
                Description = "An event for authorization testing",
                EventDate = "2027-05-15",
                Location = "Polokwane",
                EventType = "Running",
                DistanceKm = 5.0,
                RouteName = "Test Route",
                RouteDescription = "A test route"
            };

            var response = await client.PostAsJsonAsync(
                "/api/events", eventRequest);

            Assert.Equal(
                HttpStatusCode.Forbidden,
                response.StatusCode);
        }
    }
}

