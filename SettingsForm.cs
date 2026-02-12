using System;
using System.Drawing;
using System.Windows.Forms;

namespace EliteExplorerTool
{
    public class SettingsForm : Form
    {
        // Controles
        private CheckBox chkVoice;
        private TextBox txtCmdr;
        private TextBox txtApiKey;
        private Button btnSave;
        private Button btnCancel;

        public SettingsForm()
        {
            SetupUI();
            LoadSettings(); // Cargar datos guardados al abrir
        }

        private void SetupUI()
        {
            this.Text = "Settings";
            this.Size = new Size(500, 400);
            this.BackColor = Color.FromArgb(30, 30, 35);
            this.ForeColor = Color.White;
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // 1. CREAR EL SISTEMA DE PESTAÑAS
            TabControl tabs = new TabControl();
            tabs.Location = new Point(10, 10);
            tabs.Size = new Size(465, 300);
            this.Controls.Add(tabs);

            // --- PESTAÑA 1: GENERAL ---
            TabPage tabGeneral = new TabPage("General");
            tabGeneral.BackColor = Color.FromArgb(40, 40, 45);
            tabs.TabPages.Add(tabGeneral);

            // Checkbox Voz
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

            // Label CMDR
            Label lblCmdr = new Label();
            lblCmdr.Text = "CMDR Name:";
            lblCmdr.Location = new Point(20, 30);
            lblCmdr.AutoSize = true;
            lblCmdr.ForeColor = Color.Orange;
            tabEdsm.Controls.Add(lblCmdr);

            // Text CMDR
            txtCmdr = new TextBox();
            txtCmdr.Location = new Point(20, 55);
            txtCmdr.Width = 300;
            tabEdsm.Controls.Add(txtCmdr);

            // Label API Key
            Label lblApi = new Label();
            lblApi.Text = "EDSM API Key:";
            lblApi.Location = new Point(20, 95);
            lblApi.AutoSize = true;
            lblApi.ForeColor = Color.Orange;
            tabEdsm.Controls.Add(lblApi);

            // Text API Key
            txtApiKey = new TextBox();
            txtApiKey.Location = new Point(20, 120);
            txtApiKey.Width = 300;
            txtApiKey.PasswordChar = '*'; // Ocultar caracteres por seguridad
            tabEdsm.Controls.Add(txtApiKey);

            // Nota EDSM
            Label lblNote = new Label();
            lblNote.Text = "Note: You can find your API Key in your EDSM dashboard.";
            lblNote.Location = new Point(20, 160);
            lblNote.AutoSize = true;
            lblNote.Font = new Font("Arial", 8, FontStyle.Italic);
            lblNote.ForeColor = Color.Gray;
            tabEdsm.Controls.Add(lblNote);


            // --- BOTONES INFERIORES ---
            btnSave = new Button();
            btnSave.Text = "Save";
            btnSave.Location = new Point(270, 320);
            btnSave.Size = new Size(90, 30);
            btnSave.BackColor = Color.DarkGreen;
            btnSave.ForeColor = Color.White;
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);

            btnCancel = new Button();
            btnCancel.Text = "Cancel";
            btnCancel.Location = new Point(370, 320);
            btnCancel.Size = new Size(90, 30);
            btnCancel.BackColor = Color.FromArgb(60, 60, 60);
            btnCancel.ForeColor = Color.White;
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.Click += (s, e) => { this.Close(); };
            this.Controls.Add(btnCancel);
        }

        private void LoadSettings()
        {
            // Leemos de la memoria (Properties.Settings) y rellenamos los campos
            chkVoice.Checked = Properties.Settings.Default.VoiceEnabled;
            txtCmdr.Text = Properties.Settings.Default.EdsmCmdr;
            txtApiKey.Text = Properties.Settings.Default.EdsmApiKey;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            // Guardamos los campos en la memoria
            Properties.Settings.Default.VoiceEnabled = chkVoice.Checked;
            Properties.Settings.Default.EdsmCmdr = txtCmdr.Text;
            Properties.Settings.Default.EdsmApiKey = txtApiKey.Text;

            // Confirmamos el guardado en disco
            Properties.Settings.Default.Save();

            MessageBox.Show("Settings saved successfully!", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.Close();
        }
    }
}