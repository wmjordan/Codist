namespace Codist.Options
{
	partial class GeneralPage
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
			this._BottomMarginBox = new System.Windows.Forms.NumericUpDown();
			this._TopMarginBox = new System.Windows.Forms.NumericUpDown();
			this.label2 = new System.Windows.Forms.Label();
			this.label1 = new System.Windows.Forms.Label();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this._NoSpaceBetweenWrappedLinesBox = new System.Windows.Forms.CheckBox();
			this._SaveConfigButton = new System.Windows.Forms.Button();
			this._LoadConfigButton = new System.Windows.Forms.Button();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this._ResetConfigButton = new System.Windows.Forms.Button();
			this._ThemeMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
			this._LightThemeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this._DarkThemeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this._CustomThemeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.groupBox3 = new System.Windows.Forms.GroupBox();
			this._ControlQuickInfoBox = new System.Windows.Forms.CheckBox();
			this._LineNumbersBox = new System.Windows.Forms.CheckBox();
			this._GlobalFeatureBox = new System.Windows.Forms.CheckBox();
			this.label3 = new System.Windows.Forms.Label();
			this._SelectionQuickInfoBox = new System.Windows.Forms.CheckBox();
			((System.ComponentModel.ISupportInitialize)(this._BottomMarginBox)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this._TopMarginBox)).BeginInit();
			this.groupBox1.SuspendLayout();
			this.groupBox2.SuspendLayout();
			this._ThemeMenu.SuspendLayout();
			this.groupBox3.SuspendLayout();
			this.SuspendLayout();
			// 
			// _BottomMarginBox
			// 
			this._BottomMarginBox.Location = new System.Drawing.Point(313, 19);
			this._BottomMarginBox.Name = "_BottomMarginBox";
			this._BottomMarginBox.Size = new System.Drawing.Size(75, 25);
			this._BottomMarginBox.TabIndex = 3;
			// 
			// _TopMarginBox
			// 
			this._TopMarginBox.Location = new System.Drawing.Point(107, 19);
			this._TopMarginBox.Name = "_TopMarginBox";
			this._TopMarginBox.Size = new System.Drawing.Size(75, 25);
			this._TopMarginBox.TabIndex = 1;
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(188, 21);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(119, 15);
			this.label2.TabIndex = 2;
			this.label2.Text = "Bottom margin:";
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(6, 21);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(95, 15);
			this.label1.TabIndex = 0;
			this.label1.Text = "Top margin:";
			// 
			// groupBox1
			// 
			this.groupBox1.Controls.Add(this._NoSpaceBetweenWrappedLinesBox);
			this.groupBox1.Controls.Add(this._BottomMarginBox);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Controls.Add(this._TopMarginBox);
			this.groupBox1.Controls.Add(this.label1);
			this.groupBox1.Location = new System.Drawing.Point(3, 153);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(546, 84);
			this.groupBox1.TabIndex = 3;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Extra line margins";
			// 
			// _NoSpaceBetweenWrappedLinesBox
			// 
			this._NoSpaceBetweenWrappedLinesBox.AutoSize = true;
			this._NoSpaceBetweenWrappedLinesBox.Location = new System.Drawing.Point(9, 50);
			this._NoSpaceBetweenWrappedLinesBox.Name = "_NoSpaceBetweenWrappedLinesBox";
			this._NoSpaceBetweenWrappedLinesBox.Size = new System.Drawing.Size(277, 19);
			this._NoSpaceBetweenWrappedLinesBox.TabIndex = 4;
			this._NoSpaceBetweenWrappedLinesBox.Text = "No margin between wrapped lines";
			this._NoSpaceBetweenWrappedLinesBox.UseVisualStyleBackColor = true;
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
			this.groupBox2.Location = new System.Drawing.Point(3, 81);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(546, 66);
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
			// groupBox3
			// 
			this.groupBox3.Controls.Add(this._SelectionQuickInfoBox);
			this.groupBox3.Controls.Add(this._ControlQuickInfoBox);
			this.groupBox3.Controls.Add(this._LineNumbersBox);
			this.groupBox3.Location = new System.Drawing.Point(3, 243);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Size = new System.Drawing.Size(546, 82);
			this.groupBox3.TabIndex = 4;
			this.groupBox3.TabStop = false;
			this.groupBox3.Text = "Misc";
			// 
			// _ControlQuickInfoBox
			// 
			this._ControlQuickInfoBox.AutoSize = true;
			this._ControlQuickInfoBox.Location = new System.Drawing.Point(9, 49);
			this._ControlQuickInfoBox.Name = "_ControlQuickInfoBox";
			this._ControlQuickInfoBox.Size = new System.Drawing.Size(333, 19);
			this._ControlQuickInfoBox.TabIndex = 1;
			this._ControlQuickInfoBox.Text = "Hide quick info until Shift is pressed";
			this._ControlQuickInfoBox.UseVisualStyleBackColor = true;
			// 
			// _LineNumbersBox
			// 
			this._LineNumbersBox.AutoSize = true;
			this._LineNumbersBox.Location = new System.Drawing.Point(9, 24);
			this._LineNumbersBox.Name = "_LineNumbersBox";
			this._LineNumbersBox.Size = new System.Drawing.Size(325, 19);
			this._LineNumbersBox.TabIndex = 0;
			this._LineNumbersBox.Text = "Draw line numbers on editor scrollbar";
			this._LineNumbersBox.UseVisualStyleBackColor = true;
			// 
			// _GlobalFeatureBox
			// 
			this._GlobalFeatureBox.AutoSize = true;
			this._GlobalFeatureBox.Location = new System.Drawing.Point(12, 3);
			this._GlobalFeatureBox.Name = "_GlobalFeatureBox";
			this._GlobalFeatureBox.Size = new System.Drawing.Size(133, 19);
			this._GlobalFeatureBox.TabIndex = 0;
			this._GlobalFeatureBox.Text = "Enable Codist";
			this._GlobalFeatureBox.UseVisualStyleBackColor = true;
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(30, 48);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(519, 30);
			this.label3.TabIndex = 1;
			this.label3.Text = "(It takes effect on new document windows.\r\nYou can disable Codist to save power w" +
    "hen running with battery.)";
			// 
			// _SelectionQuickInfoBox
			// 
			this._SelectionQuickInfoBox.AutoSize = true;
			this._SelectionQuickInfoBox.Location = new System.Drawing.Point(348, 49);
			this._SelectionQuickInfoBox.Name = "_SelectionQuickInfoBox";
			this._SelectionQuickInfoBox.Size = new System.Drawing.Size(189, 19);
			this._SelectionQuickInfoBox.TabIndex = 2;
			this._SelectionQuickInfoBox.Text = "Selection Quick Info";
			this._SelectionQuickInfoBox.UseVisualStyleBackColor = true;
			// 
			// GeneralPage
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this.label3);
			this.Controls.Add(this._GlobalFeatureBox);
			this.Controls.Add(this.groupBox3);
			this.Controls.Add(this.groupBox2);
			this.Controls.Add(this.groupBox1);
			this.Name = "GeneralPage";
			this.Size = new System.Drawing.Size(575, 328);
			this.Load += new System.EventHandler(this.MiscPage_Load);
			((System.ComponentModel.ISupportInitialize)(this._BottomMarginBox)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this._TopMarginBox)).EndInit();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.groupBox2.ResumeLayout(false);
			this._ThemeMenu.ResumeLayout(false);
			this.groupBox3.ResumeLayout(false);
			this.groupBox3.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.NumericUpDown _BottomMarginBox;
		private System.Windows.Forms.NumericUpDown _TopMarginBox;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.CheckBox _NoSpaceBetweenWrappedLinesBox;
		private System.Windows.Forms.Button _SaveConfigButton;
		private System.Windows.Forms.Button _LoadConfigButton;
		private System.Windows.Forms.GroupBox groupBox2;
		private System.Windows.Forms.ContextMenuStrip _ThemeMenu;
		private System.Windows.Forms.ToolStripMenuItem _LightThemeMenuItem;
		private System.Windows.Forms.ToolStripMenuItem _DarkThemeMenuItem;
		private System.Windows.Forms.ToolStripMenuItem _CustomThemeMenuItem;
		private System.Windows.Forms.Button _ResetConfigButton;
		private System.Windows.Forms.GroupBox groupBox3;
		private System.Windows.Forms.CheckBox _LineNumbersBox;
		private System.Windows.Forms.CheckBox _ControlQuickInfoBox;
		private System.Windows.Forms.CheckBox _GlobalFeatureBox;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.CheckBox _SelectionQuickInfoBox;
	}
}
