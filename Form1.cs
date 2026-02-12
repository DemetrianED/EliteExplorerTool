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
        // UI Controls
        private Label lblTitle;
        private Label lblSystem;
        private Label lblStatus;
        private PictureBox btnSettings;

        // CONTAINER CONTROLS
        private DarkTabControl mainTabs;
        private DataGridView gridBodies;

        // System
        private System.Windows.Forms.Timer logTimer;
        private string journalFolder;
        private string currentFile = "";
        private long lastFileSize = 0;
        private SpeechSynthesizer voice;

        // State
        private string currentSystem = "";
        private bool isLoadingHistory = true;

        // Nuevos Controles para Rutas
        private DataGridView gridNavRoute;    // Tabla para NavRoute.json
        private DataGridView gridSpanshRoute; // Tabla para CSV de Spansh
        private Label lblNextJump;           // Label interactivo abajo de EDSM
        private List<EliteLogic.SpanshJump> spanshRouteList = new List<EliteLogic.SpanshJump>();

        public Form1()
        {
            InitializeComponent();
            SetupVoice();
            SetupInterface();
            SetupLogic();
        }

        private void SetupVoice()
        {
            try { voice = new SpeechSynthesizer(); voice.SetOutputToDefaultAudioDevice(); } catch { }
        }

        private void Speak(string message)
        {
            bool voiceEnabled = Properties.Settings.Default.VoiceEnabled;
            if (!isLoadingHistory && voiceEnabled && voice != null) try { voice.SpeakAsync(message); } catch { }
        }

        private void SetupInterface()
        {
            this.Text = "Elite Exploration - Route Master";
            this.Size = new Size(1400, 800); // Un poco más alto
            this.BackColor = Color.FromArgb(10, 10, 15);

            // --- HEADER ---
            btnSettings = new PictureBox();
            btnSettings.Image = Properties.Resources.iconSettings;
            btnSettings.SizeMode = PictureBoxSizeMode.Zoom;
            btnSettings.Size = new Size(32, 32);
            btnSettings.Location = new Point(this.ClientSize.Width - 60, 20);
            btnSettings.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSettings.Cursor = Cursors.Hand;
            btnSettings.Click += BtnSettings_Click;
            this.Controls.Add(btnSettings);

            lblTitle = new Label { Text = "CURRENT SYSTEM:", Location = new Point(20, 20), AutoSize = true, Font = new Font("Arial", 10, FontStyle.Bold), ForeColor = Color.Gray };
            this.Controls.Add(lblTitle);

            lblSystem = new Label { Text = "Detecting...", Location = new Point(20, 45), AutoSize = true, Font = new Font("Arial", 22, FontStyle.Bold), ForeColor = Color.Orange };
            this.Controls.Add(lblSystem);

            lblStatus = new Label { Text = "Waiting for data...", Location = new Point(20, 95), AutoSize = true, Font = new Font("Arial", 12, FontStyle.Italic), ForeColor = Color.LightGray };
            this.Controls.Add(lblStatus);

            // --- LABEL SIGUIENTE SALTO (SPANSH) ---
            lblNextJump = new Label();
            lblNextJump.Text = "Next Jump: ---";
            lblNextJump.Location = new Point(20, 120);
            lblNextJump.AutoSize = true;
            lblNextJump.Font = new Font("Arial", 11, FontStyle.Bold);
            lblNextJump.ForeColor = Color.Cyan;
            lblNextJump.Cursor = Cursors.Hand;
            lblNextJump.Click += (s, e) => {
                if (lblNextJump.Text.Contains(": "))
                {
                    string sys = lblNextJump.Text.Split(new[] { ": " }, StringSplitOptions.None)[1];
                    if (sys != "---")
                    {
                        Clipboard.SetText(sys);
                        lblStatus.Text = "Copied to clipboard: " + sys;
                    }
                }
            };
            this.Controls.Add(lblNextJump);

            // --- TABS ---
            mainTabs = new DarkTabControl();
            mainTabs.Location = new Point(20, 155); // Bajamos un poco los tabs
            mainTabs.Size = new Size(1340, 560);
            mainTabs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            mainTabs.Padding = new Point(20, 6);

            // Pestañas
            TabPage tabCurrent = new TabPage("Current System") { BackColor = Color.FromArgb(15, 15, 20) };
            TabPage tabRoute = new TabPage("Game Route") { BackColor = Color.FromArgb(15, 15, 20) };
            TabPage tabSpansh = new TabPage("Spansh Route") { BackColor = Color.FromArgb(15, 15, 20) };
            TabPage tabHistory = new TabPage("History") { BackColor = Color.FromArgb(15, 15, 20) };

            mainTabs.TabPages.Add(tabCurrent);
            mainTabs.TabPages.Add(tabRoute);
            mainTabs.TabPages.Add(tabSpansh);
            mainTabs.TabPages.Add(tabHistory);
            this.Controls.Add(mainTabs);

            // --- GRID 1: CURRENT SYSTEM ---
            gridBodies = CreateModernGrid();
            gridBodies.CellPainting += GridBodies_CellPainting;
            gridBodies.CellToolTipTextNeeded += GridBodies_CellToolTipTextNeeded;
            SetupCurrentSystemColumns();
            tabCurrent.Controls.Add(gridBodies);

            // --- GRID 2: GAME ROUTE ---
            gridNavRoute = CreateModernGrid();
            gridNavRoute.Columns.Add("System", "System Name");
            gridNavRoute.Columns.Add("StarClass", "Star Class");
            gridNavRoute.Columns.Add("Scoopable", "Scoopable");
            tabRoute.Controls.Add(gridNavRoute);

            // --- GRID 3: SPANSH ROUTE ---
            gridSpanshRoute = CreateModernGrid();
            gridSpanshRoute.Columns.Add("Check", "Done"); // Columna para el checkbox
            gridSpanshRoute.Columns.Add("System", "System Name");
            gridSpanshRoute.Columns.Add("Jumps", "Jumps");
            gridSpanshRoute.Columns.Add("Distance", "Distance (Ly)");
            tabSpansh.Controls.Add(gridSpanshRoute);

            ApplyColumnSettings();
        }

        // Función Helper para no repetir código de estilo de tablas
        private DataGridView CreateModernGrid()
        {
            var g = new DataGridView();
            g.Dock = DockStyle.Fill;
            g.BackgroundColor = Color.Black;
            g.BorderStyle = BorderStyle.None;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.GridColor = Color.FromArgb(20, 20, 20);
            g.AllowUserToAddRows = false;
            g.ReadOnly = true;
            g.RowHeadersVisible = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 30, 30);
            g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            g.ColumnHeadersHeight = 40;
            g.DefaultCellStyle.BackColor = Color.FromArgb(20, 20, 20);
            g.DefaultCellStyle.ForeColor = Color.White;
            g.DefaultCellStyle.Font = new Font("Consolas", 9);
            g.RowTemplate.Height = 35;
            return g;
        }

        private void SetupCurrentSystemColumns()
        {
            gridBodies.Columns.Add("Name", "Name");
            gridBodies.Columns.Add("Type", "Type");
            gridBodies.Columns.Add("Atmosphere", "Atmosphere");
            gridBodies.Columns.Add("Temperature", "Temp");
            var colScan = new DataGridViewImageColumn { Name = "SurfaceScan", HeaderText = "", ToolTipText = "Scan Status", ImageLayout = DataGridViewImageCellLayout.Normal };
            gridBodies.Columns.Add(colScan);
            gridBodies.Columns.Add("Value", "Value");
            gridBodies.Columns.Add(new DataGridViewImageColumn { Name = "Valuable", HeaderText = "", ToolTipText = "High Value" });
            gridBodies.Columns.Add(new DataGridViewImageColumn { Name = "Terra", HeaderText = "", ToolTipText = "Terraformable" });
            gridBodies.Columns.Add(new DataGridViewImageColumn { Name = "Geo", HeaderText = "", ToolTipText = "Geological" });
            gridBodies.Columns.Add(new DataGridViewImageColumn { Name = "Bio", HeaderText = "", ToolTipText = "Biological" });
            gridBodies.Columns.Add(new DataGridViewImageColumn { Name = "Landable", HeaderText = "", ToolTipText = "Landable" });
            gridBodies.Columns.Add("Gravity", "Gravity");
            gridBodies.Columns.Add(new DataGridViewImageColumn { Name = "Materials", HeaderText = "", ToolTipText = "Jumponium" });
            gridBodies.Columns.Add(new DataGridViewImageColumn { Name = "FirstDiscovery", HeaderText = "", ToolTipText = "First Discovery" });
            gridBodies.Columns.Add("EDSM", "EDSM");
            gridBodies.Columns.Add("Distance", "Distance");
            gridBodies.Columns.Add("FullName", "ID"); gridBodies.Columns["FullName"].Visible = false;

            // Ajuste pesos
            gridBodies.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            foreach (DataGridViewColumn c in gridBodies.Columns) if (c is DataGridViewImageColumn) c.FillWeight = 35;
        }
        private void BtnSettings_Click(object sender, EventArgs e)
        {
            string oldPath = Properties.Settings.Default.SpanshCsvPath;
            SettingsForm settingsWindow = new SettingsForm();

            if (settingsWindow.ShowDialog() == DialogResult.OK)
            {
                SetupVoice();
                ApplyColumnSettings();

                // Si la ruta del CSV cambió o se borró, recargamos la pestaña Spansh
                string newPath = Properties.Settings.Default.SpanshCsvPath;
                if (oldPath != newPath)
                {
                    if (string.IsNullOrEmpty(newPath))
                    {
                        spanshRouteList.Clear();
                        gridSpanshRoute.Rows.Clear();
                        lblNextJump.Text = "Next Jump: ---";
                    }
                    else
                    {
                        LoadSpanshRoute(newPath);
                    }
                }
            }
        }

        // --- NUEVA FUNCION DE COLUMNAS ---
        private void ApplyColumnSettings()
        {
            if (gridBodies == null || gridBodies.Columns.Count == 0) return;

            string hiddenString = Properties.Settings.Default.HiddenColumns ?? "";
            List<string> hiddenList = hiddenString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            foreach (DataGridViewColumn col in gridBodies.Columns)
            {
                if (col.Name == "FullName") continue;
                if (col.Name == "Name") { col.Visible = true; continue; }
                col.Visible = !hiddenList.Contains(col.Name);
            }
        }

        private void GridBodies_CellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                if (gridBodies.Columns[e.ColumnIndex].Name == "Materials")
                {
                    if (string.IsNullOrEmpty(e.ToolTipText)) e.ToolTipText = gridBodies.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText;
                }
            }
        }

        private void GridBodies_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex == -1 && e.ColumnIndex >= 0)
            {
                e.PaintBackground(e.CellBounds, true);
                Image imgHeader = null;
                string colName = gridBodies.Columns[e.ColumnIndex].Name;

                if (colName == "SurfaceScan") imgHeader = Properties.Resources.iconSurface;
                if (colName == "Valuable") imgHeader = Properties.Resources.iconMoney;
                if (colName == "Terra") imgHeader = Properties.Resources.iconTerraformable;
                if (colName == "Geo") imgHeader = Properties.Resources.iconGeo;
                if (colName == "Bio") imgHeader = Properties.Resources.iconBio;
                if (colName == "Landable") imgHeader = Properties.Resources.iconLandable;
                if (colName == "Materials") imgHeader = Properties.Resources.iconMaterials;
                if (colName == "FirstDiscovery") imgHeader = Properties.Resources.iconDiscovery;

                if (imgHeader != null)
                {
                    int iconSize = 24;
                    int x = e.CellBounds.Left + (e.CellBounds.Width - iconSize) / 2;
                    int y = e.CellBounds.Top + (e.CellBounds.Height - iconSize) / 2;
                    e.Graphics.DrawImage(imgHeader, new Rectangle(x, y, iconSize, iconSize));
                    e.Handled = true;
                }
                else e.PaintContent(e.CellBounds);
            }
        }

        // --- CORE LOGIC ---
        private void SetupLogic()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] possiblePaths = new string[]
            {
                Path.Combine(userProfile, "Saved Games", "Frontier Developments", "Elite Dangerous"),
                Path.Combine(userProfile, "OneDrive", "Saved Games", "Frontier Developments", "Elite Dangerous"),
                Path.Combine(userProfile, "Juegos guardados", "Frontier Developments", "Elite Dangerous")
            };

            journalFolder = "";
            foreach (string path in possiblePaths)
            {
                if (Directory.Exists(path)) { journalFolder = path; break; }
            }

            if (string.IsNullOrEmpty(journalFolder))
            {
                DialogResult result = MessageBox.Show(
                    "Could not find Elite Dangerous Journal folder automatically.\n\n" +
                    "Expected at: " + Path.Combine(userProfile, "Saved Games", "Frontier Developments", "Elite Dangerous") + "\n\n" +
                    "Do you want to select the folder manually?",
                    "Journal Folder Not Found", MessageBoxButtons.YesNo, MessageBoxIcon.Error);

                if (result == DialogResult.Yes)
                {
                    using (var fbd = new FolderBrowserDialog())
                    {
                        fbd.Description = "Select your Elite Dangerous Journal folder";
                        if (fbd.ShowDialog() == DialogResult.OK) journalFolder = fbd.SelectedPath;
                        else { lblStatus.Text = "Error: No folder selected."; return; }
                    }
                }
                else { lblStatus.Text = "Error: Journal folder missing."; return; }
            }

            lblStatus.Text = "Folder found: " + journalFolder;

            // --- CARGA INICIAL DE SPANSH ---
            if (!string.IsNullOrEmpty(Properties.Settings.Default.SpanshCsvPath))
            {
                LoadSpanshRoute(Properties.Settings.Default.SpanshCsvPath);
            }

            Task.Run(() => PerformFullSync());

            logTimer = new System.Windows.Forms.Timer();
            logTimer.Interval = 1000;
            logTimer.Tick += MonitorLive;
            logTimer.Start();
        }

        private void LoadSpanshRoute(string filePath)
        {
            if (!File.Exists(filePath)) return;

            try
            {
                spanshRouteList.Clear();
                gridSpanshRoute.Rows.Clear();

                var lines = File.ReadAllLines(filePath);
                if (lines.Length < 2) return; // Vacío o solo encabezado

                // Spansh CSV suele ser: System Name, Distance, Jumps, etc.
                // Buscamos los índices de las columnas por nombre
                var headers = lines[0].Split(',').Select(h => h.Trim('"')).ToList();
                int idxName = headers.FindIndex(h => h.Contains("System Name"));
                int idxJumps = headers.FindIndex(h => h.Contains("Jumps"));
                int idxDist = headers.FindIndex(h => h.Contains("Distance"));

                for (int i = 1; i < lines.Length; i++)
                {
                    var cols = lines[i].Split(',').Select(c => c.Trim('"')).ToArray();
                    if (cols.Length <= idxName) continue;

                    string sysName = cols[idxName];
                    string jumps = idxJumps != -1 ? cols[idxJumps] : "1";
                    string dist = idxDist != -1 ? cols[idxDist] : "0";

                    spanshRouteList.Add(new EliteLogic.SpanshJump
                    {
                        SystemName = sysName,
                        IsDone = false // Por defecto, se marcará con el Journal
                    });

                    gridSpanshRoute.Rows.Add("☐", sysName, jumps, dist);
                }

                UpdateSpanshUI();
                lblStatus.Text = "Spansh Route loaded: " + spanshRouteList.Count + " waypoints.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error loading Spansh CSV: " + ex.Message;
            }
        }

        private void UpdateSpanshUI()
        {
            // Busca el primer sistema no visitado para el Label "Next Jump"
            var next = spanshRouteList.FirstOrDefault(j => !j.IsDone);
            if (next != null)
            {
                lblNextJump.Text = "Next Jump: " + next.SystemName;
            }
            else
            {
                lblNextJump.Text = "Next Jump: ---";
            }
        }

        private void PerformFullSync()
        {
            isLoadingHistory = true;
            this.Invoke(new Action(() => lblStatus.Text = "Reading journals..."));

            var directory = new DirectoryInfo(journalFolder);
            var files = directory.GetFiles("Journal.*.log").OrderByDescending(f => f.LastWriteTime).ToList();

            if (files.Count == 0)
            {
                this.Invoke(new Action(() => lblStatus.Text = "Error: No Journal logs found inside folder."));
                return;
            }

            currentFile = files[0].FullName;

            // PASO A: Encontrar el sistema actual
            foreach (var file in files)
            {
                if (!string.IsNullOrEmpty(currentSystem)) break;
                var lines = ReadLinesSafely(file.FullName);
                for (int i = lines.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(lines[i])) continue;
                        using (JsonDocument doc = JsonDocument.Parse(lines[i]))
                        {
                            if (doc.RootElement.TryGetProperty("event", out JsonElement evt))
                            {
                                string e = evt.GetString();
                                if ((e == "FSDJump" || e == "Location") && doc.RootElement.TryGetProperty("StarSystem", out JsonElement sys))
                                {
                                    currentSystem = sys.GetString();
                                    break;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            if (string.IsNullOrEmpty(currentSystem))
            {
                this.Invoke(new Action(() => lblStatus.Text = "Unknown location. Please jump or relog."));
                isLoadingHistory = false;
                return;
            }

            this.Invoke(new Action(() => {
                lblSystem.Text = currentSystem;
                lblStatus.Text = "Processing scan data...";
            }));

            // PASO B: Cargar Datos
            Dictionary<string, JsonElement> localBodies = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            bool arrivalFound = false;

            foreach (var file in files)
            {
                if (arrivalFound) break;
                var lines = ReadLinesSafely(file.FullName);
                for (int i = lines.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(lines[i])) continue;
                        using (JsonDocument doc = JsonDocument.Parse(lines[i]))
                        {
                            JsonElement root = doc.RootElement.Clone();
                            if (!root.TryGetProperty("event", out JsonElement evt)) continue;
                            string e = evt.GetString();

                            if (e == "FSDJump" && root.GetProperty("StarSystem").GetString() == currentSystem)
                            {
                                arrivalFound = true;
                                break;
                            }

                            if (e == "Scan")
                            {
                                string scanSystem = root.TryGetProperty("StarSystem", out JsonElement ss) ? ss.GetString() : currentSystem;
                                if (scanSystem == currentSystem)
                                {
                                    string bodyName = root.GetProperty("BodyName").GetString();
                                    if (!localBodies.ContainsKey(bodyName)) localBodies.Add(bodyName, root);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            var sortedBodies = localBodies.Values.OrderBy(b => b.GetProperty("BodyName").GetString()).ToList();

            this.Invoke(new Action(() => {
                gridBodies.Rows.Clear();
                foreach (var body in sortedBodies) AddRowFromJournal(body);

                lastFileSize = new FileInfo(currentFile).Length;
                isLoadingHistory = false;
                lblStatus.Text = $"Ready. {sortedBodies.Count} bodies loaded.";
                lblStatus.ForeColor = Color.White;

                ApplyColumnSettings(); // RE-APLICAR POR SEGURIDAD
                FetchEdsmData(currentSystem);

                // CARGAMOS SPANSH AQUÍ, CUANDO TODO LO DEMÁS YA ESTÁ LISTO
                if (!string.IsNullOrEmpty(Properties.Settings.Default.SpanshCsvPath))
                {
                    LoadSpanshRoute(Properties.Settings.Default.SpanshCsvPath);
                }

                FetchEdsmData(currentSystem);
            }));
        }
        private void MonitorLive(object sender, EventArgs e)
        {
            try
            {
                // 1. Chequear la ruta del juego (NavRoute.json)
                CheckNavRoute();

                // 2. Chequear actualizaciones en el Journal
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
                            string line = sr.ReadLine();
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
                    string e = evt.GetString();

                    if (e == "FSDJump")
                    {
                        currentSystem = root.GetProperty("StarSystem").GetString();
                        gridBodies.Rows.Clear();
                        lblSystem.Text = currentSystem;
                        Speak($"Arrived at {currentSystem}");
                        if (mainTabs.SelectedIndex != 0) mainTabs.SelectedIndex = 0;

                        // --- NUEVA LÓGICA SPANSH ---
                        for (int i = 0; i < spanshRouteList.Count; i++)
                        {
                            if (string.Equals(spanshRouteList[i].SystemName, currentSystem, StringComparison.OrdinalIgnoreCase))
                            {
                                spanshRouteList[i].IsDone = true;
                                gridSpanshRoute.Rows[i].Cells["Check"].Value = "✔";
                                gridSpanshRoute.Rows[i].DefaultCellStyle.ForeColor = Color.Gray;
                                // Guardar progreso (opcional)
                                Properties.Settings.Default.SpanshProgress = i;
                                Properties.Settings.Default.Save();
                            }
                        }
                        UpdateSpanshUI();
                        // ---------------------------

                        FetchEdsmData(currentSystem);
                    }
                    else if (e == "Scan")
                    {
                        AddRowFromJournal(root);
                    }
                    else if (e == "StartJump" && root.GetProperty("JumpType").GetString() == "Hyperspace")
                    {
                        string dest = root.GetProperty("StarSystem").GetString();
                        lblTitle.Text = "JUMPING TO:"; lblTitle.ForeColor = Color.Yellow;
                        lblSystem.Text = dest; lblSystem.ForeColor = Color.Yellow;
                        Speak($"Charging Frame Shift Drive.");
                    }
                }
            }
            catch { }
        }

        // --- ADDERS & EDSM LOGIC ---

        private void AddRowFromJournal(JsonElement root)
        {
            if (root.TryGetProperty("PlanetClass", out JsonElement pc) && pc.GetString() == "Belt Cluster") return;
            string fullName = root.GetProperty("BodyName").GetString();
            if (fullName.Contains("Belt Cluster")) return;

            foreach (DataGridViewRow r in gridBodies.Rows)
            {
                if (NamesMatch(r.Cells["FullName"].Value.ToString(), fullName))
                {
                    UpdateRowFromJournal(r, root);
                    return;
                }
            }

            bool isStar = false; string typeCode = "", typeDescription = "", rawType = "";
            if (root.TryGetProperty("StarType", out JsonElement st)) { isStar = true; typeCode = st.GetString(); typeDescription = EliteLogic.GetStarDescription(typeCode); }
            else if (root.TryGetProperty("PlanetClass", out JsonElement pc2)) { rawType = pc2.GetString(); typeDescription = rawType; }

            string shortName = CalculateShortName(fullName, isStar);
            double dist = root.TryGetProperty("DistanceFromArrivalLS", out JsonElement d) ? d.GetDouble() : 0;
            string distStr = Math.Round(dist, 0) + " Ls";

            List<string> materials = new List<string>();
            if (root.TryGetProperty("Materials", out JsonElement mats) && mats.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in mats.EnumerateArray()) if (m.TryGetProperty("Name", out JsonElement mn)) materials.Add(mn.GetString());
            }

            bool hasGeo = false, hasBio = false;
            if (root.TryGetProperty("Signals", out JsonElement sigs) && sigs.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in sigs.EnumerateArray())
                {
                    if (s.TryGetProperty("Type", out JsonElement t2))
                    {
                        if (t2.GetString().Contains("Geological")) hasGeo = true;
                        if (t2.GetString().Contains("Biological")) hasBio = true;
                    }
                }
            }
            if (root.TryGetProperty("Genuses", out JsonElement gen) && gen.ValueKind == JsonValueKind.Array && gen.GetArrayLength() > 0) hasBio = true;

            string tempStr = root.TryGetProperty("SurfaceTemperature", out JsonElement t) ? Math.Round(t.GetDouble(), 0) + " K" : "-";
            bool isTerra = root.TryGetProperty("TerraformState", out JsonElement ts) && ts.GetString() == "Terraformable";
            long val = EliteLogic.CalculateValue(isStar ? typeCode : rawType, isTerra, isStar);
            string valStr = val.ToString("N0") + " Cr";
            bool isValuable = val > 500000;
            double grav = root.TryGetProperty("SurfaceGravity", out JsonElement g) ? g.GetDouble() / 9.81 : 0;
            bool isLand = root.TryGetProperty("Landable", out JsonElement l) ? l.GetBoolean() : false;
            bool isDisc = !root.TryGetProperty("WasDiscovered", out JsonElement wd) || !wd.GetBoolean();

            int idx = gridBodies.Rows.Add(
                shortName, typeDescription,
                root.TryGetProperty("Atmosphere", out JsonElement at) ? at.GetString() : "-",
                tempStr,
                ResizeImg(Properties.Resources.iconSurface),
                valStr,
                isValuable ? ResizeImg(Properties.Resources.iconMoney) : GetEmptyImg(),
                isTerra ? ResizeImg(Properties.Resources.iconTerraformable) : GetEmptyImg(),
                hasGeo ? ResizeImg(Properties.Resources.iconGeo) : GetEmptyImg(),
                hasBio ? ResizeImg(Properties.Resources.iconBio) : GetEmptyImg(),
                isLand ? ResizeImg(Properties.Resources.iconLandable) : GetEmptyImg(),
                Math.Round(grav, 2) + " G",
                GetJumpIcon(EliteLogic.GetJumponiumLevel(materials)),
                isDisc ? ResizeImg(Properties.Resources.iconDiscovery) : GetEmptyImg(),
                "N/A", distStr, fullName
            );

            gridBodies.Rows[idx].Cells["Materials"].ToolTipText = EliteLogic.GetMaterialTooltip(materials);

            if (isValuable)
            {
                gridBodies.Rows[idx].DefaultCellStyle.BackColor = Color.FromArgb(0, 50, 0);
                gridBodies.Rows[idx].DefaultCellStyle.ForeColor = Color.White;
                if (!isLoadingHistory && val > 1000000) Speak("High value body detected.");
            }
        }

        private async void FetchEdsmData(string systemName)
        {
            if (string.IsNullOrEmpty(systemName)) return;
            string json = await EdsmService.GetBodies(systemName);
            if (string.IsNullOrEmpty(json)) { lblStatus.Text = "EDSM: No Data"; return; }

            try
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;
                    if (root.TryGetProperty("bodies", out JsonElement bodies) && bodies.ValueKind == JsonValueKind.Array)
                    {
                        int count = 0;
                        foreach (var b in bodies.EnumerateArray()) { ProcessEdsmBody(b); count++; }
                        lblStatus.Text = $"EDSM: {count} bodies loaded.";
                    }
                }
            }
            catch { }
        }

        private void ProcessEdsmBody(JsonElement body)
        {
            string edsmName = body.GetProperty("name").GetString();

            foreach (DataGridViewRow r in gridBodies.Rows)
            {
                if (NamesMatch(r.Cells["FullName"].Value.ToString(), edsmName))
                {
                    UpdateRowFromEdsm(r, body);
                    return;
                }
            }

            string shortName = CalculateShortName(edsmName, false);
            string type = "Unknown";
            if (body.TryGetProperty("type", out JsonElement ty)) type = ty.GetString();
            if (body.TryGetProperty("subType", out JsonElement st)) type = st.GetString();

            double dist = body.TryGetProperty("distanceToArrival", out JsonElement d) ? d.GetDouble() : 0;
            long val = body.TryGetProperty("estimatedValue", out JsonElement ev) ? ev.GetInt64() : 0;
            string discoverer = "Unknown";
            if (body.TryGetProperty("discovery", out JsonElement disc) && disc.ValueKind == JsonValueKind.Object)
                if (disc.TryGetProperty("commander", out JsonElement cmdr)) discoverer = cmdr.GetString();

            bool terra = body.TryGetProperty("terraformingState", out JsonElement tfs) && tfs.GetString() == "Terraformable";

            List<string> materials = new List<string>();
            if (body.TryGetProperty("materials", out JsonElement mats) && mats.ValueKind == JsonValueKind.Object)
                foreach (var p in mats.EnumerateObject()) materials.Add(p.Name);

            int idx = gridBodies.Rows.Add(
                shortName, type, "-", "-", GetEmptyImg(),
                val > 0 ? val.ToString("N0") + " Cr" : "-",
                (val > 500000) ? ResizeImg(Properties.Resources.iconMoney) : GetEmptyImg(),
                terra ? ResizeImg(Properties.Resources.iconTerraformable) : GetEmptyImg(),
                GetEmptyImg(), GetEmptyImg(), GetEmptyImg(), "-",
                GetJumpIcon(EliteLogic.GetJumponiumLevel(materials)),
                GetEmptyImg(), discoverer, Math.Round(dist, 0) + " Ls", edsmName
            );

            gridBodies.Rows[idx].DefaultCellStyle.ForeColor = Color.LightSteelBlue;
            gridBodies.Rows[idx].Cells["Materials"].ToolTipText = EliteLogic.GetMaterialTooltip(materials);
            if (val > 500000)
            {
                gridBodies.Rows[idx].DefaultCellStyle.BackColor = Color.FromArgb(0, 50, 0);
                gridBodies.Rows[idx].DefaultCellStyle.ForeColor = Color.White;
            }
        }

        private void UpdateRowFromJournal(DataGridViewRow row, JsonElement root)
        {
            row.Cells["SurfaceScan"].Value = ResizeImg(Properties.Resources.iconSurface);

            bool isStar = false; string typeCode = "", typeDesc = "", rawType = "";
            if (root.TryGetProperty("StarType", out JsonElement st)) { isStar = true; typeCode = st.GetString(); typeDesc = EliteLogic.GetStarDescription(typeCode); }
            else if (root.TryGetProperty("PlanetClass", out JsonElement pc2)) { rawType = pc2.GetString(); typeDesc = rawType; }

            row.Cells["Type"].Value = typeDesc;

            string atm = root.TryGetProperty("Atmosphere", out JsonElement at) ? at.GetString() : "-";
            row.Cells["Atmosphere"].Value = string.IsNullOrEmpty(atm) ? "-" : atm;

            double tk = root.TryGetProperty("SurfaceTemperature", out JsonElement t) ? t.GetDouble() : 0;
            row.Cells["Temperature"].Value = Math.Round(tk, 0) + " K";

            bool terra = root.TryGetProperty("TerraformState", out JsonElement ts) && ts.GetString() == "Terraformable";
            long val = EliteLogic.CalculateValue(isStar ? typeCode : rawType, terra, isStar);
            row.Cells["Value"].Value = val.ToString("N0") + " Cr";

            row.Cells["Valuable"].Value = (val > 500000) ? ResizeImg(Properties.Resources.iconMoney) : GetEmptyImg();
            row.Cells["Terra"].Value = terra ? ResizeImg(Properties.Resources.iconTerraformable) : GetEmptyImg();

            bool hasGeo = false, hasBio = false;
            if (root.TryGetProperty("Signals", out JsonElement sigs) && sigs.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in sigs.EnumerateArray())
                {
                    if (s.TryGetProperty("Type", out JsonElement t2))
                    {
                        if (t2.GetString().Contains("Geological")) hasGeo = true;
                        if (t2.GetString().Contains("Biological")) hasBio = true;
                    }
                }
            }
            if (root.TryGetProperty("Genuses", out JsonElement gen) && gen.ValueKind == JsonValueKind.Array && gen.GetArrayLength() > 0) hasBio = true;
            row.Cells["Geo"].Value = hasGeo ? ResizeImg(Properties.Resources.iconGeo) : GetEmptyImg();
            row.Cells["Bio"].Value = hasBio ? ResizeImg(Properties.Resources.iconBio) : GetEmptyImg();

            row.DefaultCellStyle.ForeColor = Color.White;
            if (val > 500000)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(0, 50, 0);
                if (!isLoadingHistory && val > 1000000) Speak("High value body confirmed.");
            }
            else
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(20, 20, 20);
            }
        }

        private void UpdateRowFromEdsm(DataGridViewRow row, JsonElement body)
        {
            if (body.TryGetProperty("discovery", out JsonElement disc) && disc.ValueKind == JsonValueKind.Object)
                if (disc.TryGetProperty("commander", out JsonElement cmdr)) row.Cells["EDSM"].Value = cmdr.GetString();

            if (body.TryGetProperty("estimatedValue", out JsonElement ev))
            {
                long edsmVal = ev.GetInt64();
                string cur = row.Cells["Value"].Value.ToString();
                if (edsmVal > 0 && (cur == "-" || cur.StartsWith("0")))
                {
                    row.Cells["Value"].Value = edsmVal.ToString("N0") + " Cr";
                    if (edsmVal > 500000)
                    {
                        row.Cells["Valuable"].Value = ResizeImg(Properties.Resources.iconMoney);
                        row.DefaultCellStyle.BackColor = Color.FromArgb(0, 50, 0);
                        row.DefaultCellStyle.ForeColor = Color.White;
                    }
                }
            }
        }

        private long lastNavRouteTime = 0;

        private void CheckNavRoute()
        {
            string navPath = Path.Combine(journalFolder, "NavRoute.json");
            if (!File.Exists(navPath)) return;

            try
            {
                var info = new FileInfo(navPath);
                // Solo leemos si el archivo cambió (basado en la hora de última escritura)
                long currentWriteTime = info.LastWriteTime.Ticks;
                if (currentWriteTime <= lastNavRouteTime) return;
                lastNavRouteTime = currentWriteTime;

                // Leemos usando el método seguro que creamos antes para evitar bloqueos
                var lines = ReadLinesSafely(navPath);
                if (lines.Count == 0) return;

                string fullJson = string.Join("", lines);
                using (JsonDocument doc = JsonDocument.Parse(fullJson))
                {
                    if (doc.RootElement.TryGetProperty("Route", out JsonElement routeArray))
                    {
                        gridNavRoute.Rows.Clear();
                        foreach (var item in routeArray.EnumerateArray())
                        {
                            string sys = item.GetProperty("StarSystem").GetString();
                            string starClass = item.TryGetProperty("StarClass", out JsonElement sc) ? sc.GetString() : "?";
                            bool scoop = EliteLogic.IsScoopable(starClass);

                            int idx = gridNavRoute.Rows.Add(sys, starClass, scoop ? "✔ YES" : "✘ NO");
                            if (scoop) gridNavRoute.Rows[idx].Cells["Scoopable"].Style.ForeColor = Color.LightGreen;
                            else gridNavRoute.Rows[idx].Cells["Scoopable"].Style.ForeColor = Color.Salmon;
                        }
                    }
                }
            }
            catch { }
        }

        private bool NamesMatch(string n1, string n2)
        {
            if (string.Equals(n1, n2, StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(currentSystem))
            {
                string s = currentSystem.ToUpper().Trim();
                string a = n1.ToUpper().Trim();
                string b = n2.ToUpper().Trim();
                if (a == s && b == s + " A") return true;
                if (b == s && a == s + " A") return true;
            }
            return false;
        }

        private string CalculateShortName(string fullName, bool isStar)
        {
            string shortName = fullName;
            if (!string.IsNullOrEmpty(currentSystem) && fullName.StartsWith(currentSystem, StringComparison.OrdinalIgnoreCase))
                shortName = fullName.Substring(currentSystem.Length).Trim();
            if (string.IsNullOrEmpty(shortName) || (isStar && shortName.Equals("A", StringComparison.OrdinalIgnoreCase))) shortName = "Primary";
            return shortName;
        }

        private Image ResizeImg(Image img) { if (img == null) return GetEmptyImg(); return new Bitmap(img, new Size(20, 20)); }
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
            catch (Exception) { }
            return lines;
        }
        private Image GetEmptyImg() { Bitmap bmp = new Bitmap(20, 20); using (Graphics g = Graphics.FromImage(bmp)) { g.Clear(Color.Transparent); } return bmp; }
        private Image GetJumpIcon(EliteLogic.JumpLevel level)
        {
            switch (level)
            {
                case EliteLogic.JumpLevel.Basic: return ResizeImg(Properties.Resources.iconBasic);
                case EliteLogic.JumpLevel.Standard: return ResizeImg(Properties.Resources.iconStandard);
                case EliteLogic.JumpLevel.Premium: return ResizeImg(Properties.Resources.iconPremium);
                default: return GetEmptyImg();
            }
        }
    }
}