using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;

namespace EliteExplorerTool.Modules
{
    /// <summary>
    /// Contrato que deben cumplir todas las pestañas/plugins del programa.
    /// </summary>
    public interface IEliteModule
    {
        // Nombre del Módulo (Aparecerá en el Tab)
        string ModuleName { get; }

        // Inicialización: Aquí el módulo crea sus controles (Grids, Labels, etc.)
        // Recibe el Form principal por si necesita invocar acciones globales (como hablar)
        Control GetControl(); 

        // Eventos del Juego (Live)
        // Se llama cuando llega una línea nueva del Journal (FSDJump, Scan, etc.)
        void HandleJournalEvent(string eventType, JsonElement eventData);

        // Eventos de Carga (Historial)
        // Se llama al iniciar cuando leemos el historial hacia atrás
        void HandleHistoryEvent(string eventType, JsonElement eventData);

        // Opcional: Para guardar/cargar configuración propia del módulo
        void OnLoad();
        void OnShutdown();
    }
}