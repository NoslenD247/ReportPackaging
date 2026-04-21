using System.Net.Http.Headers;

namespace ReportPackaging.Services
{
    public class ClerkAuthHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var loginType = await SecureStorage.GetAsync("login_type");
            var tokenKey  = loginType == "azure" ? "azure_JWT" : "clerk_JWT";
            var token     = await SecureStorage.GetAsync(tokenKey);

            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
