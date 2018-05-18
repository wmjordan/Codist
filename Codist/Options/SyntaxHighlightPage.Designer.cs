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
			this.components = new System.ComponentModel.Container();
			this._SaveConfigButton = new System.Windows.Forms.Button();
			this._LoadConfigButton = new System.Windows.Forms.Button();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this._ResetConfigButton = new System.Windows.Forms.Button();
			this._ThemeMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
			this._LightThemeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this._DarkThemeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this._CustomThemeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.groupBox2.SuspendLayout();
			this._ThemeMenu.SuspendLayout();
			this.SuspendLayout();
			// 
			// _SaveConfigButton
			// 
			this._SaveConfigButton.Location = new System.Drawing.Point(125, 24);
			this._SaveConfigButton.Name = "_SaveConfigButton";
			this._SaveConfigButton.Size = new System.Drawing.Size(111, 23);
			this._SaveConfigButton.TabIndex = 0;
			this._SaveConfigButton.Text = "&Save...";
			this._SaveConfigButton.UseVisualStyleBackColor = true;
			// 
			// _LoadConfigButton
			// 
			this._LoadConfigButton.Location = new System.Drawing.Point(9, 24);
			this._LoadConfigButton.Name = "_LoadConfigButton";
			this._LoadConfigButton.Size = new System.Drawing.Size(110, 23);
			this._LoadConfigButton.TabIndex = 1;
			this._LoadConfigButton.Text = "&Load...";
			this._LoadConfigButton.UseVisualStyleBackColor = true;
			// 
			// groupBox2
			// 
			this.groupBox2.Controls.Add(this._ResetConfigButton);
			this.groupBox2.Controls.Add(this._SaveConfigButton);
			this.groupBox2.Controls.Add(this._LoadConfigButton);
			this.groupBox2.Location = new System.Drawing.Point(15, 6);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(378, 66);
			this.groupBox2.TabIndex = 2;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "Super syntax highlight settings";
			// 
			// _ResetConfigButton
			// 
			this._ResetConfigButton.Location = new System.Drawing.Point(242, 24);
			this._ResetConfigButton.Name = "_ResetConfigButton";
			this._ResetConfigButton.Size = new System.Drawing.Size(111, 23);
			this._ResetConfigButton.TabIndex = 2;
			this._ResetConfigButton.Text = "Reset...";
			this._ResetConfigButton.UseVisualStyleBackColor = true;
			// 
			// _ThemeMenu
			// 
			this._ThemeMenu.ImageScalingSize = new System.Drawing.Size(20, 20);
			this._ThemeMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._LightThemeMenuItem,
            this._DarkThemeMenuItem,
            this._CustomThemeMenuItem});
			this._ThemeMenu.Name = "_ThemeMenu";
			this._ThemeMenu.Size = new System.Drawing.Size(256, 76);
			// 
			// _LightThemeMenuItem
			// 
			this._LightThemeMenuItem.Name = "_LightThemeMenuItem";
			this._LightThemeMenuItem.Size = new System.Drawing.Size(255, 24);
			this._LightThemeMenuItem.Tag = "Light";
			this._LightThemeMenuItem.Text = "&Light theme";
			// 
			// _DarkThemeMenuItem
			// 
			this._DarkThemeMenuItem.Name = "_DarkThemeMenuItem";
			this._DarkThemeMenuItem.Size = new System.Drawing.Size(255, 24);
			this._DarkThemeMenuItem.Tag = "Dark";
			this._DarkThemeMenuItem.Text = "&Dark theme";
			// 
			// _CustomThemeMenuItem
			// 
			this._CustomThemeMenuItem.Name = "_CustomThemeMenuItem";
			this._CustomThemeMenuItem.Size = new System.Drawing.Size(255, 24);
			this._CustomThemeMenuItem.Text = "&Custom configurations...";
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
			this._ThemeMenu.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion
		private System.Windows.Forms.Button _SaveConfigButton;
		private System.Windows.Forms.Button _LoadConfigButton;
		private System.Windows.Forms.GroupBox groupBox2;
		private System.Windows.Forms.ContextMenuStrip _ThemeMenu;
		private System.Windows.Forms.ToolStripMenuItem _LightThemeMenuItem;
		private System.Windows.Forms.ToolStripMenuItem _DarkThemeMenuItem;
		private System.Windows.Forms.ToolStripMenuItem _CustomThemeMenuItem;
		private System.Windows.Forms.Button _ResetConfigButton;
	}
}
