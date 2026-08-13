using System.Net;
using Microsoft.AspNetCore.Components;

namespace MyAccountingApp.Web.Services;

public class VaultUnauthorizedHandler : DelegatingHandler
{
    private readonly NavigationManager _navigation;

    public VaultUnauthorizedHandler(NavigationManager navigation)
    {
        this._navigation = navigation;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            this._navigation.NavigateTo("/unlock");
        }

        return response;
    }
}
