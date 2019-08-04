namespace Codist.Options
{
	partial class SmartBarPage
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
			this._ToggleSmartBarBox = new System.Windows.Forms.CheckBox();
			this._SuperQuickInfoTabs = new System.Windows.Forms.TabControl();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this._AutoShowSmartBarBox = new System.Windows.Forms.CheckBox();
			this._SearchPage = new System.Windows.Forms.TabPage();
			this._SaveButton = new System.Windows.Forms.Button();
			this._UrlBox = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this._NameBox = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this._ResetButton = new System.Windows.Forms.Button();
			this._MoveUpButton = new System.Windows.Forms.Button();
			this._RemoveButton = new System.Windows.Forms.Button();
			this._AddButton = new System.Windows.Forms.Button();
			this._SearchEngineBox = new System.Windows.Forms.ListView();
			this._NameColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this._UrlColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.label5 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this._BrowseBrowserButton = new System.Windows.Forms.Button();
			this._BrowserParameterBox = new System.Windows.Forms.TextBox();
			this._BrowserPathBox = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this._BrowseBrowserDialog = new System.Windows.Forms.OpenFileDialog();
			this._SuperQuickInfoTabs.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this._SearchPage.SuspendLayout();
			this.SuspendLayout();
			// 
			// _ToggleSmartBarBox
			// 
			this._ToggleSmartBarBox.AutoSize = true;
			this._ToggleSmartBarBox.CheckAlign = System.Drawing.ContentAlignment.TopLeft;
			this._ToggleSmartBarBox.Location = new System.Drawing.Point(15, 31);
			this._ToggleSmartBarBox.Name = "_ToggleSmartBarBox";
			this._ToggleSmartBarBox.Size = new System.Drawing.Size(349, 34);
			this._ToggleSmartBarBox.TabIndex = 1;
			this._ToggleSmartBarBox.Text = "Show/hide with Shift key\r\n* Double tap to show, single tap to hide";
			this._ToggleSmartBarBox.UseVisualStyleBackColor = true;
			// 
			// _SuperQuickInfoTabs
			// 
			this._SuperQuickInfoTabs.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this._SuperQuickInfoTabs.Controls.Add(this.tabPage2);
			this._SuperQuickInfoTabs.Controls.Add(this._SearchPage);
			this._SuperQuickInfoTabs.Location = new System.Drawing.Point(3, 3);
			this._SuperQuickInfoTabs.Name = "_SuperQuickInfoTabs";
			this._SuperQuickInfoTabs.SelectedIndex = 0;
			this._SuperQuickInfoTabs.Size = new System.Drawing.Size(569, 322);
			this._SuperQuickInfoTabs.TabIndex = 0;
			// 
			// tabPage2
			// 
			this.tabPage2.Controls.Add(this._AutoShowSmartBarBox);
			this.tabPage2.Controls.Add(this._ToggleSmartBarBox);
			this.tabPage2.Location = new System.Drawing.Point(4, 25);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(561, 293);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "General";
			this.tabPage2.UseVisualStyleBackColor = true;
			// 
			// _AutoShowSmartBarBox
			// 
			this._AutoShowSmartBarBox.AutoSize = true;
			this._AutoShowSmartBarBox.Location = new System.Drawing.Point(15, 6);
			this._AutoShowSmartBarBox.Name = "_AutoShowSmartBarBox";
			this._AutoShowSmartBarBox.Size = new System.Drawing.Size(181, 19);
			this._AutoShowSmartBarBox.TabIndex = 2;
			this._AutoShowSmartBarBox.Text = "Show upon selection";
			this._AutoShowSmartBarBox.UseVisualStyleBackColor = true;
			// 
			// _SearchPage
			// 
			this._SearchPage.Controls.Add(this._SaveButton);
			this._SearchPage.Controls.Add(this._UrlBox);
			this._SearchPage.Controls.Add(this.label6);
			this._SearchPage.Controls.Add(this._NameBox);
			this._SearchPage.Controls.Add(this.label2);
			this._SearchPage.Controls.Add(this._ResetButton);
			this._SearchPage.Controls.Add(this._MoveUpButton);
			this._SearchPage.Controls.Add(this._RemoveButton);
			this._SearchPage.Controls.Add(this._AddButton);
			this._SearchPage.Controls.Add(this._SearchEngineBox);
			this._SearchPage.Controls.Add(this.label5);
			this._SearchPage.Controls.Add(this.label4);
			this._SearchPage.Controls.Add(this.label3);
			this._SearchPage.Controls.Add(this._BrowseBrowserButton);
			this._SearchPage.Controls.Add(this._BrowserParameterBox);
			this._SearchPage.Controls.Add(this._BrowserPathBox);
			this._SearchPage.Controls.Add(this.label1);
			this._SearchPage.Location = new System.Drawing.Point(4, 25);
			this._SearchPage.Name = "_SearchPage";
			this._SearchPage.Padding = new System.Windows.Forms.Padding(3);
			this._SearchPage.Size = new System.Drawing.Size(561, 293);
			this._SearchPage.TabIndex = 2;
			this._SearchPage.Text = "Search";
			this._SearchPage.UseVisualStyleBackColor = true;
			// 
			// _SaveButton
			// 
			this._SaveButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this._SaveButton.Location = new System.Drawing.Point(459, 263);
			this._SaveButton.Name = "_SaveButton";
			this._SaveButton.Size = new System.Drawing.Size(96, 23);
			this._SaveButton.TabIndex = 14;
			this._SaveButton.Text = "Save";
			this._SaveButton.UseVisualStyleBackColor = true;
			// 
			// _UrlBox
			// 
			this._UrlBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this._UrlBox.Location = new System.Drawing.Point(225, 264);
			this._UrlBox.Name = "_UrlBox";
			this._UrlBox.Size = new System.Drawing.Size(228, 25);
			this._UrlBox.TabIndex = 13;
			// 
			// label6
			// 
			this.label6.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(180, 267);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(39, 15);
			this.label6.TabIndex = 12;
			this.label6.Text = "URL:";
			// 
			// _NameBox
			// 
			this._NameBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this._NameBox.Location = new System.Drawing.Point(70, 264);
			this._NameBox.Name = "_NameBox";
			this._NameBox.Size = new System.Drawing.Size(104, 25);
			this._NameBox.TabIndex = 11;
			// 
			// label2
			// 
			this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(17, 267);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(47, 15);
			this.label2.TabIndex = 10;
			this.label2.Text = "Name:";
			// 
			// _ResetButton
			// 
			this._ResetButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this._ResetButton.Location = new System.Drawing.Point(459, 231);
			this._ResetButton.Name = "_ResetButton";
			this._ResetButton.Size = new System.Drawing.Size(96, 23);
			this._ResetButton.TabIndex = 9;
			this._ResetButton.Text = "Reset...";
			this._ResetButton.UseVisualStyleBackColor = true;
			// 
			// _MoveUpButton
			// 
			this._MoveUpButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this._MoveUpButton.Enabled = false;
			this._MoveUpButton.Location = new System.Drawing.Point(459, 202);
			this._MoveUpButton.Name = "_MoveUpButton";
			this._MoveUpButton.Size = new System.Drawing.Size(96, 23);
			this._MoveUpButton.TabIndex = 9;
			this._MoveUpButton.Text = "Move up";
			this._MoveUpButton.UseVisualStyleBackColor = true;
			// 
			// _RemoveButton
			// 
			this._RemoveButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this._RemoveButton.Enabled = false;
			this._RemoveButton.Location = new System.Drawing.Point(459, 173);
			this._RemoveButton.Name = "_RemoveButton";
			this._RemoveButton.Size = new System.Drawing.Size(96, 23);
			this._RemoveButton.TabIndex = 8;
			this._RemoveButton.Text = "Remove";
			this._RemoveButton.UseVisualStyleBackColor = true;
			// 
			// _AddButton
			// 
			this._AddButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this._AddButton.Location = new System.Drawing.Point(459, 144);
			this._AddButton.Name = "_AddButton";
			this._AddButton.Size = new System.Drawing.Size(96, 23);
			this._AddButton.TabIndex = 8;
			this._AddButton.Text = "Add";
			this._AddButton.UseVisualStyleBackColor = true;
			// 
			// _SearchEngineBox
			// 
			this._SearchEngineBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this._SearchEngineBox.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._NameColumn,
            this._UrlColumn});
			this._SearchEngineBox.FullRowSelect = true;
			this._SearchEngineBox.HideSelection = false;
			this._SearchEngineBox.Location = new System.Drawing.Point(20, 144);
			this._SearchEngineBox.MultiSelect = false;
			this._SearchEngineBox.Name = "_SearchEngineBox";
			this._SearchEngineBox.Size = new System.Drawing.Size(433, 114);
			this._SearchEngineBox.TabIndex = 7;
			this._SearchEngineBox.UseCompatibleStateImageBehavior = false;
			this._SearchEngineBox.View = System.Windows.Forms.View.Details;
			// 
			// _NameColumn
			// 
			this._NameColumn.Text = "Name";
			// 
			// _UrlColumn
			// 
			this._UrlColumn.Text = "URL Pattern";
			this._UrlColumn.Width = 360;
			// 
			// label5
			// 
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(17, 126);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(351, 15);
			this.label5.TabIndex = 6;
			this.label5.Text = "Search engines (use %s for search keyword):";
			// 
			// label4
			// 
			this.label4.AutoSize = true;
			this.label4.ForeColor = System.Drawing.SystemColors.GrayText;
			this.label4.Location = new System.Drawing.Point(17, 101);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(231, 15);
			this.label4.TabIndex = 5;
			this.label4.Text = "Use %u for search engine URL";
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(17, 55);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(527, 15);
			this.label3.TabIndex = 4;
			this.label3.Text = "Web browser parameters (optional, empty to use URL as parameter):";
			// 
			// _BrowseBrowserButton
			// 
			this._BrowseBrowserButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this._BrowseBrowserButton.Location = new System.Drawing.Point(459, 27);
			this._BrowseBrowserButton.Name = "_BrowseBrowserButton";
			this._BrowseBrowserButton.Size = new System.Drawing.Size(99, 23);
			this._BrowseBrowserButton.TabIndex = 2;
			this._BrowseBrowserButton.Text = "Browse...";
			this._BrowseBrowserButton.UseVisualStyleBackColor = true;
			// 
			// _BrowserParameterBox
			// 
			this._BrowserParameterBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this._BrowserParameterBox.Location = new System.Drawing.Point(20, 73);
			this._BrowserParameterBox.Name = "_BrowserParameterBox";
			this._BrowserParameterBox.Size = new System.Drawing.Size(433, 25);
			this._BrowserParameterBox.TabIndex = 1;
			// 
			// _BrowserPathBox
			// 
			this._BrowserPathBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this._BrowserPathBox.Location = new System.Drawing.Point(20, 27);
			this._BrowserPathBox.Name = "_BrowserPathBox";
			this._BrowserPathBox.Size = new System.Drawing.Size(433, 25);
			this._BrowserPathBox.TabIndex = 1;
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(17, 9);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(503, 15);
			this.label1.TabIndex = 0;
			this.label1.Text = "Search with web browser (empty to use system default browser):";
			// 
			// _BrowseBrowserDialog
			// 
			this._BrowseBrowserDialog.DefaultExt = "exe";
			this._BrowseBrowserDialog.FileName = "Browser name";
			this._BrowseBrowserDialog.Filter = "Executable files|*.exe";
			this._BrowseBrowserDialog.Title = "Select the path to a web browser";
			// 
			// SmartBarPage
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this._SuperQuickInfoTabs);
			this.Name = "SmartBarPage";
			this.Size = new System.Drawing.Size(575, 328);
			this.Load += new System.EventHandler(this.SmartBarPage_Load);
			this._SuperQuickInfoTabs.ResumeLayout(false);
			this.tabPage2.ResumeLayout(false);
			this.tabPage2.PerformLayout();
			this._SearchPage.ResumeLayout(false);
			this._SearchPage.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion
		private System.Windows.Forms.CheckBox _ToggleSmartBarBox;
		private System.Windows.Forms.TabControl _SuperQuickInfoTabs;
		private System.Windows.Forms.TabPage tabPage2;
		private System.Windows.Forms.CheckBox _AutoShowSmartBarBox;
		private System.Windows.Forms.TabPage _SearchPage;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Button _BrowseBrowserButton;
		private System.Windows.Forms.TextBox _BrowserParameterBox;
		private System.Windows.Forms.TextBox _BrowserPathBox;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.OpenFileDialog _BrowseBrowserDialog;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.Button _MoveUpButton;
		private System.Windows.Forms.Button _RemoveButton;
		private System.Windows.Forms.Button _AddButton;
		private System.Windows.Forms.ListView _SearchEngineBox;
		private System.Windows.Forms.ColumnHeader _NameColumn;
		private System.Windows.Forms.ColumnHeader _UrlColumn;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.Button _ResetButton;
		private System.Windows.Forms.Button _SaveButton;
		private System.Windows.Forms.TextBox _UrlBox;
		private System.Windows.Forms.Label label6;
		private System.Windows.Forms.TextBox _NameBox;
		private System.Windows.Forms.Label label2;
	}
}
