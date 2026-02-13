using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Text;

namespace EliteExplorerTool.Modules
{
    public class HistoryModule : IEliteModule
    {
        public string ModuleName => "History";

        // --- CONSTANTES ---
        private const string SAVE_FILE = "history_data.json";
        private const int EdsmDelayMs = 2000;

        // --- UI ELEMENTS ---
        private SplitContainer splitContainer;
        private DataGridView gridSystems;
        private DataGridView gridDetails;
        private TextBox txtSearch;
        private Label lblStatusInfo;
        private Label lblPageInfo;
        private Label lblSystemHeader;
        private ProgressBar progImport;
        private Button btnPrev, btnNext;
        private Button btnImport, btnExport, btnFilterDisc;
        private TableLayoutPanel pnlButtons;

        // --- DATA ---
        private List<SystemHistoryData> allSystems = new List<SystemHistoryData>();
        private List<SystemHistoryData> filteredSystems = new List<SystemHistoryData>();
        private string journalFolder;
        private int currentPage = 0;
        private const int ItemsPerPage = 18;
        private bool showOnlyFirstDiscovery = false;

        private string lastKnownSystem = "";
        private bool isSyncing = false;

        // --- ICONOS ---
        private Image iconSurface, iconMoney, iconTerra, iconGeo, iconBio, iconLand, iconDiscovery, iconMaterials;

        public HistoryModule(string logsPath)
        {
            this.journalFolder = logsPath;
            LoadIcons();
            SetupUI();
        }

        public Control GetControl() => splitContainer;

        // --- CORRECCIÓN AQUÍ: Configuración de tamaño seguro ---
        public void OnLoad()
        {
            try
            {
                splitContainer.SplitterDistance = 400;
                // Aplicamos el tamaño mínimo AQUÍ, cuando el control ya existe visualmente.
                splitContainer.Panel1MinSize = 400;
            }
            catch { }

            Task.Run(() => LoadHistoryData());
        }

        public void OnShutdown()
        {
            SaveHistoryData();
        }

        private void LoadIcons()
        {
            iconSurface = ResizeImg(Properties.Resources.iconSurface);
            iconMoney = ResizeImg(Properties.Resources.iconMoney);
            iconTerra = ResizeImg(Properties.Resources.iconTerraformable);
            iconGeo = ResizeImg(Properties.Resources.iconGeo);
            iconBio = ResizeImg(Properties.Resources.iconBio);
            iconLand = ResizeImg(Properties.Resources.iconLandable);
            iconDiscovery = ResizeImg(Properties.Resources.iconDiscovery);
            iconMaterials = ResizeImg(Properties.Resources.iconMaterials);
        }

        private void SetupUI()
        {
            int targetWidth = 400;

            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(10, 10, 15),
                FixedPanel = FixedPanel.Panel1,
                IsSplitterFixed = false
                // NOTA: Panel1MinSize eliminado de aquí para evitar el bloqueo del programa
            };

            // --- PANEL IZQUIERDO (TOP) ---
            var panelLeftTop = new Panel { Dock = DockStyle.Top, Height = 130, BackColor = Color.FromArgb(15, 15, 20), Padding = new Padding(10) };

            var lblSearch = new Label { Text = "SEARCH (Name, Type, 'Terra'...):", ForeColor = Color.Gray, Font = new Font("Arial", 8, FontStyle.Bold), Location = new Point(10, 10), AutoSize = true };

