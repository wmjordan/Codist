namespace Codist.Options
{
	partial class ScrollbarMarkerPage
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
			this._GeneralScrolbarMarkerTabs = new System.Windows.Forms.TabControl();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this._SelectionBox = new System.Windows.Forms.CheckBox();
			this._LineNumbersBox = new System.Windows.Forms.CheckBox();
			this._SpecialCommentsBox = new System.Windows.Forms.CheckBox();
			this._GeneralScrolbarMarkerTabs.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.SuspendLayout();
			// 
			// _GeneralScrolbarMarkerTabs
			// 
			this._GeneralScrolbarMarkerTabs.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this._GeneralScrolbarMarkerTabs.Controls.Add(this.tabPage2);
			this._GeneralScrolbarMarkerTabs.Location = new System.Drawing.Point(3, 3);
			this._GeneralScrolbarMarkerTabs.Name = "_GeneralScrolbarMarkerTabs";
			this._GeneralScrolbarMarkerTabs.SelectedIndex = 0;
			this._GeneralScrolbarMarkerTabs.Size = new System.Drawing.Size(569, 322);
			this._GeneralScrolbarMarkerTabs.TabIndex = 5;
			// 
			// tabPage2
			// 
			this.tabPage2.Controls.Add(this._SpecialCommentsBox);
			this.tabPage2.Controls.Add(this._SelectionBox);
			this.tabPage2.Controls.Add(this._LineNumbersBox);
			this.tabPage2.Location = new System.Drawing.Point(4, 25);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(561, 293);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "General";
			this.tabPage2.UseVisualStyleBackColor = true;
			// 
			// _SelectionBox
			// 
			this._SelectionBox.AutoSize = true;
			this._SelectionBox.Location = new System.Drawing.Point(15, 31);
			this._SelectionBox.Name = "_SelectionBox";
			this._SelectionBox.Size = new System.Drawing.Size(149, 19);
			this._SelectionBox.TabIndex = 1;
			this._SelectionBox.Text = "Selection range";
			this._SelectionBox.UseVisualStyleBackColor = true;
			// 
			// _LineNumbersBox
			// 
			this._LineNumbersBox.AutoSize = true;
			this._LineNumbersBox.Location = new System.Drawing.Point(15, 6);
			this._LineNumbersBox.Name = "_LineNumbersBox";
			this._LineNumbersBox.Size = new System.Drawing.Size(125, 19);
			this._LineNumbersBox.TabIndex = 1;
			this._LineNumbersBox.Text = "Line numbers";
			this._LineNumbersBox.UseVisualStyleBackColor = true;
			// 
			// _SpecialCommentsBox
			// 
			this._SpecialCommentsBox.AutoSize = true;
			this._SpecialCommentsBox.Location = new System.Drawing.Point(15, 56);
			this._SpecialCommentsBox.Name = "_SpecialCommentsBox";
			this._SpecialCommentsBox.Size = new System.Drawing.Size(149, 19);
			this._SpecialCommentsBox.TabIndex = 6;
			this._SpecialCommentsBox.Text = "Tagged comments";
			this._SpecialCommentsBox.UseVisualStyleBackColor = true;
			// 
			// ScrollbarMarkerPage
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this._GeneralScrolbarMarkerTabs);
			this.Name = "ScrollbarMarkerPage";
			this.Size = new System.Drawing.Size(575, 328);
			this.Load += new System.EventHandler(this.ScrollbarMarkerPage_Load);
			this._GeneralScrolbarMarkerTabs.ResumeLayout(false);
			this.tabPage2.ResumeLayout(false);
			this.tabPage2.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion
		private System.Windows.Forms.TabControl _GeneralScrolbarMarkerTabs;
		private System.Windows.Forms.TabPage tabPage2;
		private System.Windows.Forms.CheckBox _LineNumbersBox;
		private System.Windows.Forms.CheckBox _SelectionBox;
		private System.Windows.Forms.CheckBox _SpecialCommentsBox;
	}
}
