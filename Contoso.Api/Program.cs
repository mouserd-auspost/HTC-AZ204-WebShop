using Contoso.Api.Models;
using Contoso.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using Contoso.Api.Configuration;
using Azure.Core;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options => {
    options.AddPolicy("AllowAll", builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

});

builder.Services.AddAuthentication(options => {
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer("Bearer", options => {
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Key"])),
    };
});

builder.Services.AddAutoMapper(typeof(MapperConfig));



builder.Services.AddTransient<IProductsService, ProductsService>();
builder.Services.AddTransient<IAuthenticationService, AuthenticationService>();
builder.Services.AddTransient<IOrderService, OrderService>();
builder.Services.AddTransient<IUserService, UserService>();

// Configure Cosmos DB for EF Core using RBAC (DefaultAzureCredential) when available
var cosmosEndpoint = builder.Configuration["Azure:CosmosDB:AccountEndpoint"];
var cosmosDatabase = builder.Configuration["Azure:CosmosDB:DatabaseName"];


if (!string.IsNullOrEmpty(cosmosEndpoint) && !string.IsNullOrEmpty(cosmosDatabase))
{
    // Use DefaultAzureCredential to obtain AAD token (RBAC) for Cosmos DB
    var credential = new DefaultAzureCredential();

    // Configure logging for database startup info (do not log secrets)
    builder.Logging.AddSimpleConsole();

    var logger = LoggerFactory.Create(factory => factory.AddSimpleConsole()).CreateLogger("Startup");
    logger.LogInformation("Configuring Cosmos DB. Endpoint: {endpoint}, Database: {database}", cosmosEndpoint, cosmosDatabase);

    builder.Services.AddDbContext<ContosoDbContext>(options =>
    {
        options.UseCosmos(cosmosEndpoint, credential, cosmosDatabase);
    });
}
else
{
    throw new Exception("Cosmos DB configuration is missing in appsettings.json");
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
