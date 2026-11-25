using Minio;
using MinIOStorage.Common;
using MinIOStorage.Service;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MinioSettings>(builder.Configuration.GetSection("Minio"));

var settings = builder.Configuration.GetSection("Minio").Get<MinioSettings>() 
    ?? throw new ArgumentNullException("Minio settings not found...");

builder.Services.AddMinio(configureClient => configureClient
    .WithEndpoint(settings.Endpoint)
    .WithCredentials(settings.AccessKey, settings.SecretKey)
    .WithSSL(settings.UseSSL)
    .Build());

builder.Services.AddScoped<IFileStorageService, MinioFileStorageService>();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => Results.Redirect("/openapi/v1.json"));

app.Run();
