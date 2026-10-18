using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaceDay.Api.Data;

namespace RaceDay.Api.Tests
{
    public class RaceDayWebApplicationFactory
        : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection =
            new SqliteConnection("Data Source=:memory:");

        public RaceDayWebApplicationFactory()
        {
            _connection.Open();

            _connection.CreateFunction(
                "sysdatetime",
                () => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fffffff"));
        }

        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:RaceDayConnection"] =
                        "Data Source=:memory:",
                    ["Jwt:Key"] =
                        "ThisIsATestJwtSecretKeyWithAtLeast32Bytes!",
                    ["Jwt:Issuer"] = "RaceDay.Api",
                    ["Jwt:Audience"] = "RaceDay.Client"
                };

                config.AddInMemoryCollection(settings);
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<RaceDayContext>();
                services.RemoveAll<DbContextOptions<RaceDayContext>>();


                services.AddDbContext<RaceDayContext>(options =>
    options.UseSqlite(_connection));
            });
        }

        public async Task InitializeDatabaseAsync()
        {
            using var scope = Services.CreateScope();

            var database = scope.ServiceProvider
                .GetRequiredService<RaceDayContext>();

            await database.Database.EnsureCreatedAsync();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                _connection.Dispose();
            }
        }
    }
}

