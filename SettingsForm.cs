using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EliteExplorerTool
{
    public class SettingsForm : Form
    {
        // Controles Existentes
        private CheckBox chkVoice;
        private TextBox txtCmdr;
        private TextBox txtApiKey;
        private Button btnSave;
        private Button btnCancel;

        // NUEVOS CONTROLES (Columnas y Spansh)
        private FlowLayoutPanel panelColumns;
        private TextBox txtSpanshPath;
        private Button btnBrowseSpansh;

        // Mapeo de Nombres de Columnas
        private Dictionary<string, string> columnMap = new Dictionary<string, string>
        {
            { "Name", "Body Name (Fixed)" },
            { "Type", "Body Type" },
            { "Atmosphere", "Atmosphere" },
            { "Temperature", "Temperature" },
            { "SurfaceScan", "Scan Status (Icon)" },
            { "Value", "Credits Value" },
            { "Valuable", "High Value (Icon)" },
            { "Terra", "Terraformable (Icon)" },
            { "Geo", "Geological Signals" },
            { "Bio", "Biological Signals" },
            { "Landable", "Landable Status" },
            { "Gravity", "Gravity" },
            { "Materials", "Jumponium Materials" },
            { "FirstDiscovery", "Discovery Status" },
            { "EDSM", "EDSM Data" },
            { "Distance", "Distance (Ls)" }
        };

        public SettingsForm()
        {
            SetupUI();
            LoadSettings();
        }

        private void SetupUI()
        {
            this.Text = "Settings";
            this.Size = new Size(500, 480);
            this.BackColor = Color.FromArgb(30, 30, 35);
            this.ForeColor = Color.White;
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // 1. CREAR EL SISTEMA DE PESTAÑAS
            TabControl tabs = new TabControl();
            tabs.Location = new Point(10, 10);
            tabs.Size = new Size(465, 380);
            this.Controls.Add(tabs);

            // --- PESTAÑA 1: GENERAL ---
            TabPage tabGeneral = new TabPage("General");
            tabGeneral.BackColor = Color.FromArgb(40, 40, 45);
            tabs.TabPages.Add(tabGeneral);

            chkVoice = new CheckBox();
            chkVoice.Text = "Enable Voice Assistant";
            chkVoice.Font = new Font("Arial", 10);
            chkVoice.Location = new Point(20, 30);
            chkVoice.AutoSize = true;
            chkVoice.ForeColor = Color.White;
            tabGeneral.Controls.Add(chkVoice);

            // --- PESTAÑA 2: EDSM ---
            TabPage tabEdsm = new TabPage("EDSM / API");
            tabEdsm.BackColor = Color.FromArgb(40, 40, 45);
            tabs.TabPages.Add(tabEdsm);

            Label lblCmdr = new Label();
            lblCmdr.Text = "CMDR Name:";
            lblCmdr.Location = new Point(20, 30);
            lblCmdr.AutoSize = true;
            lblCmdr.ForeColor = Color.Orange;
            tabEdsm.Controls.Add(lblCmdr);

            txtCmdr = new TextBox();
            txtCmdr.Location = new Point(20, 55);
            txtCmdr.Width = 300;
            tabEdsm.Controls.Add(txtCmdr);

            Label lblApi = new Label();
            lblApi.Text = "EDSM API Key:";
            lblApi.Location = new Point(20, 95);
            lblApi.AutoSize = true;
            lblApi.ForeColor = Color.Orange;
            tabEdsm.Controls.Add(lblApi);

            txtApiKey = new TextBox();
            txtApiKey.Location = new Point(20, 120);
            txtApiKey.Width = 300;
            txtApiKey.PasswordChar = '*';
            tabEdsm.Controls.Add(txtApiKey);

            Label lblNote = new Label();
            lblNote.Text = "Note: You can find your API Key in your EDSM dashboard.";
            lblNote.Location = new Point(20, 160);
            lblNote.AutoSize = true;
            lblNote.Font = new Font("Arial", 8, FontStyle.Italic);
            lblNote.ForeColor = Color.Gray;
            tabEdsm.Controls.Add(lblNote);

            // --- PESTAÑA 3: COLUMNS ---
            TabPage tabCols = new TabPage("Columns");
            tabCols.BackColor = Color.FromArgb(40, 40, 45);
            tabs.TabPages.Add(tabCols);

            Label lblColInfo = new Label();
            lblColInfo.Text = "Visible Columns in System Table:";
            lblColInfo.Location = new Point(10, 10);
            lblColInfo.AutoSize = true;
            lblColInfo.Font = new Font("Arial", 9, FontStyle.Bold);
            lblColInfo.ForeColor = Color.LightGray;
            tabCols.Controls.Add(lblColInfo);

            panelColumns = new FlowLayoutPanel();
            panelColumns.Location = new Point(10, 35);
            panelColumns.Size = new Size(440, 300);
            panelColumns.AutoScroll = true;
            panelColumns.FlowDirection = FlowDirection.TopDown;
            panelColumns.WrapContents = false;
            tabCols.Controls.Add(panelColumns);

            // --- NUEVA PESTAÑA 4: SPANSH ROUTE ---
            TabPage tabSpansh = new TabPage("Spansh Route");
            tabSpansh.BackColor = Color.FromArgb(40, 40, 45);
            tabs.TabPages.Add(tabSpansh);

            Label lblSpansh = new Label();
            lblSpansh.Text = "Spansh CSV File (Neutron Plotter):";
            lblSpansh.Location = new Point(20, 30);
            lblSpansh.AutoSize = true;
            lblSpansh.ForeColor = Color.Orange;
            tabSpansh.Controls.Add(lblSpansh);

            txtSpanshPath = new TextBox();
            txtSpanshPath.Location = new Point(20, 55);
            txtSpanshPath.Width = 340;
            txtSpanshPath.ReadOnly = true; // Solo lectura, usar botón
            tabSpansh.Controls.Add(txtSpanshPath);

            btnBrowseSpansh = new Button();
            btnBrowseSpansh.Text = "...";
            btnBrowseSpansh.Location = new Point(370, 53);
            btnBrowseSpansh.Size = new Size(40, 23);
            btnBrowseSpansh.BackColor = Color.Gray;
            btnBrowseSpansh.ForeColor = Color.White;
            btnBrowseSpansh.FlatStyle = FlatStyle.Flat;
            btnBrowseSpansh.Click += BtnBrowseSpansh_Click;
            tabSpansh.Controls.Add(btnBrowseSpansh);

            Label lblSpanshInfo = new Label();
            lblSpanshInfo.Text = "Load a .csv file generated by Spansh Neutron Plotter.\nThis will populate the 'Spansh Route' tab in the main window.";
            lblSpanshInfo.Location = new Point(20, 90);
            lblSpanshInfo.AutoSize = true;
            lblSpanshInfo.ForeColor = Color.LightGray;
            tabSpansh.Controls.Add(lblSpanshInfo);

            // --- BOTÓN BORRAR CSV (En tabSpansh) ---
            Button btnClearSpansh = new Button();
            btnClearSpansh.Text = "Clear Route";
            btnClearSpansh.Location = new Point(20, 130); // Debajo del label informativo
            btnClearSpansh.Size = new Size(120, 30);
            btnClearSpansh.BackColor = Color.Maroon;
            btnClearSpansh.ForeColor = Color.White;
            btnClearSpansh.FlatStyle = FlatStyle.Flat;
            btnClearSpansh.Click += (s, e) => {
                txtSpanshPath.Text = "";
                // Opcional: Avisar que debe guardar para aplicar
            };
            tabSpansh.Controls.Add(btnClearSpansh);


            // --- BOTONES INFERIORES ---
            btnSave = new Button();
            btnSave.Text = "Save";
            btnSave.Location = new Point(270, 400);
            btnSave.Size = new Size(90, 30);
            btnSave.BackColor = Color.DarkGreen;
            btnSave.ForeColor = Color.White;
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);

            btnCancel = new Button();
            btnCancel.Text = "Cancel";
            btnCancel.Location = new Point(370, 400);
            btnCancel.Size = new Size(90, 30);
            btnCancel.BackColor = Color.FromArgb(60, 60, 60);
            btnCancel.ForeColor = Color.White;
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            this.Controls.Add(btnCancel);
        }

        private void LoadSettings()
        {
            // 1. Cargar General / EDSM
            chkVoice.Checked = Properties.Settings.Default.VoiceEnabled;
            txtCmdr.Text = Properties.Settings.Default.EdsmCmdr;
            txtApiKey.Text = Properties.Settings.Default.EdsmApiKey;

            // 2. Cargar Spansh Path
            txtSpanshPath.Text = Properties.Settings.Default.SpanshCsvPath;

            // 3. Cargar Columnas
            string hiddenString = Properties.Settings.Default.HiddenColumns ?? "";
            List<string> hiddenList = hiddenString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            foreach (var kvp in columnMap)
            {
                CheckBox chk = new CheckBox();
                chk.Text = kvp.Value;
                chk.Tag = kvp.Key;
                chk.AutoSize = true;
                chk.ForeColor = Color.White;
                chk.Margin = new Padding(3, 5, 3, 5);

                if (kvp.Key == "Name")
                {
                    chk.Checked = true;
                    chk.Enabled = false;
                    chk.ForeColor = Color.Gray;
                }
                else
                {
                    chk.Checked = !hiddenList.Contains(kvp.Key);
                }
                panelColumns.Controls.Add(chk);
            }
        }

        private void BtnBrowseSpansh_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                ofd.Title = "Select Spansh Route CSV";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtSpanshPath.Text = ofd.FileName;
                }
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            // 1. Guardar General / EDSM
            Properties.Settings.Default.VoiceEnabled = chkVoice.Checked;
            Properties.Settings.Default.EdsmCmdr = txtCmdr.Text;
            Properties.Settings.Default.EdsmApiKey = txtApiKey.Text;

            // 2. Guardar Spansh Path
            // Si cambió el archivo, podríamos resetear el progreso, pero por ahora solo guardamos la ruta.
            Properties.Settings.Default.SpanshCsvPath = txtSpanshPath.Text;

            // 3. Guardar Columnas
            List<string> hidden = new List<string>();
            foreach (Control c in panelColumns.Controls)
            {
                if (c is CheckBox chk)
                {
                    if (!chk.Checked) hidden.Add(chk.Tag.ToString());
                }
            }
            Properties.Settings.Default.HiddenColumns = string.Join(",", hidden);

            Properties.Settings.Default.Save();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}