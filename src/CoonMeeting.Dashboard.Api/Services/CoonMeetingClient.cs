using System.Net;
using System.Net.Http.Headers;
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

    public async Task<List<CoonMeetingMeeting>> ListMeetingsAsync(string apiKey)
    {
        using var request = NewRequest(HttpMethod.Get, "/api/v1/meetings", apiKey);
        var response = await _http.SendAsync(request);
        await EnsureSuccessOrThrowAsync(response, "list meetings");
        return await response.Content.ReadFromJsonAsync<List<CoonMeetingMeeting>>(JsonOptions) ?? new();
    }

    public async Task<CoonMeetingMeeting?> GetMeetingAsync(string apiKey, string meetingId)
    {
        using var request = NewRequest(HttpMethod.Get, $"/api/v1/meetings/{meetingId}", apiKey);
        var response = await _http.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessOrThrowAsync(response, "get meeting");
        return await response.Content.ReadFromJsonAsync<CoonMeetingMeeting>(JsonOptions);
    }

    public async Task<CoonMeetingMeeting> CreateMeetingAsync(string apiKey, CreateCoonMeetingMeetingRequest request)
    {
        using var httpRequest = NewRequest(HttpMethod.Post, "/api/v1/meetings", apiKey);
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        var response = await _http.SendAsync(httpRequest);
        await EnsureSuccessOrThrowAsync(response, "create meeting");
        return await response.Content.ReadFromJsonAsync<CoonMeetingMeeting>(JsonOptions)
            ?? throw new InvalidOperationException("Coon.Meeting create meeting returned an empty response.");
    }

    public async Task<bool> UpdateMeetingAsync(string apiKey, string meetingId, UpdateCoonMeetingMeetingRequest request)
    {
        using var httpRequest = NewRequest(HttpMethod.Put, $"/api/v1/meetings/{meetingId}", apiKey);
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        var response = await _http.SendAsync(httpRequest);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        await EnsureSuccessOrThrowAsync(response, "update meeting");
        return true;
    }

    public async Task<bool> CancelMeetingAsync(string apiKey, string meetingId)
    {
        using var httpRequest = NewRequest(HttpMethod.Delete, $"/api/v1/meetings/{meetingId}", apiKey);
        var response = await _http.SendAsync(httpRequest);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        await EnsureSuccessOrThrowAsync(response, "cancel meeting");
        return true;
    }

    public async Task<CoonMeetingParticipantToken?> MintParticipantTokenAsync(string apiKey, string meetingId, string participantExternalId, string name)
    {
        using var httpRequest = NewRequest(HttpMethod.Post, $"/api/v1/meetings/{meetingId}/participant-tokens", apiKey);
        httpRequest.Content = JsonContent.Create(new { participantExternalId, name }, options: JsonOptions);

        var response = await _http.SendAsync(httpRequest);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        // 400 here means "meeting is cancelled" (ParticipantTokensController.Mint) - let it
        // surface as CoonMeetingApiException so the caller can tell that apart from a real failure.
        await EnsureSuccessOrThrowAsync(response, "mint participant token");
        return await response.Content.ReadFromJsonAsync<CoonMeetingParticipantToken>(JsonOptions);
    }

    public async Task<CoonMeetingAttendee> AddAttendeeAsync(string apiKey, string meetingId, CoonMeetingAttendeeRequest attendee)
    {
        using var httpRequest = NewRequest(HttpMethod.Post, $"/api/v1/meetings/{meetingId}/attendees", apiKey);
        httpRequest.Content = JsonContent.Create(attendee, options: JsonOptions);

        var response = await _http.SendAsync(httpRequest);
        await EnsureSuccessOrThrowAsync(response, "add attendee");
        return await response.Content.ReadFromJsonAsync<CoonMeetingAttendee>(JsonOptions)
            ?? throw new InvalidOperationException("Coon.Meeting add attendee returned an empty response.");
    }

    public Task KickParticipantAsync(string apiKey, string meetingId, string participantExternalId, string requestedByExternalId) =>
        PostModerationAsync(apiKey, meetingId, participantExternalId, "kick", requestedByExternalId);

    public Task BlockParticipantAsync(string apiKey, string meetingId, string participantExternalId, string requestedByExternalId) =>
        PostModerationAsync(apiKey, meetingId, participantExternalId, "block", requestedByExternalId);

    public Task UnblockParticipantAsync(string apiKey, string meetingId, string participantExternalId, string requestedByExternalId) =>
        PostModerationAsync(apiKey, meetingId, participantExternalId, "unblock", requestedByExternalId);

    private async Task PostModerationAsync(string apiKey, string meetingId, string participantExternalId, string action, string requestedByExternalId)
    {
        using var httpRequest = NewRequest(HttpMethod.Post, $"/api/v1/meetings/{meetingId}/participants/{participantExternalId}/{action}", apiKey);
        httpRequest.Content = JsonContent.Create(new { requestedByExternalId }, options: JsonOptions);

        var response = await _http.SendAsync(httpRequest);
        await EnsureSuccessOrThrowAsync(response, $"{action} participant");
    }

    private static HttpRequestMessage NewRequest(HttpMethod method, string path, string apiKey)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return request;
    }

    private static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, string action)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync();
        throw new CoonMeetingApiException((int)response.StatusCode, $"Coon.Meeting {action} failed ({(int)response.StatusCode}): {body}");
    }

    private class CreateTenantResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
    }
}
