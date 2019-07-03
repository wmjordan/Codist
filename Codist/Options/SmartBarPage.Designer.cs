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
			this.label4 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
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
			this._SearchPage.Controls.Add(this.label4);
			this._SearchPage.Controls.Add(this.label3);
			this._SearchPage.Controls.Add(this.label2);
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
			// label4
			// 
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(17, 142);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(487, 30);
			this.label4.TabIndex = 5;
			this.label4.Text = "Use %u for search URL.\r\nIf parameter is empty, search URL will be used as paramet" +
    "er.";
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(17, 96);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(279, 15);
			this.label3.TabIndex = 4;
			this.label3.Text = "Web browser parameters (optional):";
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(17, 60);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(463, 15);
			this.label2.TabIndex = 3;
			this.label2.Text = "To use system default browser, leave the above box empty.";
			// 
			// _BrowseBrowserButton
			// 
			this._BrowseBrowserButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this._BrowseBrowserButton.Location = new System.Drawing.Point(459, 32);
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
			this._BrowserParameterBox.Location = new System.Drawing.Point(20, 114);
			this._BrowserParameterBox.Name = "_BrowserParameterBox";
			this._BrowserParameterBox.Size = new System.Drawing.Size(433, 25);
			this._BrowserParameterBox.TabIndex = 1;
			// 
			// _BrowserPathBox
			// 
			this._BrowserPathBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this._BrowserPathBox.Location = new System.Drawing.Point(20, 32);
			this._BrowserPathBox.Name = "_BrowserPathBox";
			this._BrowserPathBox.Size = new System.Drawing.Size(433, 25);
			this._BrowserPathBox.TabIndex = 1;
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(17, 14);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(199, 15);
			this.label1.TabIndex = 0;
			this.label1.Text = "Search with web browser:";
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
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Button _BrowseBrowserButton;
		private System.Windows.Forms.TextBox _BrowserParameterBox;
		private System.Windows.Forms.TextBox _BrowserPathBox;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.OpenFileDialog _BrowseBrowserDialog;
		private System.Windows.Forms.Label label4;
	}
}
