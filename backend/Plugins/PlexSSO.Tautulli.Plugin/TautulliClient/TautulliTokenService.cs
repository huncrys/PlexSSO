using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PlexSSO.Model;
using PlexSSO.Model.Internal;
using PlexSSO.Model.Types;
using PlexSSO.Service;
using PlexSSO.Service.Config;
using PlexSSO.Tautulli.Plugin.Model;

namespace PlexSSO.Tautulli.Plugin.TautulliClient
{
    public class TautulliTokenService : ITokenService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfigurationService _configurationService;

        public TautulliTokenService(IHttpClientFactory clientFactory,
                                    IConfigurationService configurationService)
        {
            _httpClientFactory = clientFactory;
            _configurationService = configurationService;
        }

        public bool Matches((Protocol, string, string) redirectComponents)
        {
            var (_, hostname, _) = redirectComponents;
            return GetHostname().Contains(hostname);
        }

        public async Task<AuthenticationToken> GetServiceToken(Identity identity)
        {
            var cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler { CookieContainer = cookieContainer };
            using var httpClient = new HttpClient(handler);

            var hostname = GetHostname();

            // Perform a GET on the sign-in page first so Tautulli sets its CSRF cookie.
            var getRequest = new HttpRequestMessage(HttpMethod.Get, hostname + "/auth/signin");
            getRequest.Headers.Add("Accept", "text/html");
            getRequest.Headers.Add("User-Agent", "PlexSSO/2");
            await httpClient.SendAsync(getRequest);

            // Extract the CSRF token from the cookie Tautulli just set.
            var cookies = cookieContainer.GetCookies(new Uri(hostname));
            var csrfToken = cookies["_csrf_token"]?.Value
                         ?? cookies.Cast<Cookie>()
                                   .FirstOrDefault(c => c.Name.Contains("csrf", StringComparison.OrdinalIgnoreCase))
                                   ?.Value
                         ?? string.Empty;

            var request = new HttpRequestMessage(HttpMethod.Post, hostname + "/auth/signin");
            request.Content = new StringContent(
                $"username=&password=&token={identity.AccessToken.Value}&remember_me=1&_csrf_token={Uri.EscapeDataString(csrfToken)}",
                Encoding.UTF8,
                "application/x-www-form-urlencoded");
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("User-Agent", "PlexSSO/2");

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            var tautulliResponse = JsonSerializer.Deserialize<TautulliTokenResponse>(json, new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            if (string.IsNullOrWhiteSpace(tautulliResponse?.Token) || tautulliResponse.Status != "success")
            {
                return null;
            }

            return new AuthenticationToken(
                "tautulli_token_" + tautulliResponse.UUID,
                tautulliResponse.Token,
                DateTimeOffset.Now.AddDays(Constants.RedirectCookieExpireDays),
                "/home"
            );
        }

        private string GetHostname()
        {
            return _configurationService
                .GetPluginConfig<TautulliConfig>(TautulliConstants.PluginName)
                .PublicHostname ?? "";
        }
    }
}
