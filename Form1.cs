using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using System.Speech.Synthesis;
using System.Threading.Tasks;

namespace EliteExplorerTool
{
    public partial class Form1 : Form
    {
        // --- CONTROLES UI ---
        private Label? lblTitle, lblSystem, lblStatus, lblNextJump;
        private PictureBox? btnSettings;
        private DarkTabControl? mainTabs;
        private Button btnEdsmUI = null!;
        private Button btnInaraUI = null!;
        private Button btnSpanshUI = null!;

        // --- SISTEMA ---
        private System.Windows.Forms.Timer? logTimer;
        private string journalFolder = "";
        private string currentFile = "";
        private long lastFileSize = 0;
        private SpeechSynthesizer? voice;
        private string currentSystem = "";

        // --- MÓDULOS ---
        private List<Modules.IEliteModule> loadedModules = new List<Modules.IEliteModule>();
        private Modules.CurrentSystemModule? currentSystemMod;
        private Modules.HistoryModule? historyMod;
        private Modules.OverlayModule? overlayMod;

        public Form1()
        {
            InitializeComponent();
            SetupVoice();
            SetupInterface();
            // NOTA: Ya no llamamos a SetupLogic() aquí para evitar el bloqueo.
        }

        // --- CORRECCIÓN CLAVE: Iniciar lógica cuando la ventana ya existe ---
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            SetupLogic();
        }

        private void SetupVoice()
        {
            try { voice = new SpeechSynthesizer(); voice.SetOutputToDefaultAudioDevice(); } catch { }
        }

        private void Speak(string message)
        {
            if (Properties.Settings.Default.VoiceEnabled && voice != null)
                try { voice.SpeakAsync(message); } catch { }
        }

        private void SetupInterface()
        {

            this.Text = "Elite Exploration Tool - Modular V1.0";
            this.Size = new Size(1400, 800);
            this.BackColor = Color.FromArgb(10, 10, 15);

            btnSettings = new PictureBox { Image = Properties.Resources.iconSettings, SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(32, 32), Location = new Point(this.ClientSize.Width - 60, 20), Anchor = AnchorStyles.Top | AnchorStyles.Right, Cursor = Cursors.Hand };
            btnSettings.Click += BtnSettings_Click;
            this.Controls.Add(btnSettings);

            lblTitle = new Label { Text = "CURRENT SYSTEM:", Location = new Point(20, 20), AutoSize = true, Font = new Font("Arial", 10, FontStyle.Bold), ForeColor = Color.Gray };
            this.Controls.Add(lblTitle);

            lblSystem = new Label { Text = "Detecting...", Location = new Point(20, 45), AutoSize = true, Font = new Font("Arial", 22, FontStyle.Bold), ForeColor = Color.Orange };
            this.Controls.Add(lblSystem);

            lblStatus = new Label { Text = "Initializing...", Location = new Point(20, 95), AutoSize = true, Font = new Font("Arial", 12, FontStyle.Italic), ForeColor = Color.LightGray };
            this.Controls.Add(lblStatus);

            lblNextJump = new Label { Text = "Next Jump: ---", Location = new Point(20, 120), AutoSize = true, Font = new Font("Arial", 11, FontStyle.Bold), ForeColor = Color.Cyan, Cursor = Cursors.Hand };
            lblNextJump.Click += (s, e) => {
                if (lblNextJump != null && lblNextJump.Text.Contains(": "))
                {
                    string sys = lblNextJump.Text.Split(new[] { ": " }, StringSplitOptions.None)[1];
                    if (sys != "---" && lblStatus != null) { Clipboard.SetText(sys); lblStatus.Text = "Copied: " + sys; }
                }
            };
            this.Controls.Add(lblNextJump);

            mainTabs = new DarkTabControl { Location = new Point(20, 155), Size = new Size(1340, 560), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, Padding = new Point(20, 6) };
            this.Controls.Add(mainTabs);

            // --- BOTONES WEB (Debajo de Settings) ---
            int btnW = 60; // Ancho del botón
            int btnH = 25; // Alto del botón
            int spacing = 5;
            // Calculamos X para que el último botón termine alineado con el de Settings
            int startX = btnSettings.Location.X + btnSettings.Width - (btnW * 3 + spacing * 2);
            int startY = btnSettings.Location.Y + btnSettings.Height + 10;

            btnEdsmUI = CreateWebButton("EDSM", new Point(startX, startY), Color.FromArgb(0, 100, 200));
            btnEdsmUI.Click += (s, e) => OpenWebSystem("EDSM");
            this.Controls.Add(btnEdsmUI);

            btnInaraUI = CreateWebButton("INARA", new Point(startX + btnW + spacing, startY), Color.FromArgb(180, 140, 0));
            btnInaraUI.Click += (s, e) => OpenWebSystem("Inara");
            this.Controls.Add(btnInaraUI);

            btnSpanshUI = CreateWebButton("SPANSH", new Point(startX + (btnW + spacing) * 2, startY), Color.FromArgb(100, 0, 150));
            btnSpanshUI.Click += (s, e) => OpenWebSystem("Spansh");
            this.Controls.Add(btnSpanshUI);

        }

