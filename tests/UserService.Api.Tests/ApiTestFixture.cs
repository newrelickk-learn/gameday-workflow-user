using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UserService.Infrastructure.Data;

namespace UserService.Api.Tests;

public class ApiTestFixture : WebApplicationFactory<Program>, IDisposable
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real database
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add in-memory database for testing
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDb");
            });

            // Build the service provider
            var sp = services.BuildServiceProvider();

            // Initialize database with test data
            using (var scope = sp.CreateScope())
            {
                var scopedServices = scope.ServiceProvider;
                var db = scopedServices.GetRequiredService<ApplicationDbContext>();
                db.Database.EnsureCreated();

                // Seed test data
                SeedTestData(db);
            }
        });

        builder.UseEnvironment("Testing");
    }

    private void SeedTestData(ApplicationDbContext context)
    {
        if (context.Users.Any())
        {
            return;
        }

        // Add test users matching the seed data structure
        var users = new[]
        {
            new UserService.Domain.Entities.User
            {
                Id = 1051,
                Name = "本部長",
                Email = "director@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
                Role = "director",
                Department = "本部",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new UserService.Domain.Entities.User
            {
                Id = 16051,
                Name = "経理",
                Email = "accounting@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
                Role = "accounting",
                Department = "経理部",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new UserService.Domain.Entities.User
            {
                Id = 21051,
                Name = "上長",
                Email = "manager@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
                Role = "manager",
                Department = "管理部",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new UserService.Domain.Entities.User
            {
                Id = 28151,
                Name = "開発エンジニア",
                Email = "engineer@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
                Role = "engineer",
                Department = "開発組織",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        context.Users.AddRange(users);
        context.SaveChanges();
    }
}

// Make Program class accessible for WebApplicationFactory
public partial class Program { }

