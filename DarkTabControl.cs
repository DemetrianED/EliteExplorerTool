using System;
using System.Drawing;
using System.Windows.Forms;

namespace EliteExplorerTool
{
    // Heredamos de TabControl para mejorarlo
    public class DarkTabControl : TabControl
    {
        public DarkTabControl()
        {
            // Activamos el pintado manual para tener control total
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.DrawMode = TabDrawMode.OwnerDrawFixed;
            this.SizeMode = TabSizeMode.Fixed;
            this.ItemSize = new Size(120, 30); // Tamaño estándar de pestaña
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // 1. PINTAR EL FONDO (El área vacía que antes salía blanca)
            using (SolidBrush backBrush = new SolidBrush(Color.FromArgb(10, 10, 15)))
            {
                e.Graphics.FillRectangle(backBrush, this.ClientRectangle);
            }

            // 2. PINTAR CADA PESTAÑA
            for (int i = 0; i < this.TabCount; i++)
            {
                Rectangle tabRect = this.GetTabRect(i);
                bool isSelected = (this.SelectedIndex == i);

                // Colores para Activo vs Inactivo
                // Activo: Fondo un poco más claro, Texto Naranja
                // Inactivo: Fondo oscuro, Texto Gris
                Brush bgBrush = isSelected ? new SolidBrush(Color.FromArgb(40, 40, 45)) : new SolidBrush(Color.FromArgb(20, 20, 25));
                Brush textBrush = isSelected ? new SolidBrush(Color.Orange) : new SolidBrush(Color.Gray);

                // Pintar el rectángulo de la pestaña
                e.Graphics.FillRectangle(bgBrush, tabRect);

                // Pintar el texto centrado
                string tabText = this.TabPages[i].Text;
                StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };

                e.Graphics.DrawString(tabText, this.Font, textBrush, tabRect, sf);

                // Limpieza de memoria gráfica
                bgBrush.Dispose();
                textBrush.Dispose();
            }
        }
    }
}