using SharpDX.Windows;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace deleteSharpDX {
    public partial class DemoForm : Form {
        public DemoForm() {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e) {
            var showForm = new RenderForm();
            showForm.TopLevel = false;
            showForm.FormBorderStyle = FormBorderStyle.None;
            showForm.ClientSize = new System.Drawing.Size(showPanel.ClientSize.Width, showPanel.ClientSize.Height);
            showPanel.Controls.Add(showForm);
            using (var temp = new MySharpDXForm(showForm)) {
            }
        }
    }
}
