using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Reflection;

namespace EliteExplorerTool.Modules
{
    public class CurrentSystemModule : IEliteModule
    {
        public string ModuleName => "Current System";

        private DataGridView grid;
        private Action<string> speakCallback;
        private string currentSystem = "";
        private bool isHistoryMode = false;

        // --- NUEVO: Lista para recordar qué cuerpos ya conoce EDSM de este sistema ---
        private HashSet<string> edsmKnownBodies = new HashSet<string>();

        public CurrentSystemModule(Action<string> speakAction)
        {
            this.speakCallback = speakAction;
            SetupGrid();
        }

        public Control GetControl()
        {
            return grid;
        }

        public void OnLoad()
        {
            ApplySettings();
        }

        public void OnShutdown() { }

        // --- API PÚBLICA ---
        public void SetCurrentSystem(string systemName, bool forceClear = false)
        {
            if (string.IsNullOrEmpty(systemName)) return;

            if (currentSystem != systemName || forceClear)
            {
                currentSystem = systemName;
                // Limpiamos la lista de conocidos al cambiar de sistema
                edsmKnownBodies.Clear();

                UpdateGridSafe(() => {
                    grid.Rows.Clear();
                    FetchEdsmData(currentSystem);
                });
            }
        }

        // --- EVENTOS ---
        public void HandleJournalEvent(string eventType, JsonElement eventData)
        {
            isHistoryMode = false;
            ProcessEvent(eventType, eventData);
        }

        public void HandleHistoryEvent(string eventType, JsonElement eventData)
        {
            isHistoryMode = true;
            ProcessEvent(eventType, eventData);
        }

        private void ProcessEvent(string eventType, JsonElement root)
        {
            if (eventType == "FSDJump" || eventType == "Location")
            {
                if (root.TryGetProperty("StarSystem", out JsonElement sys))
                {
                    SetCurrentSystem(sys.GetString() ?? "", forceClear: true);
                }
            }
            else if (eventType == "Scan")
            {
                if (root.TryGetProperty("StarSystem", out JsonElement ss))
                {
                    string scanSystem = ss.GetString() ?? "";

                    // Solo procesamos si el escaneo pertenece al sistema que estamos viendo
                    if (scanSystem == currentSystem && !string.IsNullOrEmpty(currentSystem))
                    {
                        UpdateGridSafe(() => AddRowFromJournal(root));

                        // --- LÓGICA DE SUBIDA A EDSM ---
                        // Solo subimos si NO estamos en modo historial (es decir, estamos jugando en vivo)
                        if (!isHistoryMode)
                        {
                            string bodyName = root.TryGetProperty("BodyName", out var bn) ? bn.GetString() : null;
                            if (!string.IsNullOrEmpty(bodyName))
                            {
                                // Si EDSM NO conoce este cuerpo, lo subimos
                                if (!edsmKnownBodies.Contains(bodyName))
                                {
                                    string rawJson = root.GetRawText(); // Obtenemos el JSON original del evento
                                    Task.Run(() => EdsmService.SendJournalEvent(rawJson));

                                    // Lo agregamos a la lista local para no subirlo de nuevo en esta sesión
                                    edsmKnownBodies.Add(bodyName);
                                }
                            }
                        }
                        // -------------------------------
                    }
                }
                else
                {
                    // Fallback para scans antiguos o parciales sin StarSystem explícito
                    string bodyName = root.GetProperty("BodyName").GetString() ?? "";
                    if (!string.IsNullOrEmpty(currentSystem) && bodyName.StartsWith(currentSystem, StringComparison.OrdinalIgnoreCase))
                    {
                        UpdateGridSafe(() => AddRowFromJournal(root));
                        // Aquí NO subimos a EDSM por seguridad, ya que falta el StarSystem explícito en el evento
                    }
                }
            }
        }

        // --- GRID UI ---
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
            grid.ShowCellToolTips = true;

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

