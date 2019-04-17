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
			this._SuperQuickInfoTabs.SuspendLayout();
			this.tabPage2.SuspendLayout();
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
			this.ResumeLayout(false);

		}

		#endregion
		private System.Windows.Forms.CheckBox _ToggleSmartBarBox;
		private System.Windows.Forms.TabControl _SuperQuickInfoTabs;
		private System.Windows.Forms.TabPage tabPage2;
		private System.Windows.Forms.CheckBox _AutoShowSmartBarBox;
	}
}
