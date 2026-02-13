using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace EliteExplorerTool.Modules
{
    public class SpanshRouteModule : IEliteModule
    {
        public string ModuleName => "Spansh Route";

        private DataGridView grid;
        private List<EliteLogic.SpanshJump> routeList;
        private string csvPath;
        private Action<string> onNextJumpChanged;

        public SpanshRouteModule(string csvPath, Action<string> nextJumpCallback)
        {
            this.csvPath = csvPath;
            this.onNextJumpChanged = nextJumpCallback;
            this.routeList = new List<EliteLogic.SpanshJump>();
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

            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 30, 30);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Arial", 9, FontStyle.Bold);
            grid.ColumnHeadersHeight = 40;

            grid.DefaultCellStyle.BackColor = Color.FromArgb(20, 20, 20);
            grid.DefaultCellStyle.ForeColor = Color.White;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(50, 50, 50);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.DefaultCellStyle.Font = new Font("Consolas", 9);
            grid.RowTemplate.Height = 35;

            // Columnas
            grid.Columns.Add("Check", "Done");
            grid.Columns.Add("System", "System Name");
            grid.Columns.Add("Jumps", "Jumps");
            grid.Columns.Add("Distance", "Distance (Ly)");

            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.Columns["Check"].FillWeight = 20;
        }

        public void OnLoad()
        {
            // Intentar cargar datos al iniciar
            LoadCsv();
        }

        public void OnShutdown() { }

        // --- MANEJO DE EVENTOS ---
        public void HandleJournalEvent(string eventType, JsonElement eventData)
        {
            if (eventType == "FSDJump" || eventType == "Location")
            {
                if (eventData.TryGetProperty("StarSystem", out JsonElement sysElement))
                {
                    CheckSystemArrival(sysElement.GetString());
                }
            }
        }

        public void HandleHistoryEvent(string eventType, JsonElement eventData)
        {
            HandleJournalEvent(eventType, eventData);
        }

        // --- LÓGICA INTERNA ---

        private void CheckSystemArrival(string currentSystem)
        {
            if (routeList == null || routeList.Count == 0) return;

            bool changed = false;
            for (int i = 0; i < routeList.Count; i++)
            {
                if (!routeList[i].IsDone && string.Equals(routeList[i].SystemName, currentSystem, StringComparison.OrdinalIgnoreCase))
                {
                    routeList[i].IsDone = true;
                    UpdateRowVisual(i, true);
                    changed = true;
                }
            }

            if (changed) UpdateNextJumpLabel();
        }

        private void UpdateRowVisual(int rowIndex, bool isDone)
        {
            UpdateGridSafe(() => {
                if (rowIndex < grid.Rows.Count)
                {
                    grid.Rows[rowIndex].Cells["Check"].Value = isDone ? "✔" : "☐";
                    grid.Rows[rowIndex].DefaultCellStyle.ForeColor = isDone ? Color.Gray : Color.White;
                }
            });
        }

        private void UpdateNextJumpLabel()
        {
            var next = routeList.FirstOrDefault(j => !j.IsDone);
            string text = (next != null) ? "Next Jump: " + next.SystemName : "Next Jump: ---";
            onNextJumpChanged?.Invoke(text);
        }

        private void LoadCsv()
        {
            if (string.IsNullOrEmpty(csvPath) || !File.Exists(csvPath)) return;

            try
            {
                var lines = File.ReadAllLines(csvPath);
                if (lines.Length < 2) return;

                var headers = lines[0].Split(',').Select(h => h.Trim('"')).ToList();
                int idxName = headers.FindIndex(h => h.Contains("System Name"));
                int idxJumps = headers.FindIndex(h => h.Contains("Jumps"));
                int idxDist = headers.FindIndex(h => h.Contains("Distance"));

                if (idxName == -1) return;

                routeList.Clear();

                // Preparar filas para agregar masivamente
                var rowsToAdd = new List<object[]>();

                for (int i = 1; i < lines.Length; i++)
                {
                    var cols = lines[i].Split(',').Select(c => c.Trim('"')).ToArray();
                    if (cols.Length <= idxName) continue;

                    string sysName = cols[idxName];
                    string jumps = (idxJumps != -1 && cols.Length > idxJumps) ? cols[idxJumps] : "1";
                    string dist = (idxDist != -1 && cols.Length > idxDist) ? cols[idxDist] : "0";

                    routeList.Add(new EliteLogic.SpanshJump { SystemName = sysName, IsDone = false });
                    rowsToAdd.Add(new object[] { "☐", sysName, jumps, dist });
                }

                // Actualizar Grid de forma segura
                UpdateGridSafe(() => {
                    grid.Rows.Clear();
                    foreach (var row in rowsToAdd) grid.Rows.Add(row);
                });

                UpdateNextJumpLabel();
            }
            catch { }
        }

        // Helper clave para evitar errores de Invoke al inicio
        private void UpdateGridSafe(Action action)
        {
            if (grid == null || grid.IsDisposed) return;

            // Si el Handle ya existe y necesitamos Invoke, lo usamos.
            if (grid.InvokeRequired)
            {
                grid.Invoke(action);
            }
            else
            {
                // Si estamos en el hilo UI (aunque no haya handle aún), ejecutamos directo.
                action();
            }
        }
    }
}