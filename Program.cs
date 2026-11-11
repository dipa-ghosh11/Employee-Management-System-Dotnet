using EmployeeManagementSystem.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

using EmployeeManagementSystem.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);
});

// Register services
//builder.Services.AddControllers();
//builder.Services.AddOpenApi();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// Configure MongoDB settings
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

// Register MongoDB client
builder.Services.AddSingleton<IMongoClient>(serviceProvider =>
{
    var settings = serviceProvider
        .GetRequiredService<IOptions<MongoDbSettings>>()
        .Value;

    return new MongoClient(settings.ConnectionString);
});



// Register MongoDB database
builder.Services.AddSingleton<IMongoDatabase>(serviceProvider =>
{
    var settings = serviceProvider
        .GetRequiredService<IOptions<MongoDbSettings>>()
        .Value;

    var client = serviceProvider.GetRequiredService<IMongoClient>();

    return client.GetDatabase(settings.DatabaseName);
});

builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<DepartmentService>();

// Build the application AFTER all registrations
var app = builder.Build();
Log.Information("Employee Management System application is starting...");

app.UseSerilogRequestLogging();

// Configure HTTP pipeline
if (app.Environment.IsDevelopment())
{
    //app.MapOpenApi();

    app.UseSwagger(); //generates the OpenAPI JSON document.
    app.UseSwaggerUI(); //displays the interactive Swagger interface in your browser.
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () =>
    "Welcome to the Employee Management System API!");

app.MapGet("/health", () =>
    Results.Ok("API is running smoothly."));

// MongoDB connectivity test
app.MapGet("/api/health/mongodb",
    async ([Microsoft.AspNetCore.Mvc.FromServices]
    IMongoDatabase database) =>
    {
        try
        {
            await database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("ping", 1));

            return Results.Ok(new
            {
                status = "Connected",
                message = "MongoDB Atlas connection successful."
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MongoDB error: {ex.Message}");

            return Results.Problem(
                title: "MongoDB connection failed",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    });

Log.Information("Employee Management System application is ready.");

app.Run();
