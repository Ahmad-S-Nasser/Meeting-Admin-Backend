using System.Net.Http.Json;
using System.Text.Json;
using CoonMeeting.Dashboard.Api.Config;

namespace CoonMeeting.Dashboard.Api.Services;

public class CoonMeetingClient : ICoonMeetingClient
{
    // Coon.Meeting's own controllers use ASP.NET Core's default MVC JSON formatter, which is
    // camelCase and case-insensitive - matching that explicitly here rather than relying on
    // System.Text.Json's raw (case-SENSITIVE) defaults, which would silently leave Id/ApiKey
    // empty on every response instead of throwing.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly CoonMeetingSettings _settings;

    public CoonMeetingClient(HttpClient http, CoonMeetingSettings settings)
    {
        _http = http;
        _settings = settings;
        _http.BaseAddress = new Uri(settings.ApiBaseUrl);
    }

    public async Task<CoonMeetingTenant> CreateTenantAsync(string name, List<string> allowedOrigins)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tenants")
        {
            Content = JsonContent.Create(new { name, allowedOrigins, live = false }),
        };
        request.Headers.Add("X-Admin-Provisioning-Key", _settings.AdminProvisioningKey);

        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Coon.Meeting tenant provisioning failed ({(int)response.StatusCode}): {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<CreateTenantResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Coon.Meeting tenant provisioning returned an empty response.");

        return new CoonMeetingTenant { TenantId = result.Id, ApiKey = result.ApiKey };
    }

    private class CreateTenantResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
    }
}
