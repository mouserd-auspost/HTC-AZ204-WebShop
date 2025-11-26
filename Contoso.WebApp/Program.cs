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
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Configure forwarded headers for proper HTTPS detection behind proxies/load balancers
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

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
    
    // Require HTTPS for redirect URIs in production
    if (!isDev)
    {
        options.RequireHttpsMetadata = true;
    }

    options.Events.OnTokenValidated = async ctx =>
        {
            var http = ctx.HttpContext;
            var accessToken = ctx.TokenEndpointResponse?.AccessToken 
                ?? ctx.Properties?.GetTokenValue("access_token");
            var idToken = ctx.TokenEndpointResponse?.IdToken 
                ?? ctx.Properties?.GetTokenValue("id_token");

            http.Session.SetString("AuthToken", accessToken ?? "");
            http.Session.SetString("IdToken", idToken ?? "");

            var name = ctx.Principal?.FindFirst("name")?.Value;
            if (name != null)
            {
                http.Session.SetString("UserName", name);
            }
            
            var email = ctx.Principal?.FindFirst("email")?.Value 
                ?? ctx.Principal?.FindFirst("preferred_username")?.Value;
            
            // Hacky solution to set admin based on specific email address! This would ideally be done with roles/groups.
            var isAdmin = email?.Equals("17-htc@20251117htcaz204.onmicrosoft.com", StringComparison.OrdinalIgnoreCase) ?? false;
            http.Session.SetString("IsAdmin", isAdmin.ToString().ToLower());
        };
});

builder.Services.AddControllersWithViews().AddMicrosoftIdentityUI();

builder.Services.AddTransient<AuthHandler>();
builder.Services.AddTransient<LoggingHandler>();

builder.Services.AddHttpClient<IContosoAPI>(client => {
    client.BaseAddress = new Uri(builder.Configuration["BackendUrl"]);
})
.AddHttpMessageHandler<AuthHandler>()
.AddTypedClient(client => RestService.For<IContosoAPI>(client));

// Blob image service for resolving ReleaseDate metadata logic.
builder.Services.AddSingleton<IBlobImageService, BlobImageService>();


var app = builder.Build();

// Use forwarded headers BEFORE other middleware
app.UseForwardedHeaders();

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
    http.Session.Remove("IsAdmin");
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
