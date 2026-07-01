using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;

using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Repositories.Implementations;

using ShaloTrack_API.Services.Interfaces;
using ShaloTrack_API.Services.Implementations;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<ShaloTrackDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Repositories
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Services
builder.Services.AddScoped<ICustomerService, CustomerService>();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();