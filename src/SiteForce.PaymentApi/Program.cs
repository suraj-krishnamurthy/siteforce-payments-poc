using Microsoft.EntityFrameworkCore;
using SiteForce.PaymentApi.Data;
using SiteForce.PaymentApi.Rules;
using SiteForce.PaymentApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Rule settings
builder.Services.Configure<RuleSettings>(
    builder.Configuration.GetSection(RuleSettings.SectionName));

// Microkernel: Rule plugins (extension points)
builder.Services.AddTransient<IRulePlugin, BasePayRule>();
builder.Services.AddTransient<IRulePlugin, AdvanceDeductionRule>();
builder.Services.AddTransient<IRulePlugin, SiteAllowanceRule>();
builder.Services.AddTransient<IRulePlugin, DisputeThresholdRule>();

// Microkernel: Rule engine core + config provider
builder.Services.AddScoped<RuleEngine>();
builder.Services.AddScoped<IRuleConfigProvider, DbRuleConfigProvider>();

// Application services
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<IngestionService>();
builder.Services.AddScoped<CalculationService>();
builder.Services.AddScoped<DisputeService>();

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SiteForce Payment API", Version = "v1" });
});

// CORS for React dev server
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Apply pending migrations on startup (POC convenience, skip for testing)
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.MapControllers();

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
