using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using MyAccountingApp.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();

string apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? string.Empty;
Uri baseAddress = Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out Uri? uri)
    ? uri
    : new Uri(builder.HostEnvironment.BaseAddress);

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = baseAddress });

await builder.Build().RunAsync();
