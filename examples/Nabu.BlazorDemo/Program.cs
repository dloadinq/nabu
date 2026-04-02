using Nabu.BlazorDemo;
using Nabu.BlazorDemo.Components;
using Nabu;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddNabu()
    .AddHandler<SampleAgentService>()
    .AddNavigation(nav => nav
        .Map("/", "home", "homepage", "start")
        .Map("/counter", "counter")
        .Map("/weather", "weather"))
    .AddCommand("increment", [
        "increment the counter",
        "increment the counter by one",
        "increase the counter",
        "increase the counter by one",
        "increase the count",
        "add one to the counter",
        "plus one to the counter",
        "count up by one",
        "raise the counter",
        "raise the counter by one",
        "raise the counter by one",
        "raise the number by one"
    ], scope: "/counter")
    .AddCommand("reset", [
        "reset the counter",
        "reset the counter to zero",
        "set the counter to zero",
        "clear the counter",
        "start the counter over",
        "zero out the counter"
    ], scope: "/counter");

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

app.Run();