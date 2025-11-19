using Contoso.WebApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Refit;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages()
                .AddRazorPagesOptions(options =>
                {
                    options.Conventions.AddPageRoute("/Home/Home", "/");
                });   
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();

builder.Services.AddTransient<AuthHandler>();
builder.Services.AddTransient<LoggingHandler>();

builder.Services.AddHttpClient<IContosoAPI>(client => {
    client.BaseAddress = new Uri(builder.Configuration["BackendUrl"]);
})
// .AddHttpMessageHandler(() => new LoggingHandler())
.AddHttpMessageHandler<AuthHandler>()
.AddTypedClient(client => RestService.For<IContosoAPI>(client));

// Blob image service for resolving ReleaseDate metadata logic.
builder.Services.AddSingleton<IBlobImageService, BlobImageService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
