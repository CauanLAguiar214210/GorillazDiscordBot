using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LuckyMonkey.Contracts.Bets;
using LuckyMonkey.Contracts.Common;
using LuckyMonkey.Contracts.Enums;
using LuckyMonkey.Contracts.Sessions;

namespace GorillazDiscordBot.Services;

/// <summary>Erro retornado pelo serviço de cassino (ou falha de comunicação com ele).</summary>
public sealed class CasinoApiException : Exception
{
    public CasinoApiException(ErrorCode code, string message) : base(message)
    {
        Code = code;
    }

    public ErrorCode Code { get; }
}

/// <summary>
/// Cliente HTTP do microserviço de cassino. O bot é o dono do dinheiro (débito/crédito/relíquias);
/// o serviço só orquestra o jogo e devolve o montante base a pagar em <see cref="Outcome.ReturnAmount"/>.
/// </summary>
public sealed class CasinoApiClient
{
    /// <summary>
    /// Mesmas convenções do serviço (camelCase + enums como string via <c>JsonStringEnumConverter</c>).
    /// Mantém <see cref="JsonStringEnumConverter"/> em modo padrão para continuar aceitando enums numéricos na leitura.
    /// </summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;

    public CasinoApiClient(HttpClient http)
    {
        _http = http;
    }

    public Task<BetResponse> StartBetAsync(GameKind game, Guid betId, ulong userId, ulong amount, BetOptions? options)
        => PostAsync<BetRequest, BetResponse>(userId, $"/casino/{game}/bets", new BetRequest(betId, amount, options) { UserId = userId });

    public Task<StakeResponse> AddStakeAsync(GameKind game, ulong userId, AddStakeRequest request)
        => PostAsync<AddStakeRequest, StakeResponse>(userId, $"/casino/{game}/sessions/stakes", request);

    public Task<ActionResponse> ActionAsync(GameKind game, ulong userId, string action, ActionPayload? payload = null)
        => PostAsync<ActionRequest, ActionResponse>(
            userId, $"/casino/{game}/sessions/actions/{action}", new ActionRequest(payload));

    private async Task<TResponse> PostAsync<TRequest, TResponse>(ulong userId, string path, TRequest body)
        where TRequest : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body, options: Json)
        };
        request.Headers.Authorization = System.Net.Http.Headers.AuthenticationHeaderValue.Parse(
            CasinoJwtProvider.BearerFor(userId));

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (HttpRequestException ex)
        {
            throw new CasinoApiException(ErrorCode.InvalidState, $"Não foi possível falar com o serviço de cassino: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            throw new CasinoApiException(ErrorCode.InvalidState, "O serviço de cassino demorou demais para responder.");
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    return (await response.Content.ReadFromJsonAsync<TResponse>(Json))!;
                }
                catch (JsonException ex)
                {
                    throw new CasinoApiException(
                        ErrorCode.InvalidState, $"O serviço de cassino devolveu uma resposta inválida: {ex.Message}");
                }
                catch (NotSupportedException ex)
                {
                    throw new CasinoApiException(
                        ErrorCode.InvalidState, $"O serviço de cassino devolveu uma resposta inválida: {ex.Message}");
                }
            }

            var rawBody = await response.Content.ReadAsStringAsync();
            var error = TryReadError(rawBody);
            if (error != null)
                throw new CasinoApiException(error.Code, error.Message);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new CasinoApiException(ErrorCode.InvalidState,
                    "O serviço de cassino rejeitou a autenticação (X-Api-Key ou token JWT incompatíveis com o serviço)." +
                    (string.IsNullOrWhiteSpace(rawBody) ? "" : $" Resposta: {rawBody}"));

            throw new CasinoApiException(ErrorCode.InvalidState,
                $"O serviço de cassino respondeu com erro inesperado ({(int)response.StatusCode})." +
                (string.IsNullOrWhiteSpace(rawBody) ? "" : $" Resposta: {rawBody}"));
        }
    }

    private static ApiError? TryReadError(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<ApiError>(body, Json);
        }
        catch
        {
            return null;
        }
    }
}