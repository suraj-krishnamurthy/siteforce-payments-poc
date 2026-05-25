using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SiteForce.PaymentApi.Data;

namespace SiteForce.PaymentApi.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing DB registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<PaymentDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Use in-memory database for tests
            var dbName = "TestDb_" + Guid.NewGuid();
            services.AddDbContext<PaymentDbContext>(options =>
                options.UseInMemoryDatabase(dbName));
        });

        builder.UseEnvironment("Testing");
    }
}
