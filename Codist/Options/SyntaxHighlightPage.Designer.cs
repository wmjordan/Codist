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
			this._SyntaxHighlightTabs = new System.Windows.Forms.TabControl();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.groupBox2 = new Codist.Controls.CustomGroupBox();
			this.label1 = new System.Windows.Forms.Label();
			this._FontFamilyBox = new System.Windows.Forms.CheckBox();
			this._ColorBox = new System.Windows.Forms.CheckBox();
			this._FontSizeBox = new System.Windows.Forms.CheckBox();
			this._FontStyleBox = new System.Windows.Forms.CheckBox();
			this.titleLabel2 = new Codist.Controls.TitleLabel();
			this.titleLabel1 = new Codist.Controls.TitleLabel();
			this._LoadButton = new System.Windows.Forms.Button();
			this._SaveButton = new System.Windows.Forms.Button();
			this._ResetThemeButton = new System.Windows.Forms.Button();
			this._SimpleThemeButton = new System.Windows.Forms.Button();
			this._DarkThemeButton = new System.Windows.Forms.Button();
			this._LightThemeButton = new System.Windows.Forms.Button();
			this._SyntaxHighlightTabs.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.groupBox2.SuspendLayout();
			this.SuspendLayout();
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
			this.tabPage2.Controls.Add(this.groupBox2);
			this.tabPage2.Location = new System.Drawing.Point(4, 25);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(561, 293);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "General";
			this.tabPage2.UseVisualStyleBackColor = true;
			// 
			// groupBox2
			// 
			this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.groupBox2.Controls.Add(this.label1);
			this.groupBox2.Controls.Add(this._FontFamilyBox);
			this.groupBox2.Controls.Add(this._ColorBox);
			this.groupBox2.Controls.Add(this._FontSizeBox);
			this.groupBox2.Controls.Add(this._FontStyleBox);
			this.groupBox2.Controls.Add(this.titleLabel2);
			this.groupBox2.Controls.Add(this.titleLabel1);
			this.groupBox2.Controls.Add(this._LoadButton);
			this.groupBox2.Controls.Add(this._SaveButton);
			this.groupBox2.Controls.Add(this._ResetThemeButton);
			this.groupBox2.Controls.Add(this._SimpleThemeButton);
			this.groupBox2.Controls.Add(this._DarkThemeButton);
			this.groupBox2.Controls.Add(this._LightThemeButton);
			this.groupBox2.Location = new System.Drawing.Point(6, 6);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(549, 207);
			this.groupBox2.TabIndex = 2;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "Manage Syntax Highlight Themes";
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point(6, 154);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(537, 50);
			this.label1.TabIndex = 12;
			this.label1.Text = "1. Press one of the above button to quickly setup syntax highlight styles.\r\n2. Op" +
    "en a document to see highlight effects";
			// 
			// _FontFamilyBox
			// 
			this._FontFamilyBox.AutoSize = true;
			this._FontFamilyBox.Location = new System.Drawing.Point(357, 78);
			this._FontFamilyBox.Name = "_FontFamilyBox";
			this._FontFamilyBox.Size = new System.Drawing.Size(117, 19);
			this._FontFamilyBox.TabIndex = 11;
			this._FontFamilyBox.Text = "Font family";
			this._FontFamilyBox.UseVisualStyleBackColor = true;
			// 
			// _ColorBox
			// 
			this._ColorBox.AutoSize = true;
			this._ColorBox.Location = new System.Drawing.Point(9, 78);
			this._ColorBox.Name = "_ColorBox";
			this._ColorBox.Size = new System.Drawing.Size(69, 19);
			this._ColorBox.TabIndex = 11;
			this._ColorBox.Text = "Color";
			this._ColorBox.UseVisualStyleBackColor = true;
			// 
			// _FontSizeBox
			// 
			this._FontSizeBox.AutoSize = true;
			this._FontSizeBox.Location = new System.Drawing.Point(242, 78);
			this._FontSizeBox.Name = "_FontSizeBox";
			this._FontSizeBox.Size = new System.Drawing.Size(101, 19);
			this._FontSizeBox.TabIndex = 11;
			this._FontSizeBox.Text = "Font size";
			this._FontSizeBox.UseVisualStyleBackColor = true;
			// 
			// _FontStyleBox
			// 
			this._FontStyleBox.AutoSize = true;
			this._FontStyleBox.Location = new System.Drawing.Point(125, 78);
			this._FontStyleBox.Name = "_FontStyleBox";
			this._FontStyleBox.Size = new System.Drawing.Size(109, 19);
			this._FontStyleBox.TabIndex = 11;
			this._FontStyleBox.Text = "Font style";
			this._FontStyleBox.UseVisualStyleBackColor = true;
			// 
			// titleLabel2
			// 
			this.titleLabel2.Location = new System.Drawing.Point(6, 60);
			this.titleLabel2.Name = "titleLabel2";
			this.titleLabel2.Size = new System.Drawing.Size(371, 15);
			this.titleLabel2.TabIndex = 10;
			this.titleLabel2.Text = "Load following parts when importing themes";
			// 
			// titleLabel1
			// 
			this.titleLabel1.Location = new System.Drawing.Point(6, 109);
			this.titleLabel1.Name = "titleLabel1";
			this.titleLabel1.Size = new System.Drawing.Size(371, 15);
			this.titleLabel1.TabIndex = 9;
			this.titleLabel1.Text = "Import predefined theme";
			// 
			// _LoadButton
			// 
			this._LoadButton.Location = new System.Drawing.Point(9, 24);
			this._LoadButton.Name = "_LoadButton";
			this._LoadButton.Size = new System.Drawing.Size(111, 23);
			this._LoadButton.TabIndex = 8;
			this._LoadButton.Text = "Load...";
			this._LoadButton.UseVisualStyleBackColor = true;
			// 
			// _SaveButton
			// 
			this._SaveButton.Location = new System.Drawing.Point(125, 24);
			this._SaveButton.Name = "_SaveButton";
			this._SaveButton.Size = new System.Drawing.Size(111, 23);
			this._SaveButton.TabIndex = 8;
			this._SaveButton.Text = "Save...";
			this._SaveButton.UseVisualStyleBackColor = true;
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
			// _SimpleThemeButton
			// 
			this._SimpleThemeButton.Location = new System.Drawing.Point(242, 128);
			this._SimpleThemeButton.Name = "_SimpleThemeButton";
			this._SimpleThemeButton.Size = new System.Drawing.Size(111, 23);
			this._SimpleThemeButton.TabIndex = 0;
			this._SimpleThemeButton.Text = "&Simple theme";
			this._SimpleThemeButton.UseVisualStyleBackColor = true;
			// 
			// _DarkThemeButton
			// 
			this._DarkThemeButton.Location = new System.Drawing.Point(125, 128);
			this._DarkThemeButton.Name = "_DarkThemeButton";
			this._DarkThemeButton.Size = new System.Drawing.Size(111, 23);
			this._DarkThemeButton.TabIndex = 0;
			this._DarkThemeButton.Text = "&Dark theme";
			this._DarkThemeButton.UseVisualStyleBackColor = true;
			// 
			// _LightThemeButton
			// 
			this._LightThemeButton.Location = new System.Drawing.Point(8, 128);
			this._LightThemeButton.Name = "_LightThemeButton";
			this._LightThemeButton.Size = new System.Drawing.Size(111, 23);
			this._LightThemeButton.TabIndex = 1;
			this._LightThemeButton.Text = "&Light theme";
			this._LightThemeButton.UseVisualStyleBackColor = true;
			// 
			// SyntaxHighlightPage
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this._SyntaxHighlightTabs);
			this.Name = "SyntaxHighlightPage";
			this.Size = new System.Drawing.Size(575, 328);
			this.Load += new System.EventHandler(this.SyntaxHighlightPage_Load);
			this._SyntaxHighlightTabs.ResumeLayout(false);
			this.tabPage2.ResumeLayout(false);
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion
		private System.Windows.Forms.Button _LightThemeButton;
		private Codist.Controls.CustomGroupBox groupBox2;
		private System.Windows.Forms.Button _ResetThemeButton;
		private System.Windows.Forms.Button _DarkThemeButton;
		private System.Windows.Forms.TabControl _SyntaxHighlightTabs;
		private System.Windows.Forms.TabPage tabPage2;
		private System.Windows.Forms.Button _SimpleThemeButton;
		private System.Windows.Forms.Button _SaveButton;
		private System.Windows.Forms.Button _LoadButton;
		private Controls.TitleLabel titleLabel1;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.CheckBox _FontFamilyBox;
		private System.Windows.Forms.CheckBox _ColorBox;
		private System.Windows.Forms.CheckBox _FontSizeBox;
		private System.Windows.Forms.CheckBox _FontStyleBox;
		private Controls.TitleLabel titleLabel2;
	}
}
