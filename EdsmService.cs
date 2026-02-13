using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text;

namespace EliteExplorerTool
{
    public static class EdsmService
    {
        private static readonly HttpClient client = new HttpClient();

        public static bool IsConfigured()
        {
            return !string.IsNullOrEmpty(Properties.Settings.Default.EdsmApiKey) &&
                   !string.IsNullOrEmpty(Properties.Settings.Default.EdsmCmdr);
        }

        // 1. Obtener cuerpos (GET) - Para Current System y History
        public static async Task<string> GetBodies(string systemName)
        {
            try
            {
                string encodedSystem = Uri.EscapeDataString(systemName);
                string url = $"https://www.edsm.net/api-system-v1/bodies?systemName={encodedSystem}";

                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception)
            {
                return null;
            }
        }

        // 2. Enviar eventos (POST) - Para subir descubrimientos
        public static async Task SendJournalEvent(string jsonEvent)
        {
            string apiKey = Properties.Settings.Default.EdsmApiKey;
            string cmdr = Properties.Settings.Default.EdsmCmdr;

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(cmdr)) return;

            string url = $"https://www.edsm.net/api-journal-v1?commanderName={Uri.EscapeDataString(cmdr)}&apiKey={apiKey}&fromSoftware=EliteExplorationTool&fromSoftwareVersion=1.0";

            try
            {
                var content = new StringContent(jsonEvent, Encoding.UTF8, "application/json");
                await client.PostAsync(url, content);
            }
            catch
            {
                // Ignoramos errores de red silenciosamente
            }
        }

        // 3. Verificar si el sistema es conocido (GET Ligero) - Para el Overlay
        public static async Task<bool> IsSystemKnown(string systemName)
        {
            if (string.IsNullOrEmpty(systemName)) return false;
            try
            {
                string encoded = Uri.EscapeDataString(systemName);
                // Usamos 'showId=1' porque es la query más liviana de EDSM para saber si existe
                string url = $"https://www.edsm.net/api-v1/system?systemName={encoded}&showId=1";

                string json = await client.GetStringAsync(url);

                // Si devuelve un array vacío "[]" o null, no existe. Si devuelve un objeto con ID, existe.
                return !string.IsNullOrEmpty(json) && json.Contains("\"name\"");
            }
            catch
            {
                return false; // Ante error, asumimos desconocido
            }
        }
    }
}