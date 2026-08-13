using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using MyAccountingApp.Web;
using MyAccountingApp.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();

// Redirects to the unlock screen when the API returns 401 (vault locked).
builder.Services.AddScoped<VaultUnauthorizedHandler>();

// Import of multi-year CSVs can exceed the default 100s timeout if the API is slow.
builder.Services.AddScoped(sp =>
{
    VaultUnauthorizedHandler handler = new(sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>())
    {
        InnerHandler = new HttpClientHandler(),
    };

    return new HttpClient(handler)
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
        Timeout = TimeSpan.FromMinutes(10),
    };
});

await builder.Build().RunAsync();
