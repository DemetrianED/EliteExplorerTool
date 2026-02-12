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
            this.Text = "Elite Explorer Tool - Final Stable";
            this.Size = new Size(1400, 760);
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

            lblTitle = new Label();
            lblTitle.Text = "CURRENT SYSTEM:";
            lblTitle.Location = new Point(20, 20);
            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Arial", 10, FontStyle.Bold);
            lblTitle.ForeColor = Color.Gray;
            this.Controls.Add(lblTitle);

            lblSystem = new Label();
            lblSystem.Text = "Detecting...";
            lblSystem.Location = new Point(20, 45);
            lblSystem.AutoSize = true;
            lblSystem.Font = new Font("Arial", 22, FontStyle.Bold);
            lblSystem.ForeColor = Color.Orange;
            this.Controls.Add(lblSystem);

            lblStatus = new Label();
            lblStatus.Text = "Waiting for data...";
            lblStatus.Location = new Point(20, 95);
            lblStatus.AutoSize = true;
            lblStatus.Font = new Font("Arial", 12, FontStyle.Italic);
            lblStatus.ForeColor = Color.LightGray;
            this.Controls.Add(lblStatus);

            // --- TABS ---
            mainTabs = new DarkTabControl();
            mainTabs.Location = new Point(20, 140);
            mainTabs.Size = new Size(1340, 560);
            mainTabs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            mainTabs.Padding = new Point(20, 6);

            TabPage tabCurrent = new TabPage("Current System");
            tabCurrent.BackColor = Color.FromArgb(15, 15, 20);
            mainTabs.TabPages.Add(tabCurrent);
            mainTabs.TabPages.Add(new TabPage("Route") { BackColor = Color.FromArgb(15, 15, 20) });
            mainTabs.TabPages.Add(new TabPage("History") { BackColor = Color.FromArgb(15, 15, 20) });

            this.Controls.Add(mainTabs);

            // --- GRID ---
            gridBodies = new DataGridView();
            gridBodies.Dock = DockStyle.Fill;
            gridBodies.BackgroundColor = Color.Black;
            gridBodies.BorderStyle = BorderStyle.None;
            gridBodies.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            gridBodies.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            gridBodies.GridColor = Color.FromArgb(20, 20, 20);
            gridBodies.AllowUserToAddRows = false;
            gridBodies.ReadOnly = true;
            gridBodies.RowHeadersVisible = false;
            gridBodies.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridBodies.ShowCellToolTips = true;

            gridBodies.EnableHeadersVisualStyles = false;
            gridBodies.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 30, 30);
            gridBodies.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            gridBodies.ColumnHeadersDefaultCellStyle.Font = new Font("Arial", 9, FontStyle.Bold);
            gridBodies.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(30, 30, 30);

            gridBodies.DefaultCellStyle.BackColor = Color.FromArgb(20, 20, 20);
            gridBodies.DefaultCellStyle.ForeColor = Color.White;
            gridBodies.DefaultCellStyle.SelectionBackColor = Color.FromArgb(50, 50, 50);
            gridBodies.DefaultCellStyle.SelectionForeColor = Color.White;
            gridBodies.DefaultCellStyle.Font = new Font("Consolas", 9);
            gridBodies.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

            gridBodies.RowTemplate.Height = 40;
            gridBodies.ColumnHeadersHeight = 40;

            gridBodies.CellPainting += GridBodies_CellPainting;
            gridBodies.CellToolTipTextNeeded += GridBodies_CellToolTipTextNeeded;

            // --- COLUMNAS ---
            gridBodies.Columns.Add("Name", "Name");
            gridBodies.Columns.Add("Type", "Type");
            gridBodies.Columns.Add("Atmosphere", "Atmosphere");
            gridBodies.Columns.Add("Temperature", "Temp");

            var colScan = new DataGridViewImageColumn(); colScan.Name = "SurfaceScan"; colScan.HeaderText = ""; colScan.ToolTipText = "Scan Status"; colScan.ImageLayout = DataGridViewImageCellLayout.Normal; colScan.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter; gridBodies.Columns.Add(colScan);
            gridBodies.Columns.Add("Value", "Value");
            var colVal = new DataGridViewImageColumn(); colVal.Name = "Valuable"; colVal.HeaderText = ""; colVal.ToolTipText = "High Value"; colVal.ImageLayout = DataGridViewImageCellLayout.Normal; colVal.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter; gridBodies.Columns.Add(colVal);
            var colTerra = new DataGridViewImageColumn(); colTerra.Name = "Terra"; colTerra.HeaderText = ""; colTerra.ToolTipText = "Terraformable"; colTerra.ImageLayout = DataGridViewImageCellLayout.Normal; colTerra.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter; gridBodies.Columns.Add(colTerra);
            var colGeo = new DataGridViewImageColumn(); colGeo.Name = "Geo"; colGeo.HeaderText = ""; colGeo.ToolTipText = "Geological Signals"; colGeo.ImageLayout = DataGridViewImageCellLayout.Normal; colGeo.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter; gridBodies.Columns.Add(colGeo);
            var colBio = new DataGridViewImageColumn(); colBio.Name = "Bio"; colBio.HeaderText = ""; colBio.ToolTipText = "Biological Signals"; colBio.ImageLayout = DataGridViewImageCellLayout.Normal; colBio.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter; gridBodies.Columns.Add(colBio);
            var colLand = new DataGridViewImageColumn(); colLand.Name = "Landable"; colLand.HeaderText = ""; colLand.ToolTipText = "Landable"; colLand.ImageLayout = DataGridViewImageCellLayout.Normal; colLand.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter; gridBodies.Columns.Add(colLand);
            gridBodies.Columns.Add("Gravity", "Gravity");
            var colMat = new DataGridViewImageColumn(); colMat.Name = "Materials"; colMat.HeaderText = ""; colMat.ToolTipText = "Jumponium Materials"; colMat.ImageLayout = DataGridViewImageCellLayout.Normal; colMat.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter; gridBodies.Columns.Add(colMat);
            var colDisc = new DataGridViewImageColumn(); colDisc.Name = "FirstDiscovery"; colDisc.HeaderText = ""; colDisc.ToolTipText = "First Discovery"; colDisc.ImageLayout = DataGridViewImageCellLayout.Normal; colDisc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter; gridBodies.Columns.Add(colDisc);
            gridBodies.Columns.Add("EDSM", "EDSM");
            gridBodies.Columns.Add("Distance", "Distance");
            gridBodies.Columns.Add("FullName", "ID"); gridBodies.Columns["FullName"].Visible = false;

            // Ajuste Anchos
            gridBodies.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridBodies.Columns["Name"].FillWeight = 80;
            gridBodies.Columns["Type"].FillWeight = 110;
            gridBodies.Columns["Atmosphere"].FillWeight = 80;
            gridBodies.Columns["SurfaceScan"].FillWeight = 35;
            gridBodies.Columns["Valuable"].FillWeight = 35;
            gridBodies.Columns["Terra"].FillWeight = 35;
            gridBodies.Columns["Geo"].FillWeight = 35;
            gridBodies.Columns["Bio"].FillWeight = 35;
            gridBodies.Columns["Landable"].FillWeight = 35;
            gridBodies.Columns["Materials"].FillWeight = 40;
            gridBodies.Columns["FirstDiscovery"].FillWeight = 35;
            gridBodies.Columns["Distance"].FillWeight = 50;

            tabCurrent.Controls.Add(gridBodies);
        }

        private void BtnSettings_Click(object sender, EventArgs e)
        {
            SettingsForm settingsWindow = new SettingsForm();
            settingsWindow.ShowDialog();
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
            journalFolder = Path.Combine(userProfile, "Saved Games", "Frontier Developments", "Elite Dangerous");
            if (!Directory.Exists(journalFolder)) return;

            Task.Run(() => PerformFullSync());

            logTimer = new System.Windows.Forms.Timer();
            logTimer.Interval = 1000;
            logTimer.Tick += MonitorLive;
            logTimer.Start();
        }

        private void PerformFullSync()
        {
            isLoadingHistory = true;
            this.Invoke(new Action(() => lblStatus.Text = "Synchronizing..."));

            var directory = new DirectoryInfo(journalFolder);
            var files = directory.GetFiles("Journal.*.log").OrderByDescending(f => f.LastWriteTime).ToList();
            if (files.Count == 0) return;

            currentFile = files[0].FullName;

            // 1. Find Current Location (GPS)
            foreach (var file in files)
            {
                if (!string.IsNullOrEmpty(currentSystem)) break;
                var lines = File.ReadAllLines(file.FullName);
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    try
                    {
                        using (JsonDocument doc = JsonDocument.Parse(lines[i]))
                        {
                            if (doc.RootElement.TryGetProperty("event", out JsonElement evt))
                            {
                                string e = evt.GetString();
                                if ((e == "FSDJump" || e == "Location") && doc.RootElement.TryGetProperty("StarSystem", out JsonElement sys))
                                {
                                    currentSystem = sys.GetString(); break;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            if (string.IsNullOrEmpty(currentSystem))
            {
                this.Invoke(new Action(() => lblStatus.Text = "Waiting for location..."));
                isLoadingHistory = false;
                return;
            }

            this.Invoke(new Action(() => { lblSystem.Text = currentSystem; lblStatus.Text = "Loading system data..."; }));

            // 2. Load Scans from Journal (Single Pass logic to avoid duplicates in list)
            // Using Dictionary to ensure uniqueness by BodyName
            Dictionary<string, JsonElement> localBodies = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            bool arrivalFound = false;

            foreach (var file in files)
            {
                if (arrivalFound) break;
                var lines = File.ReadAllLines(file.FullName);
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    try
                    {
                        using (JsonDocument doc = JsonDocument.Parse(lines[i]))
                        {
                            JsonElement root = doc.RootElement.Clone();
                            if (!root.TryGetProperty("event", out JsonElement evt)) continue;
                            string e = evt.GetString();

                            if (e == "FSDJump" && root.GetProperty("StarSystem").GetString() == currentSystem)
                            {
                                arrivalFound = true; break;
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

            // 3. Render Grid
            var sortedBodies = localBodies.Values.OrderBy(b => b.GetProperty("BodyName").GetString()).ToList();
            this.Invoke(new Action(() => {
                gridBodies.Rows.Clear();
                foreach (var body in sortedBodies) AddRowFromJournal(body);

                lastFileSize = new FileInfo(currentFile).Length;
                isLoadingHistory = false;
                lblStatus.Text = "Journal Sync Complete.";
                lblStatus.ForeColor = Color.White;

                // 4. Fetch EDSM (Async)
                FetchEdsmData(currentSystem);
            }));
        }

        private void MonitorLive(object sender, EventArgs e)
        {
            try
            {
                var fileInfo = new FileInfo(currentFile);
                fileInfo.Refresh();
                var directory = new DirectoryInfo(journalFolder);
                var newest = directory.GetFiles("Journal.*.log").OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
                if (newest != null && newest.FullName != currentFile) { currentFile = newest.FullName; lastFileSize = 0; return; }

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

            // Anti-Duplicate Check: If exists (maybe created by EDSM), Update it.
            foreach (DataGridViewRow r in gridBodies.Rows)
            {
                if (NamesMatch(r.Cells["FullName"].Value.ToString(), fullName))
                {
                    UpdateRowFromJournal(r, root); // Convert Blue EDSM row to White Journal row
                    return;
                }
            }

            // Extract Data
            bool isStar = false; string typeCode = "", typeDescription = "", rawType = "";
            if (root.TryGetProperty("StarType", out JsonElement st)) { isStar = true; typeCode = st.GetString(); typeDescription = EliteLogic.GetStarDescription(typeCode); }
            else if (root.TryGetProperty("PlanetClass", out JsonElement pc2)) { rawType = pc2.GetString(); typeDescription = rawType; }

            string shortName = CalculateShortName(fullName, isStar);
            double dist = root.TryGetProperty("DistanceFromArrivalLS", out JsonElement d) ? d.GetDouble() : 0;
            string distStr = Math.Round(dist, 0) + " Ls";

            // Materials
            List<string> materials = new List<string>();
            if (root.TryGetProperty("Materials", out JsonElement mats) && mats.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in mats.EnumerateArray()) if (m.TryGetProperty("Name", out JsonElement mn)) materials.Add(mn.GetString());
            }

            // Signals
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

            // Check Duplicates
            foreach (DataGridViewRow r in gridBodies.Rows)
            {
                if (NamesMatch(r.Cells["FullName"].Value.ToString(), edsmName))
                {
                    // Exists (from Journal). Update only EDSM specific data.
                    UpdateRowFromEdsm(r, body);
                    return;
                }
            }

            // New Body from EDSM (Not scanned by us yet)
            string shortName = CalculateShortName(edsmName, false); // Unknown if star yet, roughly safe
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

            gridBodies.Rows[idx].DefaultCellStyle.ForeColor = Color.LightSteelBlue; // EDSM Color
            gridBodies.Rows[idx].Cells["Materials"].ToolTipText = EliteLogic.GetMaterialTooltip(materials);
            if (val > 500000)
            {
                gridBodies.Rows[idx].DefaultCellStyle.BackColor = Color.FromArgb(0, 50, 0);
                gridBodies.Rows[idx].DefaultCellStyle.ForeColor = Color.White;
            }
        }

        // --- HELPERS PARA ACTUALIZAR FILAS ---

        private void UpdateRowFromJournal(DataGridViewRow row, JsonElement root)
        {
            // We scanned a body that EDSM already put on the list.
            // Overwrite with our accurate scan data, preserve EDSM Discoverer.
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

            // Signals
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

            // Reset Color to Journal Style
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
            // Just update Discovery info and Value if better
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

        private bool NamesMatch(string n1, string n2)
        {
            if (string.Equals(n1, n2, StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(currentSystem))
            {
                string s = currentSystem.ToUpper().Trim();
                string a = n1.ToUpper().Trim();
                string b = n2.ToUpper().Trim();
                // Check "Sys" == "Sys A" logic
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