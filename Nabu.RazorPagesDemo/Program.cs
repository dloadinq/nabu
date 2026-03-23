using Nabu.RazorPagesDemo;
using Nabu.RCL;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseWebRoot("wwwroot");
builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorPages();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

builder.Services.AddScoped<IWhisperSettings, WhisperSettingsService>();
builder.Services.AddScoped<WhisperAgentService>();
builder.Services.AddScoped<IWhisperHandler>(sp => sp.GetRequiredService<WhisperAgentService>());

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAntiforgery();
app.UseAuthorization();

app.MapRazorPages();

app.Run();