        private void SetupLogic()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] possiblePaths = {
                Path.Combine(userProfile, "Saved Games", "Frontier Developments", "Elite Dangerous"),
                Path.Combine(userProfile, "OneDrive", "Saved Games", "Frontier Developments", "Elite Dangerous"),
                Path.Combine(userProfile, "Juegos guardados", "Frontier Developments", "Elite Dangerous")
            };

            foreach (string path in possiblePaths) if (Directory.Exists(path)) { journalFolder = path; break; }

            if (string.IsNullOrEmpty(journalFolder))
            {
                if (lblStatus != null) lblStatus.Text = "Error: Journal folder missing.";
                MessageBox.Show("Journal folder not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (lblStatus != null) lblStatus.Text = "Folder found. Starting modules...";

            // Cargar Módulos
            currentSystemMod = new Modules.CurrentSystemModule((msg) => Speak(msg));
            LoadModule(currentSystemMod);

            LoadModule(new Modules.GameRouteModule(journalFolder));
            ReloadSpanshModule();

            // --- Módulo de Historial ---
            historyMod = new Modules.HistoryModule(journalFolder);
            LoadModule(historyMod);

            // --- NUEVO: Overlay Module ---
            // Instanciamos el overlay pero NO usamos LoadModule (para no crear pestaña)
            overlayMod = new Modules.OverlayModule(journalFolder);
            loadedModules.Add(overlayMod); // Lo añadimos a la lista para recibir eventos
            overlayMod.OnLoad();           // Lo iniciamos manualmente (verifica configuración interna)

            // Iniciar lectura en hilo separado
            Task.Run(() => PerformFullSync());

            logTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            logTimer.Tick += MonitorLive;
            logTimer.Start();
        }

        private void LoadModule(Modules.IEliteModule module)
        {
            if (mainTabs == null) return;
            loadedModules.Add(module);
            TabPage tab = new TabPage(module.ModuleName);
            tab.BackColor = Color.FromArgb(15, 15, 20);
            tab.Controls.Add(module.GetControl());
            mainTabs.TabPages.Add(tab);
            module.OnLoad();
        }

        private void ReloadSpanshModule()
        {
            if (mainTabs == null) return;
            string spanshPath = Properties.Settings.Default.SpanshCsvPath ?? "";

            var existingMod = loadedModules.FirstOrDefault(m => m.ModuleName == "Spansh Route");
            if (existingMod != null)
            {
                loadedModules.Remove(existingMod);
                TabPage? tabToRemove = null;
                foreach (TabPage tab in mainTabs.TabPages) { if (tab.Text == "Spansh Route") { tabToRemove = tab; break; } }
                if (tabToRemove != null) mainTabs.TabPages.Remove(tabToRemove);
            }

            var spanshMod = new Modules.SpanshRouteModule(spanshPath, (text) => {
                this.Invoke(new Action(() => { if (lblNextJump != null) lblNextJump.Text = text; }));
            });

            loadedModules.Add(spanshMod);
            TabPage tabSpansh = new TabPage(spanshMod.ModuleName) { BackColor = Color.FromArgb(15, 15, 20) };
            tabSpansh.Controls.Add(spanshMod.GetControl());

            if (mainTabs.TabCount >= 2) mainTabs.TabPages.Insert(2, tabSpansh);
            else mainTabs.TabPages.Add(tabSpansh);

            spanshMod.OnLoad();
            if ((string.IsNullOrEmpty(spanshPath) || !File.Exists(spanshPath)) && lblNextJump != null) lblNextJump.Text = "Next Jump: ---";
        }

        private void BtnSettings_Click(object? sender, EventArgs e)
        {
            SettingsForm settingsWindow = new SettingsForm();
            if (settingsWindow.ShowDialog() == DialogResult.OK)
            {
                SetupVoice();
                if (currentSystemMod != null) currentSystemMod.ApplySettings();
                ReloadSpanshModule();

                // --- NUEVO: Actualizar estado del Overlay ---
                if (overlayMod != null)
                    overlayMod.ToggleOverlay(Properties.Settings.Default.OverlayEnabled);
            }
        }

        private void MonitorLive(object? sender, EventArgs e)
        {
            try
            {
                var fileInfo = new FileInfo(currentFile);
                fileInfo.Refresh();

                var directory = new DirectoryInfo(journalFolder);
                var newest = directory.GetFiles("Journal.*.log").OrderByDescending(f => f.LastWriteTime).FirstOrDefault();

                if (newest != null && newest.FullName != currentFile)
                {
                    currentFile = newest.FullName;
                    lastFileSize = 0;
                    return;
                }

                if (fileInfo.Length > lastFileSize)
                {
                    using (var fs = new FileStream(currentFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs))
                    {
                        fs.Seek(lastFileSize, SeekOrigin.Begin);
                        while (!sr.EndOfStream)
                        {
                            string? line = sr.ReadLine();
                            if (!string.IsNullOrWhiteSpace(line)) ProcessLiveLine(line);
                        }
                        lastFileSize = fs.Position;
                    }
                }
            }
            catch { }
        }

        private void ProcessLiveLine(string line)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(line))
                {
                    JsonElement root = doc.RootElement;
                    if (!root.TryGetProperty("event", out JsonElement evt)) return;
                    string? e = evt.GetString();
                    if (e == null) return;

                    foreach (var module in loadedModules) module.HandleJournalEvent(e, root);

                    if (e == "FSDJump" || e == "Location")
                    {
                        currentSystem = root.GetProperty("StarSystem").GetString() ?? "Unknown";
                        UpdateHeader(currentSystem, Color.Orange);
                        Speak($"Arrived at {currentSystem}");
                    }
                    else if (e == "StartJump" && root.TryGetProperty("JumpType", out JsonElement jt) && jt.GetString() == "Hyperspace")
                    {
                        string dest = root.GetProperty("StarSystem").GetString() ?? "Unknown";
                        if (lblTitle != null) { lblTitle.Text = "JUMPING TO:"; lblTitle.ForeColor = Color.Yellow; }
                        if (lblSystem != null) { lblSystem.Text = dest; lblSystem.ForeColor = Color.Yellow; }
                        Speak("Charging Frame Shift Drive.");
                    }
                }
            }
            catch { }
        }

        private void UpdateHeader(string sysName, Color color)
        {
            if (lblSystem != null) { lblSystem.Text = sysName; lblSystem.ForeColor = color; }
            if (lblTitle != null) { lblTitle.Text = "CURRENT SYSTEM:"; lblTitle.ForeColor = Color.Gray; }
        }

        // --- SINCRONIZACIÓN ROBUSTA (Busca en múltiples archivos) ---
        private void PerformFullSync()
        {
            try
            {
                this.Invoke(new Action(() => lblStatus!.Text = "Reading journals..."));

                var directory = new DirectoryInfo(journalFolder);
                var files = directory.GetFiles("Journal.*.log").OrderByDescending(f => f.LastWriteTime).ToList();
                if (files.Count == 0) return;

                currentFile = files[0].FullName;
                string finalSystem = "";

                // PASO 1: Determinar el sistema final (el más reciente en el tiempo)
                foreach (var file in files.Take(3))
                {
                    if (!string.IsNullOrEmpty(finalSystem)) break;
                    var lastLines = ReadLinesSafely(file.FullName);
                    for (int i = lastLines.Count - 1; i >= 0; i--)
                    {
                        try
                        {
                            using (JsonDocument doc = JsonDocument.Parse(lastLines[i]))
                            {
                                if (doc.RootElement.TryGetProperty("event", out JsonElement evt))
                                {
                                    string? e = evt.GetString();
                                    if ((e == "FSDJump" || e == "Location") && doc.RootElement.TryGetProperty("StarSystem", out JsonElement sys))
                                    {
                                        finalSystem = sys.GetString() ?? "";
                                        break;
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }

                currentSystem = finalSystem;

                // PASO 2: Procesar historial (Esto ya lo tenías bien)
                var lines = ReadLinesSafely(currentFile);
                foreach (var line in lines)
                {
                    try
                    {
                        using (JsonDocument doc = JsonDocument.Parse(line))
                        {
                            JsonElement root = doc.RootElement;
                            if (root.TryGetProperty("event", out JsonElement evt))
                            {
                                string? e = evt.GetString();
                                if (e != null)
                                {
                                    foreach (var mod in loadedModules)
                                        mod.HandleHistoryEvent(e, root);
                                }
                            }
                        }
                    }
                    catch { }
                }

                // PASO 3: Actualizar UI y rellenar Current System (CORREGIDO)
                this.Invoke(new Action(() => {
                    UpdateHeader(currentSystem, Color.Orange);

                    if (currentSystemMod != null && currentSystem != "Unknown System")
                    {
                        // A. Decirle al módulo dónde estamos (Location)
                        string jsonLocation = $"{{\"event\":\"Location\", \"StarSystem\":\"{currentSystem}\", \"timestamp\":\"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\"}}";
                        currentSystemMod.HandleJournalEvent("Location", JsonDocument.Parse(jsonLocation).RootElement);

                        // B. [NUEVO] Reprocesar el log actual para encontrar escaneos y llenar la tabla
                        // Hacemos esto en un Task para no congelar la UI si el log es enorme
                        Task.Run(() => {
                            try
                            {
                                foreach (var line in lines) // Reusamos las líneas leídas en el Paso 2
                                {
                                    using (JsonDocument doc = JsonDocument.Parse(line))
                                    {
                                        if (doc.RootElement.TryGetProperty("event", out JsonElement evt))
                                        {
                                            string? e = evt.GetString();
                                            // Si es un evento de escaneo, se lo mandamos al CurrentSystemModule
                                            if (e == "Scan" || e == "FSSDiscoveryScan" || e == "FSSAllBodiesFound" || e == "SAASignalsFound")
                                            {
                                                currentSystemMod.HandleJournalEvent(e!, doc.RootElement);
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                        });
                    }

                    lastFileSize = new FileInfo(currentFile).Length;
                    if (lblStatus != null)
                    {
                        lblStatus.Text = "System Ready.";
                        lblStatus.ForeColor = Color.White;
                    }
                }));
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() => lblStatus!.Text = "Sync Error: " + ex.Message));
            }
        }

        private void ProcessHistoryFile(string path)
        {
            var lines = ReadLinesSafely(path);
            foreach (var line in lines)
            {
                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(line))
                    {
                        JsonElement root = doc.RootElement;
                        if (root.TryGetProperty("event", out JsonElement evt))
                        {
                            string? e = evt.GetString();
                            if (e != null) foreach (var mod in loadedModules) mod.HandleHistoryEvent(e, root);
                        }
                    }
                }
                catch { }
            }
        }

        private List<string> ReadLinesSafely(string filePath)
        {
            var lines = new List<string>();
            try { using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var sr = new StreamReader(fs)) { while (!sr.EndOfStream) { string? l = sr.ReadLine(); if (l != null) lines.Add(l); } } } catch { }
            return lines;
        }

        // Crea un botón con estilo moderno y oscuro
        private Button CreateWebButton(string text, Point location, Color accentColor)
        {
            return new Button
            {
                Text = text,
                Location = location,
                Size = new Size(60, 25),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(20, 20, 25),
                ForeColor = Color.LightGray,
                Font = new Font("Arial", 8, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatAppearance = { BorderSize = 1, BorderColor = accentColor }
            };
        }

        // Abre el navegador con el sistema actual
        private void OpenWebSystem(string platform)
        {
            if (string.IsNullOrEmpty(currentSystem) || currentSystem == "Detecting..." || currentSystem == "Unknown System")
            {
                if (lblStatus != null) lblStatus.Text = "Error: No system detected yet.";
                return;
            }

            // Usamos EscapeDataString para convertir espacios en %20 y manejar caracteres especiales
            string systemName = Uri.EscapeDataString(currentSystem);
            string url = "";

            switch (platform)
            {
                case "EDSM":
                    // Esta es la ruta de búsqueda directa por nombre en EDSM
                    url = $"https://www.edsm.net/en/system/id/38399468/name/{systemName}";
                    break;

                case "Inara":
                    url = $"https://inara.cz/elite/starsystem/?search={systemName}";
                    break;

                case "Spansh":
                    // Spansh usa /system/ seguido del nombre para ir directo a la ficha del sistema
                    url = $"https://www.spansh.co.uk/search/{systemName}";
                    break;
            }

            if (!string.IsNullOrEmpty(url))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                    if (lblStatus != null) lblStatus.Text = $"Opening {platform} for {currentSystem}...";
                }
                catch (Exception ex)
                {
                    if (lblStatus != null) lblStatus.Text = "Error opening browser: " + ex.Message;
                }
            }
        }

    }
}