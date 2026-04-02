using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Nabu;
using Nabu.BlazorWasmDemo;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddNabu()
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

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();