namespace Codist.Options
{
	partial class SyntaxHighlightPage
	{
		/// <summary> 
		/// 必需的设计器变量。
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary> 
		/// 清理所有正在使用的资源。
		/// </summary>
		/// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
		protected override void Dispose(bool disposing) {
			if (disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region 组件设计器生成的代码

		/// <summary> 
		/// 设计器支持所需的方法 - 不要修改
		/// 使用代码编辑器修改此方法的内容。
		/// </summary>
		private void InitializeComponent() {
			this._LightThemeButton = new System.Windows.Forms.Button();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.label1 = new System.Windows.Forms.Label();
			this._ResetThemeButton = new System.Windows.Forms.Button();
			this._DarkThemeButton = new System.Windows.Forms.Button();
			this.groupBox2.SuspendLayout();
			this.SuspendLayout();
			// 
			// _LightThemeButton
			// 
			this._LightThemeButton.Location = new System.Drawing.Point(9, 24);
			this._LightThemeButton.Name = "_LightThemeButton";
			this._LightThemeButton.Size = new System.Drawing.Size(110, 23);
			this._LightThemeButton.TabIndex = 1;
			this._LightThemeButton.Text = "&Light theme";
			this._LightThemeButton.UseVisualStyleBackColor = true;
			// 
			// groupBox2
			// 
			this.groupBox2.Controls.Add(this.label1);
			this.groupBox2.Controls.Add(this._ResetThemeButton);
			this.groupBox2.Controls.Add(this._DarkThemeButton);
			this.groupBox2.Controls.Add(this._LightThemeButton);
			this.groupBox2.Location = new System.Drawing.Point(15, 6);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(464, 146);
			this.groupBox2.TabIndex = 2;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "Super syntax highlight presets";
			// 
			// label1
			// 
			this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.label1.Location = new System.Drawing.Point(6, 61);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(452, 69);
			this.label1.TabIndex = 3;
			this.label1.Text = "Tip: you can quickly load or reset syntax theme by pressing the buttons above.\r\nO" +
    "pen a C# code file to see effects immediately.";
			// 
			// _ResetThemeButton
			// 
			this._ResetThemeButton.Location = new System.Drawing.Point(242, 24);
			this._ResetThemeButton.Name = "_ResetThemeButton";
			this._ResetThemeButton.Size = new System.Drawing.Size(111, 23);
			this._ResetThemeButton.TabIndex = 2;
			this._ResetThemeButton.Text = "Reset...";
			this._ResetThemeButton.UseVisualStyleBackColor = true;
			// 
			// _DarkThemeButton
			// 
			this._DarkThemeButton.Location = new System.Drawing.Point(125, 24);
			this._DarkThemeButton.Name = "_DarkThemeButton";
			this._DarkThemeButton.Size = new System.Drawing.Size(111, 23);
			this._DarkThemeButton.TabIndex = 0;
			this._DarkThemeButton.Text = "&Dark theme";
			this._DarkThemeButton.UseVisualStyleBackColor = true;
			// 
			// SyntaxHighlightPage
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this.groupBox2);
			this.Name = "SyntaxHighlightPage";
			this.Size = new System.Drawing.Size(575, 328);
			this.Load += new System.EventHandler(this.SyntaxHighlightPage_Load);
			this.groupBox2.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion
		private System.Windows.Forms.Button _LightThemeButton;
		private System.Windows.Forms.GroupBox groupBox2;
		private System.Windows.Forms.Button _ResetThemeButton;
		private System.Windows.Forms.Button _DarkThemeButton;
		private System.Windows.Forms.Label label1;
	}
}