            EnableDoubleBuffer(grid);

            grid.CellPainting += GridBodies_CellPainting;
            grid.CellToolTipTextNeeded += GridBodies_CellToolTipTextNeeded;

            grid.Columns.Add("Name", "Name");
            grid.Columns.Add("Type", "Type");
            grid.Columns.Add("Atmosphere", "Atmosphere");
            grid.Columns.Add("Temperature", "Temp");
            AddIconCol("SurfaceScan", "Scan Status");
            grid.Columns.Add("Value", "Value");
            AddIconCol("Valuable", "High Value");
            AddIconCol("Terra", "Terraformable");
            AddIconCol("Geo", "Geological");
            AddIconCol("Bio", "Biological");
            AddIconCol("Landable", "Landable");
            grid.Columns.Add("Gravity", "Gravity");
            AddIconCol("Materials", "Jumponium");
            AddIconCol("FirstDiscovery", "Discovery Status");
            grid.Columns.Add("EDSM", "EDSM");
            grid.Columns.Add("Distance", "Distance");
            grid.Columns.Add("FullName", "ID");
            grid.Columns["FullName"].Visible = false;

            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            foreach (DataGridViewColumn c in grid.Columns) if (c is DataGridViewImageColumn) c.FillWeight = 35;
            grid.Columns["Name"].FillWeight = 80;
            grid.Columns["Type"].FillWeight = 110;
            grid.Columns["Type"].MinimumWidth = 100; // Asegurar ancho mínimo para texto largo
        }

