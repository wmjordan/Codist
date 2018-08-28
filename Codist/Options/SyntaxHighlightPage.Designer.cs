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
			this._SimpleThemeButton = new System.Windows.Forms.Button();
			this._DarkThemeButton = new System.Windows.Forms.Button();
			this._SyntaxHighlightTabs = new System.Windows.Forms.TabControl();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this._HighlightSpecialCommentBox = new System.Windows.Forms.CheckBox();
			this.groupBox2.SuspendLayout();
			this._SyntaxHighlightTabs.SuspendLayout();
			this.tabPage2.SuspendLayout();
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
			this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.groupBox2.Controls.Add(this.label1);
			this.groupBox2.Controls.Add(this._ResetThemeButton);
			this.groupBox2.Controls.Add(this._SimpleThemeButton);
			this.groupBox2.Controls.Add(this._DarkThemeButton);
			this.groupBox2.Controls.Add(this._LightThemeButton);
			this.groupBox2.Location = new System.Drawing.Point(6, 6);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(549, 132);
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
			this.label1.Size = new System.Drawing.Size(537, 55);
			this.label1.TabIndex = 3;
			this.label1.Text = "Tip: you can quickly load or reset syntax theme by pressing the buttons above.\r\nO" +
    "pen a C# code file to see effects immediately.";
			// 
			// _ResetThemeButton
			// 
			this._ResetThemeButton.Location = new System.Drawing.Point(373, 24);
			this._ResetThemeButton.Name = "_ResetThemeButton";
			this._ResetThemeButton.Size = new System.Drawing.Size(111, 23);
			this._ResetThemeButton.TabIndex = 2;
			this._ResetThemeButton.Text = "Reset...";
			this._ResetThemeButton.UseVisualStyleBackColor = true;
			// 
			// _SimpleThemeButton
			// 
			this._SimpleThemeButton.Location = new System.Drawing.Point(242, 24);
			this._SimpleThemeButton.Name = "_SimpleThemeButton";
			this._SimpleThemeButton.Size = new System.Drawing.Size(111, 23);
			this._SimpleThemeButton.TabIndex = 0;
			this._SimpleThemeButton.Text = "&Simple theme";
			this._SimpleThemeButton.UseVisualStyleBackColor = true;
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
			// _SyntaxHighlightTabs
			// 
			this._SyntaxHighlightTabs.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this._SyntaxHighlightTabs.Controls.Add(this.tabPage2);
			this._SyntaxHighlightTabs.Location = new System.Drawing.Point(3, 3);
			this._SyntaxHighlightTabs.Name = "_SyntaxHighlightTabs";
			this._SyntaxHighlightTabs.SelectedIndex = 0;
			this._SyntaxHighlightTabs.Size = new System.Drawing.Size(569, 322);
			this._SyntaxHighlightTabs.TabIndex = 4;
			// 
			// tabPage2
			// 
			this.tabPage2.Controls.Add(this._HighlightSpecialCommentBox);
			this.tabPage2.Controls.Add(this.groupBox2);
			this.tabPage2.Location = new System.Drawing.Point(4, 25);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(561, 293);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "General";
			this.tabPage2.UseVisualStyleBackColor = true;
			// 
			// _HighlightSpecialCommentBox
			// 
			this._HighlightSpecialCommentBox.AutoSize = true;
			this._HighlightSpecialCommentBox.Location = new System.Drawing.Point(15, 144);
			this._HighlightSpecialCommentBox.Name = "_HighlightSpecialCommentBox";
			this._HighlightSpecialCommentBox.Size = new System.Drawing.Size(189, 19);
			this._HighlightSpecialCommentBox.TabIndex = 7;
			this._HighlightSpecialCommentBox.Text = "Tag special comments";
			this._HighlightSpecialCommentBox.UseVisualStyleBackColor = true;
			// 
			// SyntaxHighlightPage
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this._SyntaxHighlightTabs);
			this.Name = "SyntaxHighlightPage";
			this.Size = new System.Drawing.Size(575, 328);
			this.Load += new System.EventHandler(this.SyntaxHighlightPage_Load);
			this.groupBox2.ResumeLayout(false);
			this._SyntaxHighlightTabs.ResumeLayout(false);
			this.tabPage2.ResumeLayout(false);
			this.tabPage2.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion
		private System.Windows.Forms.Button _LightThemeButton;
		private System.Windows.Forms.GroupBox groupBox2;
		private System.Windows.Forms.Button _ResetThemeButton;
		private System.Windows.Forms.Button _DarkThemeButton;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.TabControl _SyntaxHighlightTabs;
		private System.Windows.Forms.TabPage tabPage2;
		private System.Windows.Forms.Button _SimpleThemeButton;
		private System.Windows.Forms.CheckBox _HighlightSpecialCommentBox;
	}
}
