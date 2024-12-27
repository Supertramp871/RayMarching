namespace Shadertoy
{
    partial class ShaderForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.txtInputShader = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // txtInputShader
            // 
            this.txtInputShader.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtInputShader.Location = new System.Drawing.Point(39, 27);
            this.txtInputShader.Multiline = true;
            this.txtInputShader.Name = "txtInputShader";
            this.txtInputShader.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtInputShader.Size = new System.Drawing.Size(491, 361);
            this.txtInputShader.TabIndex = 0;
            this.txtInputShader.WordWrap = false;
            // 
            // ShaderForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(690, 449);
            this.ControlBox = false;
            this.Controls.Add(this.txtInputShader);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ShaderForm";
            this.ShowIcon = false;
            this.Text = "Fragment Shader";
            this.Load += new System.EventHandler(this.ShaderForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtInputShader;
    }
}

