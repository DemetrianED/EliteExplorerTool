using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace EliteExplorerTool
{
    public static class EdsmService
    {
        private static readonly HttpClient client = new HttpClient();

        // Verifica si tenemos los datos necesarios (aunque para consultar cuerpos no siempre hace falta API Key, es bueno tenerla)
        public static bool IsConfigured()
        {
            // Para consultas públicas de cuerpos, EDSM no exige API Key estricta, 
            // pero si la tienes configurada, genial.
            return true;
        }

        // Método asíncrono para obtener los cuerpos de un sistema
        public static async Task<string> GetBodies(string systemName)
        {
            try
            {
                // Codificamos el nombre del sistema para URL (ej: "Sol" -> "Sol", "Alpha Centauri" -> "Alpha%20Centauri")
                string encodedSystem = Uri.EscapeDataString(systemName);

                // URL oficial de EDSM para obtener cuerpos
                string url = $"https://www.edsm.net/api-system-v1/bodies?systemName={encodedSystem}";

                // Hacemos la petición
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                // Devolvemos el JSON crudo
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                // Si falla (sin internet, EDSM caído), devolvemos null
                return null;
            }
        }
    }
}