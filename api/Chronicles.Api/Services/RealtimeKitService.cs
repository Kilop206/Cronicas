using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Chronicles.Api.Services;

public sealed class RealtimeKitService(IHttpClientFactory clients, IConfiguration config)
{
    private string? Account => config["CF_ACCOUNT_ID"];
    private string? App => config["CF_RTK_APP_ID"];
    private string? Token => config["CF_RTK_API_TOKEN"];
    public string? Preset => config["CF_RTK_PRESET"];
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Account) && !string.IsNullOrWhiteSpace(App)
        && !string.IsNullOrWhiteSpace(Token) && !string.IsNullOrWhiteSpace(Preset);

    public async Task<Guid> CreateMeetingAsync(string title, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, "meetings", new { title }, cancellationToken);
        var data = await ReadDataAsync(response, cancellationToken);
        return data.GetProperty("id").GetGuid();
    }

    public async Task<string> AddParticipantAsync(Guid meetingId, Guid userId, string name, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, $"meetings/{meetingId}/participants",
            new { custom_participant_id = userId.ToString(), preset_name = Preset, name }, cancellationToken);
        var data = await ReadDataAsync(response, cancellationToken);
        return data.GetProperty("token").GetString() ?? throw new InvalidOperationException("RealtimeKit não retornou um token.");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, $"https://api.cloudflare.com/client/v4/accounts/{Account}/realtime/kit/{App}/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        request.Content = JsonContent.Create(body);
        return await clients.CreateClient(nameof(RealtimeKitService)).SendAsync(request, cancellationToken);
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("RealtimeKit indisponível.", null, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (!document.RootElement.GetProperty("success").GetBoolean()) throw new InvalidOperationException("RealtimeKit rejeitou a solicitação.");
        return document.RootElement.GetProperty("data").Clone();
    }
}
