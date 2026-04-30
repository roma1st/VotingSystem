using Microsoft.EntityFrameworkCore;
using VotingSystem.Api.Infrastructure;
using VotingSystem.Api.Infrastructure.Data;
using VotingSystem.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Add services to the container.
builder.Services.AddScoped<IElectionService, ElectionService>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<VotingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Наповнюємо базу даних (Seeding 10 000 записів для тестування продуктивності)
using (var scope = app.Services.CreateScope()){var context = scope.ServiceProvider.GetRequiredService<VotingDbContext>(); if(app.Environment.EnvironmentName != "Testing"){context.Database.Migrate(); await DbSeeder.SeedAsync(context);}}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();

public partial class Program { }

