using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using System.Collections.Generic;

namespace EliteExplorerTool.Modules
{
    public class GameRouteModule : IEliteModule
    {
        public string ModuleName => "Game Route";

        private DataGridView grid;
        private string journalFolder;
        private FileSystemWatcher watcher;
        private Control parentControl; // Referencia para invocar cambios en la UI

        public GameRouteModule(string journalFolder)
        {
            this.journalFolder = journalFolder;
            SetupGrid();
        }

        public Control GetControl()
        {
            return grid;
        }

        private void SetupGrid()
        {
            grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.BackgroundColor = Color.Black;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.GridColor = Color.FromArgb(20, 20, 20);
            grid.AllowUserToAddRows = false;
            grid.ReadOnly = true;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.EnableHeadersVisualStyles = false;

            // Estilo Encabezados
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 30, 30);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Arial", 9, FontStyle.Bold);
            grid.ColumnHeadersHeight = 40;

            // Estilo Celdas
            grid.DefaultCellStyle.BackColor = Color.FromArgb(20, 20, 20);
            grid.DefaultCellStyle.ForeColor = Color.White;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(50, 50, 50);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.DefaultCellStyle.Font = new Font("Consolas", 9);
            grid.RowTemplate.Height = 35;

            // Columnas
            grid.Columns.Add("System", "System Name");
            grid.Columns.Add("StarClass", "Star Class");
            grid.Columns.Add("Scoopable", "Scoopable");

            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // Guardamos referencia al control para Invoke
            parentControl = grid;
        }

        public void OnLoad()
        {
            // Cargar ruta existente al iniciar
            CheckNavRoute();

            // Iniciar vigilancia del archivo
            SetupWatcher();
        }

        public void OnShutdown()
        {
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
        }

        // No usamos estos eventos del Journal en este módulo específico, 
        // pero debemos cumplir con la interfaz.
        public void HandleJournalEvent(string eventType, JsonElement eventData) { }
        public void HandleHistoryEvent(string eventType, JsonElement eventData) { }

        // --- LÓGICA ESPECÍFICA DEL MÓDULO ---

        private void SetupWatcher()
        {
            if (string.IsNullOrEmpty(journalFolder) || !Directory.Exists(journalFolder)) return;

            watcher = new FileSystemWatcher();
            watcher.Path = journalFolder;
            watcher.Filter = "NavRoute.json";
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size;

            // Evento cuando el archivo cambia
            watcher.Changed += (s, e) => {
                // Pequeña pausa para asegurar que el juego terminó de escribir
                System.Threading.Thread.Sleep(100);
                // Usamos Invoke para modificar la UI desde el hilo del Watcher
                if (grid != null && !grid.IsDisposed)
                    grid.Invoke(new Action(CheckNavRoute));
            };

            watcher.EnableRaisingEvents = true;
        }

        private void CheckNavRoute()
        {
            string navPath = Path.Combine(journalFolder, "NavRoute.json");
            if (!File.Exists(navPath)) return;

            try
            {
                var lines = ReadLinesSafely(navPath);
                if (lines.Count == 0) return;

                string fullJson = string.Join("", lines);

                using (JsonDocument doc = JsonDocument.Parse(fullJson))
                {
                    if (doc.RootElement.TryGetProperty("Route", out JsonElement routeArray))
                    {
                        grid.Rows.Clear();
                        foreach (var item in routeArray.EnumerateArray())
                        {
                            string sys = item.GetProperty("StarSystem").GetString();
                            string starClass = item.TryGetProperty("StarClass", out JsonElement sc) ? sc.GetString() : "?";
                            bool scoop = EliteLogic.IsScoopable(starClass);

                            int idx = grid.Rows.Add(sys, starClass, scoop ? "✔ YES" : "✘ NO");

                            if (scoop)
                                grid.Rows[idx].Cells["Scoopable"].Style.ForeColor = Color.LightGreen;
                            else
                                grid.Rows[idx].Cells["Scoopable"].Style.ForeColor = Color.Salmon;
                        }
                    }
                }
            }
            catch { }
        }

        // Helper para leer sin bloquear (copiado para que el módulo sea independiente)
        private List<string> ReadLinesSafely(string filePath)
        {
            var lines = new List<string>();
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    while (!sr.EndOfStream)
                    {
                        string line = sr.ReadLine();
                        if (line != null) lines.Add(line);
                    }
                }
            }
            catch { }
            return lines;
        }
    }
}