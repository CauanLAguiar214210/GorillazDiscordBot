using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace GorillazDiscordBot.Service
{
    internal class CotacaoService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task<Dictionary<string, decimal?>> ObterCotacoesAsync(params string[] codigosMoeda)
        {
            if (codigosMoeda == null || codigosMoeda.Length == 0)
                throw new ArgumentException("Informe ao menos um código de moeda");

            // Construir a query com todos os códigos separados por vírgula
            string query = string.Join(",", codigosMoeda);
            string url = $"https://economia.awesomeapi.com.br/json/last/{query}";

            var resultado = new Dictionary<string, decimal?>();

            try
            {
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);

                foreach (var codigo in codigosMoeda)
                {
                    // Remove o traço para formar a chave ex: "USDBRL" de "USD-BRL"
                    var chave = codigo.Replace("-", "");

                    var valorStr = (string)json[chave]?["bid"];
                    if (decimal.TryParse(valorStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var valor))
                    {
                        resultado[codigo] = valor;
                    }
                    else
                    {
                        resultado[codigo] = null;
                    }
                }
            }
            catch
            {
                foreach (var codigo in codigosMoeda)
                {
                    resultado[codigo] = null;
                }
            }

            return resultado;
        }
    }
}
