using Contoso.WebApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Refit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages()
                .AddRazorPagesOptions(options =>
                {
                    options.Conventions.AddPageRoute("/Home/Home", "/");
                });   
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();

// Configure Microsoft Identity Web
var graphScopes = (builder.Configuration["GraphScopes"] ?? "User.Read").Split(' ', StringSplitOptions.RemoveEmptyEntries);
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("EntraID"));

// Ensure the main auth cookie and the OIDC temporary cookies send on cross-site callbacks.
// In Development we allow cookies to be sent over HTTP by using SameAsRequest; in Production we require secure cookies.
var isDev = builder.Environment.IsDevelopment();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});

builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, (OpenIdConnectOptions options) =>
{
    // The OIDC handler sets temporary correlation and nonce cookies — ensure they're sent on the callback
    options.CorrelationCookie.SameSite = SameSiteMode.None;
    options.CorrelationCookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.NonceCookie.SameSite = SameSiteMode.None;
    options.NonceCookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    // Persist tokens from the OIDC response so they can be retrieved with GetTokenAsync
    options.SaveTokens = true;

    // options.Events.OnTokenValidated = async ctx =>
    //     {
    //         var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            
    //         // TokenEndpointResponse may be null in certain flows (e.g., implicit flow)
    //         // Fall back to the tokens saved in the authentication properties
    //         string? accessToken = null;
    //         string? idToken = null;

    //         if (ctx.TokenEndpointResponse != null)
    //         {
    //             logger.LogInformation("Token validated: Tokens retrieved from TokenEndpointResponse");
    //             accessToken = ctx.TokenEndpointResponse.AccessToken;
    //             idToken = ctx.TokenEndpointResponse.IdToken;
    //         }
    //         else if (ctx.Properties != null)
    //         {
    //             logger.LogInformation("Token validated: TokenEndpointResponse was null, retrieving from Properties");
    //             // Try to get tokens from saved properties (since SaveTokens = true)
    //             accessToken = ctx.Properties.GetTokenValue("access_token");
    //             idToken = ctx.Properties.GetTokenValue("id_token");
    //         }
    //         else
    //         {
    //             logger.LogWarning("Token validated: Both TokenEndpointResponse and Properties are null");
    //         }

    //         logger.LogInformation("Access token present: {HasAccessToken}, ID token present: {HasIdToken}", 
    //             !string.IsNullOrEmpty(accessToken), !string.IsNullOrEmpty(idToken));

    //         // Example: store in session
    //         var http = ctx.HttpContext;
    //         http.Session.SetString("AuthToken", accessToken ?? "");
    //         http.Session.SetString("IdToken", idToken ?? "");

    //         var name = ctx.Principal?.FindFirst("name")?.Value;
    //         if (name != null)
    //         {
    //             logger.LogInformation("User authenticated: {UserName}", name);
    //             http.Session.SetString("UserName", name);
    //         }
    //         else
    //         {
    //             logger.LogWarning("Token validated but no 'name' claim found in Principal");
    //         }
    //     };
});

builder.Services.AddControllersWithViews().AddMicrosoftIdentityUI();

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

app.UseAuthentication();
app.UseAuthorization();


// Login endpoint that triggers the OIDC challenge
app.MapGet("/login", (HttpContext http) =>
{
    var props = new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = "/Home" };
    return Results.Challenge(props, new[] { OpenIdConnectDefaults.AuthenticationScheme });
});



// Logout endpoint that signs out of the app and redirects to the identity provider
app.MapGet("/logout", async (HttpContext http, IConfiguration config) =>
{
    // Clear local session
    http.Session.Remove("AuthToken");
    http.Session.Remove("AuthTokenResponse");
    http.Session.Remove("UserName");
    http.Session.Clear();

    var entra = config.GetSection("Entra");
    var postLogout = entra["PostLogoutRedirectUri"] ?? "/";

    var props = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
    {
        RedirectUri = postLogout
    };

    await http.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, props);
    await http.SignOutAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect(postLogout);
});

app.MapRazorPages();

app.Run();
