using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GlassBar
{
    /// <summary>Optionale KI-Analyse der Systemwerte über die Anthropic-Messages-API.</summary>
    internal static class AiAnalyzer
    {
        const string Model = "claude-haiku-5-5";
        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        public static string ApiKey()
        {
            var k = AppSettings.Current.ApiKey;
            return string.IsNullOrWhiteSpace(k) ? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") : k.Trim();
        }

        public static async Task<string> AskAsync(string snapshot)
        {
            string key = ApiKey();
            if (string.IsNullOrEmpty(key)) return "Kein API-Key hinterlegt – trage ihn in den Einstellungen ein.";

            string prompt = "Du bist der Assistent einer Windows-Taskleiste. Analysiere diese Systemwerte auf Deutsch in höchstens 4 kurzen Sätzen "
                          + "und gib 1–2 konkrete, sichere Tipps (ohne riskante Befehle).\n\n" + snapshot;
            var body = JsonSerializer.Serialize(new
            {
                model = Model,
                max_tokens = 400,
                messages = new[] { new { role = "user", content = prompt } }
            });

            try
            {
                using (var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages"))
                {
                    req.Headers.Add("x-api-key", key);
                    req.Headers.Add("anthropic-version", "2023-06-01");
                    req.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    using (var resp = await Http.SendAsync(req))
                    {
                        string json = await resp.Content.ReadAsStringAsync();
                        if (!resp.IsSuccessStatusCode) return "KI-Fehler (" + (int)resp.StatusCode + "): " + Shorten(json);
                        using (var doc = JsonDocument.Parse(json))
                            return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
                    }
                }
            }
            catch (Exception ex) { return "KI nicht erreichbar: " + ex.Message; }
        }

        static string Shorten(string s) => s.Length > 200 ? s.Substring(0, 200) + "…" : s;
    }
}
