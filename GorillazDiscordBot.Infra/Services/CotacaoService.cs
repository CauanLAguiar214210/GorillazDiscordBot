using System.Net.Http;
using Newtonsoft.Json.Linq;
using GorillazDiscordBot.Services.Interfaces;

namespace GorillazDiscordBot.Services;

public class CotacaoService : ICotacaoService
{
    private readonly HttpClient _httpClient;

    public CotacaoService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Dictionary<string, decimal?>> ObterCotacoesAsync(params string[] codigosMoeda)
    {
        if (codigosMoeda == null || codigosMoeda.Length == 0)
            throw new ArgumentException("Informe ao menos um código de moeda");

        string query = string.Join(",", codigosMoeda);
        string url = $"https://economia.awesomeapi.com.br/json/last/{query}";

        var resultado = new Dictionary<string, decimal?>();

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            var json = JObject.Parse(response);

            foreach (var codigo in codigosMoeda)
            {
                var chave = codigo.Replace("-", "");
                    var valorStr = json[chave]?["bid"]?.ToString();
                if (decimal.TryParse(valorStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var valor))
                    resultado[codigo] = valor;
                else
                    resultado[codigo] = null;
            }
        }
        catch
        {
            foreach (var codigo in codigosMoeda)
                resultado[codigo] = null;
        }

        return resultado;
    }
}
