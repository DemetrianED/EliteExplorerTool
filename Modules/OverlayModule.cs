using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EliteExplorerTool.Modules
{
    public class OverlayModule : IEliteModule
    {
        public string ModuleName => "Overlay";

        private OverlayForm overlay;
        private string journalFolder;
        private System.Windows.Forms.Timer statusTimer; // Timer explícito de WinForms
        private string currentTargetSystem = "";

        public OverlayModule(string journalPath)
        {
            this.journalFolder = journalPath;
        }

        public Control GetControl() => new Panel { Visible = false };

        public void OnLoad()
        {
            if (Properties.Settings.Default.OverlayEnabled)
            {
                StartOverlay();
            }
        }

        public void OnShutdown()
        {
            StopOverlay();
        }

        public void ToggleOverlay(bool enable)
        {
            if (enable) StartOverlay();
            else StopOverlay();
        }

        private void StartOverlay()
        {
            if (overlay == null || overlay.IsDisposed)
            {
                overlay = new OverlayForm();
                overlay.Show();
            }

            if (statusTimer == null)
            {
                statusTimer = new System.Windows.Forms.Timer { Interval = 500 };
                statusTimer.Tick += CheckGameStatus;
                statusTimer.Start();
            }
        }

        private void StopOverlay()
        {
            statusTimer?.Stop();
            statusTimer = null;
            overlay?.Close();
            overlay = null;
        }

        // --- LÓGICA DE VISIBILIDAD (Status.json) ---
        private void CheckGameStatus(object sender, EventArgs e)
        {
            if (overlay == null) return;

            string statusPath = Path.Combine(journalFolder, "Status.json");
            if (File.Exists(statusPath))
            {
                try
                {
                    using (var fs = new FileStream(statusPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs))
                    {
                        string json = sr.ReadToEnd();
                        using (JsonDocument doc = JsonDocument.Parse(json))
                        {
                            // GuiFocus Codes:
                            // 0 = NoFocus (Nave)
                            // 6 = Galaxy Map
                            // 7 = System Map
                            // 9 = FSS Mode
                            int guiFocus = doc.RootElement.TryGetProperty("GuiFocus", out var gf) ? gf.GetInt32() : 0;

                            // CAMBIO: Permitir ver el overlay en Nave, Mapa Galaxia, Mapa Sistema y FSS
                            bool visible = (guiFocus == 0 || guiFocus == 6 || guiFocus == 7 || guiFocus == 9);

                            overlay.SetVisibility(visible);
                        }
                    }
                }
                catch { }
            }
        }

        // --- LÓGICA DE DATOS (Journal) ---
        public void HandleJournalEvent(string eventType, JsonElement root)
        {
            if (overlay == null) return;

            // Detectar Target de Salto
            if (eventType == "FSDTarget")
            {
                if (root.TryGetProperty("Name", out var n))
                {
                    string sysName = n.GetString();
                    UpdateOverlayData(sysName);
                }
            }
        }

        public void HandleHistoryEvent(string eventType, JsonElement root) { }

        private async void UpdateOverlayData(string systemName)
        {
            if (currentTargetSystem == systemName) return;
            currentTargetSystem = systemName;

            overlay.SetSystemName(systemName);
            overlay.SetDiscoveryStatus("Checking EDSM...", Color.Gray);

            bool known = await EdsmService.IsSystemKnown(systemName);

            if (known)
                overlay.SetDiscoveryStatus("DISCOVERED (EDSM)", Color.LightGreen);
            else
                overlay.SetDiscoveryStatus("UNDISCOVERED !!", Color.Red); // Rojo alerta para explorar
        }

        // --- CLASE INTERNA: LA VENTANA FLOTANTE ---
        private class OverlayForm : Form
        {
            private Label lblTitle;
            private Label lblSystem;
            private Label lblStatus;
            private Point mouseOffset;
            private bool isMouseDown = false;

            public OverlayForm()
            {
                this.FormBorderStyle = FormBorderStyle.None;
                this.ShowInTaskbar = false;
                this.TopMost = true;
                this.Size = new Size(300, 80);
                this.BackColor = Color.Black;
                this.TransparencyKey = Color.Black;

                // --- CORRECCIÓN DE PERSISTENCIA ---
                // "Manual" obliga a Windows a respetar las coordenadas X,Y exactas
                this.StartPosition = FormStartPosition.Manual;

                Point savedLoc = Properties.Settings.Default.OverlayLocation;
                // Evitar que aparezca fuera de pantalla si es la primera vez (0,0)
                if (savedLoc.X == 0 && savedLoc.Y == 0) savedLoc = new Point(100, 100);

                this.Location = savedLoc;
                // ----------------------------------

                lblTitle = CreateLabel("NEXT JUMP:", 10, 8, Color.Orange, 8);
                lblSystem = CreateLabel("---", 25, 14, Color.White, 14, true);
                lblStatus = CreateLabel("Waiting...", 55, 10, Color.Gray, 10, true);

                this.Controls.Add(lblTitle);
                this.Controls.Add(lblSystem);
                this.Controls.Add(lblStatus);

                BindDragEvents(this);
                BindDragEvents(lblTitle);
                BindDragEvents(lblSystem);
                BindDragEvents(lblStatus);
            }

            private Label CreateLabel(string text, int y, float size, Color color, int h, bool bold = false)
            {
                return new Label
                {
                    Text = text,
                    Location = new Point(5, y),
                    AutoSize = true,
                    ForeColor = color,
                    BackColor = Color.Transparent,
                    Font = new Font("Arial", size, bold ? FontStyle.Bold : FontStyle.Regular)
                };
            }

            public void SetSystemName(string name) => Invoke((Action)(() => lblSystem.Text = name.ToUpper()));

            public void SetDiscoveryStatus(string status, Color c) => Invoke((Action)(() => {
                lblStatus.Text = status;
                lblStatus.ForeColor = c;
            }));

            public void SetVisibility(bool visible)
            {
                if (this.IsDisposed) return;
                if (this.InvokeRequired) { this.Invoke(new Action(() => SetVisibility(visible))); return; }

                if (this.Visible != visible) this.Visible = visible;
            }

            private void BindDragEvents(Control c)
            {
                c.MouseDown += (s, e) => {
                    if (e.Button == MouseButtons.Left)
                    {
                        mouseOffset = new Point(-e.X, -e.Y);
                        isMouseDown = true;
                    }
                };
                c.MouseMove += (s, e) => {
                    if (isMouseDown)
                    {
                        Point mousePos = Control.MousePosition;
                        mousePos.Offset(mouseOffset.X, mouseOffset.Y);
                        this.Location = mousePos;
                    }
                };
                c.MouseUp += (s, e) => {
                    if (isMouseDown)
                    {
                        isMouseDown = false;
                        // Guardamos la posición cada vez que soltamos el mouse
                        Properties.Settings.Default.OverlayLocation = this.Location;
                        Properties.Settings.Default.Save();
                    }
                };
            }

            protected override bool ShowWithoutActivation => true;
        }
    }
}