        private void EnableDoubleBuffer(DataGridView dgv)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, dgv, new object[] { true });
        }

        private void AddIconCol(string name, string tooltip)
        {
            var col = new DataGridViewImageColumn { Name = name, HeaderText = "", ToolTipText = tooltip, ImageLayout = DataGridViewImageCellLayout.Normal };
            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.Columns.Add(col);
        }

        public void ApplySettings()
        {
            if (grid == null) return;
            string hiddenString = Properties.Settings.Default.HiddenColumns ?? "";
            List<string> hiddenList = hiddenString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            foreach (DataGridViewColumn col in grid.Columns)
            {
                if (col.Name == "FullName") { col.Visible = false; continue; }
                if (col.Name == "Name") { col.Visible = true; continue; }
                col.Visible = !hiddenList.Contains(col.Name);
            }
        }

        // --- DATOS ---
        private void AddRowFromJournal(JsonElement root)
        {
            if (!root.TryGetProperty("BodyName", out JsonElement bn)) return;
            string fullName = bn.GetString() ?? "";
            if (fullName.Contains("Belt Cluster")) return;

            foreach (DataGridViewRow r in grid.Rows)
            {
                if (NamesMatch(r.Cells["FullName"].Value.ToString() ?? "", fullName))
                {
                    UpdateRowFromJournal(r, root);
                    return;
                }
            }

            int idx = grid.Rows.Add();
            DataGridViewRow row = grid.Rows[idx];
            row.Cells["FullName"].Value = fullName;

            UpdateRowFromJournal(row, root);
        }

        private void UpdateRowFromJournal(DataGridViewRow row, JsonElement root)
        {
            bool isStar = false;
            string typeCode = "", typeDesc = "";
            if (root.TryGetProperty("StarType", out JsonElement st))
            {
                isStar = true;
                typeCode = st.GetString() ?? "";
                typeDesc = EliteLogic.GetStarDescription(typeCode);
            }
            else if (root.TryGetProperty("PlanetClass", out JsonElement pc))
            {
                typeDesc = pc.GetString() ?? "Unknown Body";
            }

            string fullName = root.GetProperty("BodyName").GetString() ?? "";
            row.Cells["Name"].Value = CalculateShortName(fullName, isStar);
            row.Cells["Type"].Value = typeDesc;
            row.Cells["SurfaceScan"].Value = ResizeImg(Properties.Resources.iconSurface);

            row.Cells["Atmosphere"].Value = root.TryGetProperty("Atmosphere", out JsonElement at) ? at.GetString() : "-";
            double tempK = root.TryGetProperty("SurfaceTemperature", out JsonElement t) ? t.GetDouble() : 0;
            row.Cells["Temperature"].Value = tempK > 0 ? Math.Round(tempK, 0) + " K" : "-";

            double gravMS2 = root.TryGetProperty("SurfaceGravity", out JsonElement g) ? g.GetDouble() : 0;
            row.Cells["Gravity"].Value = gravMS2 > 0 ? Math.Round(gravMS2 / 9.81, 2) + " G" : "-";
            bool landable = root.TryGetProperty("Landable", out JsonElement l) && l.GetBoolean();
            row.Cells["Landable"].Value = landable ? ResizeImg(Properties.Resources.iconLandable) : GetEmptyImg();

            bool hasGeo = false, hasBio = false;
            if (root.TryGetProperty("Signals", out JsonElement sigs) && sigs.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in sigs.EnumerateArray())
                {
                    string sType = s.TryGetProperty("Type", out JsonElement stp) ? stp.GetString() ?? "" : "";
                    if (sType.Contains("Geological")) hasGeo = true;
                    if (sType.Contains("Biological")) hasBio = true;
                }
            }
            if (root.TryGetProperty("Genuses", out JsonElement gen) && gen.ValueKind == JsonValueKind.Array && gen.GetArrayLength() > 0) hasBio = true;
            row.Cells["Geo"].Value = hasGeo ? ResizeImg(Properties.Resources.iconGeo) : GetEmptyImg();
            row.Cells["Bio"].Value = hasBio ? ResizeImg(Properties.Resources.iconBio) : GetEmptyImg();

            List<string> materials = new List<string>();
            if (root.TryGetProperty("Materials", out JsonElement mats) && mats.ValueKind == JsonValueKind.Array)
                foreach (var m in mats.EnumerateArray()) materials.Add(m.GetProperty("Name").GetString() ?? "");
            row.Cells["Materials"].Value = GetJumpIcon(EliteLogic.GetJumponiumLevel(materials));
            row.Cells["Materials"].ToolTipText = EliteLogic.GetMaterialTooltip(materials);

            bool isTerra = root.TryGetProperty("TerraformState", out JsonElement ts) && ts.GetString() == "Terraformable";
            row.Cells["Terra"].Value = isTerra ? ResizeImg(Properties.Resources.iconTerraformable) : GetEmptyImg();

            long val = EliteLogic.CalculateValue(isStar ? typeCode : (root.TryGetProperty("PlanetClass", out JsonElement pc2) ? pc2.GetString() : ""), isTerra, isStar);
            row.Cells["Value"].Value = val.ToString("N0") + " Cr";
            row.Cells["Valuable"].Value = val > 500000 ? ResizeImg(Properties.Resources.iconMoney) : GetEmptyImg();

            double dist = root.TryGetProperty("DistanceFromArrivalLS", out JsonElement d) ? d.GetDouble() : 0;
            row.Cells["Distance"].Value = Math.Round(dist, 0) + " Ls";
            bool isDisc = !root.TryGetProperty("WasDiscovered", out JsonElement wd) || !wd.GetBoolean();
            row.Cells["FirstDiscovery"].Value = isDisc ? ResizeImg(Properties.Resources.iconDiscovery) : GetEmptyImg();

            if (isStar && EliteLogic.IsScoopable(typeCode))
            {
                row.Cells["Type"].Style.ForeColor = Color.LightGreen;
                row.Cells["Type"].Tag = "Scoopable";
            }
            else
            {
                row.Cells["Type"].Style.ForeColor = Color.White;
                row.Cells["Type"].Tag = null;
            }

            row.DefaultCellStyle.ForeColor = Color.White;
            if (val > 500000)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(0, 40, 0);
                if (!isHistoryMode && val > 1000000) speakCallback?.Invoke("High value body detected.");
            }
            else
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(20, 20, 20);
            }
        }

        private void ProcessEdsmBody(JsonElement body)
        {
            string edsmName = body.GetProperty("name").GetString() ?? "";
            foreach (DataGridViewRow r in grid.Rows)
                if (NamesMatch(r.Cells["FullName"].Value.ToString() ?? "", edsmName))
                {
                    if (body.TryGetProperty("discovery", out JsonElement disc) && disc.TryGetProperty("commander", out JsonElement cmdr))
                        r.Cells["EDSM"].Value = cmdr.GetString();
                    return;
                }

            string shortName = CalculateShortName(edsmName, false);
            string type = "Unknown";
            if (body.TryGetProperty("subType", out JsonElement st)) type = st.GetString() ?? "Unknown";
            else if (body.TryGetProperty("type", out JsonElement ty)) type = ty.GetString() ?? "Unknown";

            long val = body.TryGetProperty("estimatedValue", out JsonElement ev) ? ev.GetInt64() : 0;
            double dist = body.TryGetProperty("distanceToArrival", out JsonElement d) ? d.GetDouble() : 0;
            string discoverer = (body.TryGetProperty("discovery", out JsonElement dsc) && dsc.TryGetProperty("commander", out JsonElement c)) ? c.GetString() ?? "" : "";
            bool terra = body.TryGetProperty("terraformingState", out JsonElement tfs) && tfs.GetString() == "Terraformable";

            int idx = grid.Rows.Add(shortName, type, "-", "-", GetEmptyImg(), val > 0 ? val.ToString("N0") + " Cr" : "-", (val > 500000) ? ResizeImg(Properties.Resources.iconMoney) : GetEmptyImg(), terra ? ResizeImg(Properties.Resources.iconTerraformable) : GetEmptyImg(), GetEmptyImg(), GetEmptyImg(), GetEmptyImg(), "-", GetEmptyImg(), GetEmptyImg(), discoverer, Math.Round(dist, 0) + " Ls", edsmName);

            if (type.Contains("Star"))
            {
                string first = type.Substring(0, 1).ToUpper();
                if ("KMGFOBA".Contains(first))
                {
                    grid.Rows[idx].Cells["Type"].Style.ForeColor = Color.LightGreen;
                    grid.Rows[idx].Cells["Type"].Tag = "Scoopable";
                }
            }

            grid.Rows[idx].DefaultCellStyle.ForeColor = Color.LightSteelBlue;
            if (val > 500000) { grid.Rows[idx].DefaultCellStyle.BackColor = Color.FromArgb(0, 50, 0); grid.Rows[idx].DefaultCellStyle.ForeColor = Color.White; }
        }

        private async void FetchEdsmData(string systemName)
        {
            if (string.IsNullOrEmpty(systemName) || isHistoryMode) return;
            string requestedSystem = systemName;
            try
            {
                string json = await EdsmService.GetBodies(systemName);
                if (string.IsNullOrEmpty(json)) return;

                // --- NUEVO: Cacheamos los cuerpos conocidos por EDSM ---
                edsmKnownBodies.Clear();

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("bodies", out JsonElement bodies))
                    {
                        foreach (var b in bodies.EnumerateArray())
                        {
                            // Agregamos a la lista de conocidos
                            if (b.TryGetProperty("name", out var n)) edsmKnownBodies.Add(n.GetString());

                            UpdateGridSafe(() => {
                                if (requestedSystem == currentSystem) ProcessEdsmBody(b);
                            });
                        }
                    }
                }
            }
            catch { }
        }

        // --- HELPERS ---
        private void UpdateGridSafe(Action action)
        {
            if (grid == null || grid.IsDisposed) return;
            if (!grid.IsHandleCreated) { try { if (grid.InvokeRequired) grid.Invoke(new Action(() => { action(); })); else action(); } catch { } return; }
            if (grid.InvokeRequired) grid.Invoke(action); else action();
        }

        private bool NamesMatch(string n1, string n2)
        {
            if (string.Equals(n1, n2, StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(currentSystem))
            {
                string s = currentSystem.ToUpper().Trim(); string a = n1.ToUpper().Trim(); string b = n2.ToUpper().Trim();
                if ((a == s && b == s + " A") || (b == s && a == s + " A")) return true;
            }
            return false;
        }

        private string CalculateShortName(string fullName, bool isStar)
        {
            string s = fullName;
            if (!string.IsNullOrEmpty(currentSystem) && fullName.StartsWith(currentSystem, StringComparison.OrdinalIgnoreCase)) s = fullName.Substring(currentSystem.Length).Trim();
            return string.IsNullOrEmpty(s) || (isStar && s == "A") ? "Primary" : s;
        }

        private Image ResizeImg(Image img) => img == null ? GetEmptyImg() : new Bitmap(img, new Size(20, 20));
        private Image GetEmptyImg() { Bitmap bmp = new Bitmap(20, 20); using (Graphics g = Graphics.FromImage(bmp)) g.Clear(Color.Transparent); return bmp; }
        private Image GetJumpIcon(EliteLogic.JumpLevel level)
        {
            if (level == EliteLogic.JumpLevel.Premium) return ResizeImg(Properties.Resources.iconPremium);
            if (level == EliteLogic.JumpLevel.Standard) return ResizeImg(Properties.Resources.iconStandard);
            if (level == EliteLogic.JumpLevel.Basic) return ResizeImg(Properties.Resources.iconBasic);
            return GetEmptyImg();
        }

        private void GridBodies_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex == -1 && e.ColumnIndex >= 0)
            {
                e.PaintBackground(e.CellBounds, true);
                Image? img = null; string name = grid.Columns[e.ColumnIndex].Name;
                if (name == "SurfaceScan") img = Properties.Resources.iconSurface;
                else if (name == "Valuable") img = Properties.Resources.iconMoney;
                else if (name == "Terra") img = Properties.Resources.iconTerraformable;
                else if (name == "Geo") img = Properties.Resources.iconGeo;
                else if (name == "Bio") img = Properties.Resources.iconBio;
                else if (name == "Landable") img = Properties.Resources.iconLandable;
                else if (name == "Materials") img = Properties.Resources.iconMaterials;
                else if (name == "FirstDiscovery") img = Properties.Resources.iconDiscovery;
                if (img != null) { int sz = 24; e.Graphics.DrawImage(img, new Rectangle(e.CellBounds.Left + (e.CellBounds.Width - sz) / 2, e.CellBounds.Top + (e.CellBounds.Height - sz) / 2, sz, sz)); e.Handled = true; }
                else e.PaintContent(e.CellBounds);
            }
            else if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && grid.Columns[e.ColumnIndex].Name == "Type")
            {
                if (e.Value != null && grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Tag?.ToString() == "Scoopable")
                {
                    e.PaintBackground(e.CellBounds, true);
                    int sz = 16; int m = 4;
                    e.Graphics.DrawImage(Properties.Resources.iconScoopable, new Rectangle(e.CellBounds.Left + m, e.CellBounds.Top + (e.CellBounds.Height - sz) / 2, sz, sz));
                    TextRenderer.DrawText(e.Graphics, e.Value.ToString(), e.CellStyle.Font, new Rectangle(e.CellBounds.Left + sz + m + 5, e.CellBounds.Top, e.CellBounds.Width - sz - m, e.CellBounds.Height), e.CellStyle.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                    e.Handled = true;
                }
            }
        }

        private void GridBodies_CellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && grid.Columns[e.ColumnIndex].Name == "Materials")
                e.ToolTipText = grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText;
        }
    }
}