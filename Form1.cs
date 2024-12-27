using System.Windows.Forms;
using System.Runtime.InteropServices;
using System;

namespace Shadertoy
{
    public partial class ShaderForm : Form
    {


        public ShaderForm()
        {
            InitializeComponent();
            this.txtInputShader.Text = System.IO.File.ReadAllText("..\\..\\SWater.glsl");
            /////
            RunFragmentShader();
        }

        private void RunFragmentShader()
        {
            ShaderWindow.FragmentShaderSource = this.txtInputShader.Lines;
            ShaderWindow.RunFragmentShader();
        }

        private void ShaderForm_Load(object sender, EventArgs e)
        {

        }
    }
}
