using ShaloTrack_API.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure
builder.Services.AddDatabaseServices(builder.Configuration);
builder.Services.AddRepositoryServices();
builder.Services.AddBusinessServices();

// ASP.NET Core
builder.Services.AddControllers();
builder.Services.AddSwaggerDocumentation();

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwaggerDocumentation();
}

// Temporarily disable until ALB is fully configured
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    timestamp = DateTime.UtcNow
}));


app.MapControllers();

app.Run();