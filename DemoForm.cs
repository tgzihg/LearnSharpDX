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
            using (var temp = new MySharpDXForm(showPanel.Handle)) {
            }
        }
    }
}