            txtSearch = new TextBox
            {
                Location = new Point(10, 30),
                Height = 25,
                Width = targetWidth - 40,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.FromArgb(30, 30, 35),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            txtSearch.TextChanged += (s, e) => ApplyFilters();

            // --- BOTONES ---
            int btnY = 65;
            pnlButtons = new TableLayoutPanel
            {
                Location = new Point(10, btnY),
                Size = new Size(300, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.Transparent
            };

            float colWidth = 95F;
            pnlButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, colWidth));
            pnlButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, colWidth));
            pnlButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, colWidth));

            btnImport = CreateButton("Import Logs");
            btnExport = CreateButton("Export CSV");
            btnFilterDisc = CreateButton("First Disc.");

            btnImport.Margin = new Padding(0, 0, 5, 0);
            btnExport.Margin = new Padding(0, 0, 5, 0);
            btnFilterDisc.Margin = new Padding(0, 0, 5, 0);

            pnlButtons.Controls.Add(btnImport, 0, 0);
            pnlButtons.Controls.Add(btnExport, 1, 0);
            pnlButtons.Controls.Add(btnFilterDisc, 2, 0);

            btnImport.Click += (s, e) => { if (!isSyncing) Task.Run(() => ImportAllJournals()); };
            btnExport.Click += (s, e) => ExportToCsv();
            btnFilterDisc.Click += (s, e) => ToggleFirstDiscovery();

            // Barra de Progreso
            progImport = new ProgressBar
            {
                Location = new Point(10, 105),
                Height = 5,
                Width = targetWidth - 40,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Visible = false,
                Style = ProgressBarStyle.Continuous
            };

            panelLeftTop.Controls.AddRange(new Control[] { lblSearch, txtSearch, pnlButtons, progImport });

            // --- GRID SISTEMAS ---
            gridSystems = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.Black,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ColumnHeadersHeight = 30,
                EnableHeadersVisualStyles = false
            };
            gridSystems.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White, Font = new Font("Arial", 9, FontStyle.Bold) };
            gridSystems.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.Black, ForeColor = Color.LightGray, Font = new Font("Consolas", 9), SelectionBackColor = Color.FromArgb(60, 60, 60) };

            gridSystems.Columns.Add("Date", "Date");
            gridSystems.Columns.Add("System", "System Name");
            gridSystems.Columns["Date"].Width = 110;
            gridSystems.Columns["System"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            gridSystems.SelectionChanged += GridSystems_SelectionChanged;

            // --- PANEL IZQUIERDO (BOTTOM) ---
            var panelLeftBottom = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = Color.FromArgb(15, 15, 20), Padding = new Padding(5) };

            btnPrev = CreateButton("<");
            btnPrev.Width = 40; btnPrev.Dock = DockStyle.Left; btnPrev.Margin = new Padding(0);

            btnNext = CreateButton(">");
            btnNext.Width = 40; btnNext.Dock = DockStyle.Right; btnNext.Margin = new Padding(0);

            lblPageInfo = new Label
            {
                Text = "Page 1",
                ForeColor = Color.Gray,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            lblPageInfo.BringToFront();

            panelLeftBottom.Controls.Add(lblPageInfo);
            panelLeftBottom.Controls.Add(btnPrev);
            panelLeftBottom.Controls.Add(btnNext);

            btnPrev.Click += (s, e) => ChangePage(-1);
            btnNext.Click += (s, e) => ChangePage(1);

            splitContainer.Panel1.Controls.Add(gridSystems);
            splitContainer.Panel1.Controls.Add(panelLeftTop);
            splitContainer.Panel1.Controls.Add(panelLeftBottom);

            // --- PANEL DERECHO ---
            var panelRightTop = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = Color.FromArgb(20, 20, 25) };

            lblSystemHeader = new Label
            {
                Text = "NO SYSTEM SELECTED",
                ForeColor = Color.White,
                Font = new Font("Arial", 18, FontStyle.Bold),
                Location = new Point(10, 10),
                AutoSize = true
            };

            lblStatusInfo = new Label
            {
                Text = "Waiting...",
                ForeColor = Color.Gray,
                Font = new Font("Arial", 10, FontStyle.Italic),
                Location = new Point(12, 40),
                AutoSize = true
            };

            panelRightTop.Controls.AddRange(new Control[] { lblSystemHeader, lblStatusInfo });

            gridDetails = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(5, 5, 10),
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ColumnHeadersHeight = 40,
                EnableHeadersVisualStyles = false,
                RowTemplate = { Height = 30 },
                ShowCellToolTips = true
            };

            gridDetails.ColumnHeadersDefaultCellStyle = gridSystems.ColumnHeadersDefaultCellStyle;
            gridDetails.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(10, 10, 15), ForeColor = Color.White, Font = new Font("Consolas", 9), SelectionBackColor = Color.FromArgb(50, 50, 60) };

            gridDetails.CellPainting += GridDetails_CellPainting;
            gridDetails.CellToolTipTextNeeded += GridDetails_CellToolTipTextNeeded;

            gridDetails.Columns.Add("Name", "Name");
            gridDetails.Columns.Add("Type", "Type");
            AddIconCol(gridDetails, "SurfaceScan", "Scan");
            AddIconCol(gridDetails, "Terra", "Terraformable");
            AddIconCol(gridDetails, "Materials", "Materials");
            AddIconCol(gridDetails, "FirstDiscovery", "Discovery");
            gridDetails.Columns.Add("Distance", "Dist.");

            gridDetails.Columns["Name"].FillWeight = 120;
            gridDetails.Columns["Type"].FillWeight = 150;
            gridDetails.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            foreach (DataGridViewColumn c in gridDetails.Columns) if (c is DataGridViewImageColumn) c.FillWeight = 40;

            splitContainer.Panel2.Controls.Add(gridDetails);
            splitContainer.Panel2.Controls.Add(panelRightTop);

            EnableDoubleBuffer(gridSystems);
            EnableDoubleBuffer(gridDetails);
        }

        // --- LÓGICA DE DATOS ---

        public void HandleJournalEvent(string eventType, JsonElement root) => ProcessEvent(eventType, root, live: true);
        public void HandleHistoryEvent(string eventType, JsonElement root) => ProcessEvent(eventType, root, live: false);

        private void ProcessEvent(string eventType, JsonElement root, bool live)
        {
            if (eventType == "FSDJump" || eventType == "Location")
            {
                string sysName = root.TryGetProperty("StarSystem", out var s) ? s.GetString() ?? "Unknown" : "Unknown";
                string timestamp = root.TryGetProperty("timestamp", out var t) ? t.GetString() ?? "" : "";

                lastKnownSystem = sysName;
                var sys = GetOrCreateSystem(sysName, timestamp);
                if (live) RefreshPage();
            }
            else if (eventType == "Scan")
            {
                string sysName = root.TryGetProperty("StarSystem", out var s) ? s.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(sysName)) sysName = lastKnownSystem;
                if (string.IsNullOrEmpty(sysName)) return;

                string bodyName = root.TryGetProperty("BodyName", out var bn) ? bn.GetString() ?? "Unknown" : "Unknown";
                if (bodyName.Contains("Belt Cluster")) return;

                var sys = GetOrCreateSystem(sysName, null);
                if (sys.Bodies.Any(b => b.FullName == bodyName)) return;

                List<string> mats = new List<string>();
                if (root.TryGetProperty("Materials", out JsonElement mArray) && mArray.ValueKind == JsonValueKind.Array)
                    foreach (var m in mArray.EnumerateArray())
                        if (m.TryGetProperty("Name", out var n)) mats.Add(n.GetString() ?? "");

                var body = new BodyHistoryData
                {
                    FullName = bodyName,
                    ShortName = bodyName.Replace(sysName, "").Trim(),
                    Type = GetBodyType(root),
                    IsTerraformable = root.TryGetProperty("TerraformState", out var ts) && ts.GetString() == "Terraformable",
                    JumponiumLevel = EliteLogic.GetJumponiumLevel(mats),
                    MaterialsList = mats,
                    IsFirstDiscovery = !root.TryGetProperty("WasDiscovered", out var wd) || !wd.GetBoolean(),
                    Distance = root.TryGetProperty("DistanceFromArrivalLS", out var d) ? d.GetDouble() : 0
                };

                if (string.IsNullOrEmpty(body.ShortName)) body.ShortName = "Primary";
                sys.Bodies.Add(body);
                if (body.IsFirstDiscovery) sys.HasFirstDiscovery = true;

                if (live && gridSystems.SelectedRows.Count > 0 && gridSystems.SelectedRows[0].Cells["System"].Value.ToString() == sysName)
                    ShowDetails(sys);
            }
            else if (eventType == "FSSAllBodiesFound")
            {
                string? sysName = root.TryGetProperty("SystemName", out var s) ? s.GetString() : null;
                if (sysName != null)
                {
                    var sys = GetOrCreateSystem(sysName, null);
                    sys.IsCompleted = true;
                    if (live && gridSystems.SelectedRows.Count > 0 && gridSystems.SelectedRows[0].Cells["System"].Value.ToString() == sysName)
                        UpdateStatusLabel(true);
                }
            }
        }

        private SystemHistoryData GetOrCreateSystem(string name, string? timestamp)
        {
            var sys = allSystems.FirstOrDefault(s => s.Name == name);
            if (sys == null)
            {
                sys = new SystemHistoryData { Name = name, Bodies = new List<BodyHistoryData>() };
                allSystems.Insert(0, sys);
            }
            if (!string.IsNullOrEmpty(timestamp)) sys.Timestamp = DateTime.Parse(timestamp);
            return sys;
        }

        // --- PERSISTENCIA (JSON) ---
        private void SaveHistoryData()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SAVE_FILE);
                string json = JsonSerializer.Serialize(allSystems);
                File.WriteAllText(path, json);
            }
            catch { }
        }

        private void LoadHistoryData()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SAVE_FILE);
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var loaded = JsonSerializer.Deserialize<List<SystemHistoryData>>(json);
                    if (loaded != null)
                    {
                        allSystems = loaded;
                        gridSystems.Invoke(new Action(() => { ApplyFilters(); UpdateStatusUI($"Loaded {allSystems.Count} systems from storage."); }));
                    }
                }
            }
            catch { }
        }

        // --- IMPORTACIÓN Y EDSM SYNC ---

        private async void ImportAllJournals()
        {
            if (isSyncing) return;
            isSyncing = true;
            btnImport.Invoke(new Action(() => btnImport.Enabled = false));

            progImport.Invoke(new Action(() => { progImport.Visible = true; progImport.Value = 0; progImport.ForeColor = Color.Green; }));
            UpdateStatusUI("Reading local journals...");

            await Task.Run(() => {
                allSystems.Clear();
                lastKnownSystem = "";
                var dir = new DirectoryInfo(journalFolder);
                var files = dir.GetFiles("Journal.*.log").OrderBy(f => f.LastWriteTime).ToArray();

                int totalFiles = files.Length;
                progImport.Invoke(new Action(() => { progImport.Maximum = totalFiles; }));

                int processed = 0;
                foreach (var file in files)
                {
                    var lines = ReadLinesSafely(file.FullName);
                    foreach (var line in lines)
                    {
                        try { using (JsonDocument doc = JsonDocument.Parse(line)) { var root = doc.RootElement; if (root.TryGetProperty("event", out var e)) ProcessEvent(e.GetString() ?? "", root, live: false); } } catch { }
                    }
                    processed++;
                    if (processed % 5 == 0 || processed == totalFiles) progImport.Invoke(new Action(() => { progImport.Value = processed; }));
                }
            });

            allSystems = allSystems.OrderByDescending(s => s.Timestamp).ToList();
            gridSystems.Invoke(new Action(() => ApplyFilters()));
            SaveHistoryData(); // Guardar lo importado localmente

            // --- INICIAR SYNC CON EDSM ---
            await SyncWithEdsm();

            isSyncing = false;
            btnImport.Invoke(new Action(() => btnImport.Enabled = true));
            progImport.Invoke(new Action(() => progImport.Visible = false));
        }

        private async Task SyncWithEdsm()
        {
            var incompleteSystems = allSystems.Where(s => !s.IsCompleted).ToList();
            if (incompleteSystems.Count == 0) return;

            UpdateStatusUI($"Syncing {incompleteSystems.Count} incomplete systems with EDSM...");

            progImport.Invoke(new Action(() => {
                progImport.Maximum = incompleteSystems.Count;
                progImport.Value = 0;
                progImport.ForeColor = Color.Cyan;
            }));

            int processed = 0;
            foreach (var sys in incompleteSystems)
            {
                if (splitContainer.IsDisposed) break;

                try
                {
                    string json = await EdsmService.GetBodies(sys.Name);
                    if (!string.IsNullOrEmpty(json))
                    {
                        using (JsonDocument doc = JsonDocument.Parse(json))
                        {
                            if (doc.RootElement.TryGetProperty("bodies", out JsonElement bodies))
                            {
                                bool addedNew = false;
                                foreach (var b in bodies.EnumerateArray())
                                {
                                    if (ProcessEdsmBodyIntoHistory(sys, b)) addedNew = true;
                                }
                                if (addedNew) sys.IsCompleted = true;
                            }
                        }
                    }
                }
                catch { }

                processed++;
                progImport.Invoke(new Action(() => progImport.Value = processed));
                UpdateStatusUI($"EDSM Sync: {processed}/{incompleteSystems.Count} - {sys.Name}");

                if (processed % 10 == 0) SaveHistoryData();
                await Task.Delay(EdsmDelayMs);
            }

            SaveHistoryData();
            UpdateStatusUI("EDSM Sync Complete.");
            gridSystems.Invoke(new Action(() => ApplyFilters()));
        }

        private bool ProcessEdsmBodyIntoHistory(SystemHistoryData sys, JsonElement edsmBody)
        {
            string name = edsmBody.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(name)) return false;
            if (sys.Bodies.Any(b => b.FullName == name)) return false;

            string type = "Unknown";
            if (edsmBody.TryGetProperty("subType", out JsonElement st)) type = st.GetString() ?? "Unknown";
            else if (edsmBody.TryGetProperty("type", out JsonElement ty)) type = ty.GetString() ?? "Unknown";

            bool terra = edsmBody.TryGetProperty("terraformingState", out JsonElement tfs) && tfs.GetString() == "Terraformable";
            double dist = edsmBody.TryGetProperty("distanceToArrival", out JsonElement d) ? d.GetDouble() : 0;

            var body = new BodyHistoryData
            {
                FullName = name,
                ShortName = name.Replace(sys.Name, "").Trim(),
                Type = type,
                IsTerraformable = terra,
                Distance = dist,
                IsFirstDiscovery = false // EDSM ya lo tiene
            };
            if (string.IsNullOrEmpty(body.ShortName)) body.ShortName = "Primary";

            sys.Bodies.Add(body);
            return true;
        }

        // --- FILTROS Y UI ---

        private void ApplyFilters()
        {
            string search = txtSearch.Text.ToLower().Trim();
            filteredSystems = allSystems.Where(s =>
            {
                bool matchName = string.IsNullOrEmpty(search) || s.Name.ToLower().Contains(search);
                bool matchBody = !string.IsNullOrEmpty(search) && s.Bodies.Any(b => b.FullName.ToLower().Contains(search) || b.Type.ToLower().Contains(search) || (search.Contains("terra") && b.IsTerraformable));
                bool matchDisc = !showOnlyFirstDiscovery || s.HasFirstDiscovery;
                return (matchName || matchBody) && matchDisc;
            }).ToList();
            currentPage = 0;
            RefreshPage();
        }

        private void ToggleFirstDiscovery()
        {
            showOnlyFirstDiscovery = !showOnlyFirstDiscovery;
            btnFilterDisc.BackColor = showOnlyFirstDiscovery ? Color.Green : Color.FromArgb(40, 40, 50);
            ApplyFilters();
        }

        private void ChangePage(int delta)
        {
            int maxPage = (int)Math.Ceiling((double)filteredSystems.Count / ItemsPerPage) - 1;
            currentPage += delta;
            if (currentPage < 0) currentPage = 0;
            if (currentPage > maxPage) currentPage = maxPage;
            RefreshPage();
        }

        private void RefreshPage()
        {
            if (gridSystems.InvokeRequired) { gridSystems.Invoke(new Action(RefreshPage)); return; }
            gridSystems.Rows.Clear();
            if (filteredSystems.Count == 0 && string.IsNullOrEmpty(txtSearch.Text) && !showOnlyFirstDiscovery && allSystems.Count > 0)
                filteredSystems = new List<SystemHistoryData>(allSystems);

            int start = currentPage * ItemsPerPage;
            var pageItems = filteredSystems.Skip(start).Take(ItemsPerPage).ToList();

            foreach (var sys in pageItems) gridSystems.Rows.Add(sys.Timestamp.ToString("yyyy-MM-dd HH:mm"), sys.Name);

            int totalPages = (int)Math.Ceiling((double)filteredSystems.Count / ItemsPerPage);
            if (totalPages == 0) totalPages = 1;
            lblPageInfo.Text = $"Page {currentPage + 1} of {totalPages} ({filteredSystems.Count} Systems)";
            btnPrev.Enabled = currentPage > 0;
            btnNext.Enabled = currentPage < totalPages - 1;
        }

        private void GridSystems_SelectionChanged(object sender, EventArgs e)
        {
            if (gridSystems.SelectedRows.Count == 0) return;
            string sysName = gridSystems.SelectedRows[0].Cells["System"].Value.ToString() ?? "";
            var sys = allSystems.FirstOrDefault(s => s.Name == sysName);
            if (sys != null) ShowDetails(sys);
        }

        private void ShowDetails(SystemHistoryData sys)
        {
            gridDetails.Rows.Clear();
            lblSystemHeader.Text = sys.Name.ToUpper();
            UpdateStatusLabel(sys.IsCompleted);
            foreach (var body in sys.Bodies.OrderBy(b => b.Distance))
            {
                int idx = gridDetails.Rows.Add(body.ShortName, body.Type, iconSurface, body.IsTerraformable ? iconTerra : GetEmptyImg(), GetJumpIcon(body.JumponiumLevel), body.IsFirstDiscovery ? iconDiscovery : GetEmptyImg(), Math.Round(body.Distance, 0) + " Ls");
                gridDetails.Rows[idx].Tag = body;
                if (body.IsFirstDiscovery) gridDetails.Rows[idx].DefaultCellStyle.ForeColor = Color.Yellow;
            }
        }

        private void GridDetails_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex == -1 && e.ColumnIndex >= 0)
            {
                e.PaintBackground(e.CellBounds, true);
                Image? img = null;
                string colName = gridDetails.Columns[e.ColumnIndex].Name;
                if (colName == "SurfaceScan") img = iconSurface;
                else if (colName == "Terra") img = iconTerra;
                else if (colName == "Materials") img = iconMaterials;
                else if (colName == "FirstDiscovery") img = iconDiscovery;
                if (img != null) { int sz = 24; e.Graphics.DrawImage(img, new Rectangle(e.CellBounds.Left + (e.CellBounds.Width - sz) / 2, e.CellBounds.Top + (e.CellBounds.Height - sz) / 2, sz, sz)); e.Handled = true; }
                else e.PaintContent(e.CellBounds);
            }
        }

        private void GridDetails_CellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && gridDetails.Columns[e.ColumnIndex].Name == "Materials")
                if (gridDetails.Rows[e.RowIndex].Tag is BodyHistoryData body && body.MaterialsList.Count > 0) e.ToolTipText = EliteLogic.GetMaterialTooltip(body.MaterialsList);
        }

        private void UpdateStatusLabel(bool completed)
        {
            if (completed) { lblStatusInfo.Text = "SYSTEM SCANNED COMPLETELY"; lblStatusInfo.ForeColor = Color.LightGreen; }
            else { lblStatusInfo.Text = "SYSTEM INCOMPLETE / PARTIAL SCAN"; lblStatusInfo.ForeColor = Color.Orange; }
        }

        private void UpdateStatusUI(string msg) => lblStatusInfo.Invoke(new Action(() => { lblStatusInfo.Text = msg; lblStatusInfo.ForeColor = Color.White; }));

        private void ExportToCsv()
        {
            if (filteredSystems.Count == 0) { MessageBox.Show("No data to export."); return; }
            SaveFileDialog sfd = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "ExplorationHistory.csv" };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Date,System,Body,Type,Distance,Terraformable,FirstDiscovery");
                foreach (var sys in filteredSystems) foreach (var b in sys.Bodies) sb.AppendLine($"{sys.Timestamp},{sys.Name},{b.FullName},{b.Type},{b.Distance},{b.IsTerraformable},{b.IsFirstDiscovery}");
                File.WriteAllText(sfd.FileName, sb.ToString());
                MessageBox.Show("Export Successful!");
            }
        }

        private Button CreateButton(string text) => new Button { Text = text, Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(40, 40, 50), ForeColor = Color.White, Cursor = Cursors.Hand, Margin = new Padding(2) };
        private void AddIconCol(DataGridView grid, string name, string header) => grid.Columns.Add(new DataGridViewImageColumn { Name = name, HeaderText = header, ImageLayout = DataGridViewImageCellLayout.Normal });
        private List<string> ReadLinesSafely(string filePath) { var lines = new List<string>(); try { using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var sr = new StreamReader(fs)) { while (!sr.EndOfStream) { string? l = sr.ReadLine(); if (l != null) lines.Add(l); } } } catch { } return lines; }
        private string GetBodyType(JsonElement root) { if (root.TryGetProperty("StarType", out var st)) return EliteLogic.GetStarDescription(st.GetString() ?? ""); if (root.TryGetProperty("PlanetClass", out var pc)) return pc.GetString() ?? ""; return "Unknown"; }
        private Image ResizeImg(Image img) => img == null ? GetEmptyImg() : new Bitmap(img, new Size(20, 20));
        private Image GetEmptyImg() { Bitmap bmp = new Bitmap(20, 20); using (Graphics g = Graphics.FromImage(bmp)) g.Clear(Color.Transparent); return bmp; }
        private Image GetJumpIcon(EliteLogic.JumpLevel level)
        {
            if (level == EliteLogic.JumpLevel.Premium) return ResizeImg(Properties.Resources.iconPremium);
            if (level == EliteLogic.JumpLevel.Standard) return ResizeImg(Properties.Resources.iconStandard);
            if (level == EliteLogic.JumpLevel.Basic) return ResizeImg(Properties.Resources.iconBasic);
            return GetEmptyImg();
        }
        private void EnableDoubleBuffer(DataGridView dgv) => typeof(Control).InvokeMember("DoubleBuffered", System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, dgv, new object[] { true });

        public class SystemHistoryData { public string Name { get; set; } = ""; public DateTime Timestamp { get; set; } public List<BodyHistoryData> Bodies { get; set; } = new List<BodyHistoryData>(); public bool IsCompleted { get; set; } = false; public bool HasFirstDiscovery { get; set; } = false; }
        public class BodyHistoryData { public string FullName { get; set; } = ""; public string ShortName { get; set; } = ""; public string Type { get; set; } = ""; public bool IsTerraformable { get; set; } public EliteLogic.JumpLevel JumponiumLevel { get; set; } public List<string> MaterialsList { get; set; } = new List<string>(); public bool IsFirstDiscovery { get; set; } public double Distance { get; set; } }
    }
}