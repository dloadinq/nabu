using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Nabu;
using Nabu.BlazorDemo;
using Nabu.BlazorDemo.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddNabu()
    .AddHandler<SampleAgentService>()
    .AddNavigation(nav => nav
        .Map("/", "home", "homepage", "start")
        .Map("/counter", "counter")
        .Map("/weather", "weather")
        .Map("/form", "form", "formula", "contact"))
    .AddCommandsFromResource("commands.json");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/set-demo-cookie", (HttpContext context) =>
{
    const string cookieName = "DemoUserToken";

    context.Response.Cookies.Delete(cookieName);

    var firstNames = new[] { "Michael", "Sarah", "Christopher", "Jessica", "James", "Emily", "David", "Ashley" };
    var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Miller", "Davis", "Wilson" };
    var random = Random.Shared;

    var selectedFirst = firstNames[random.Next(firstNames.Length)];
    var selectedLast = lastNames[random.Next(lastNames.Length)];
    
    var randomPhone = $"+1 ({random.Next(200, 999)}) {random.Next(100, 999)}-{random.Next(1000, 9999)}";
    
    var randomBday = new DateTime(random.Next(1985, 2005), random.Next(1, 13), random.Next(1, 28)).ToString("yyyy-MM-dd");

    var secretKey = "This_key_has_to_be_as_long_as_possible!";
    var claims = new[]
    {
        new Claim("FirstName", selectedFirst),
        new Claim("LastName", selectedLast),
        new Claim("Phone", randomPhone),
        new Claim("Birthday", randomBday)
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken(claims: claims, expires: DateTime.Now.AddMinutes(30), signingCredentials: credentials);
    var jwtToken = new JwtSecurityTokenHandler().WriteToken(token);

    context.Response.Cookies.Append(cookieName, jwtToken, new CookieOptions
    {
        HttpOnly = true,
        Secure = true, 
        SameSite = SameSiteMode.Strict,
        Expires = DateTimeOffset.UtcNow.AddMinutes(30)
    });

    return Results.Redirect("/form");
});

app.Run();