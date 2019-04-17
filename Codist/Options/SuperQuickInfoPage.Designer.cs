namespace Codist.Options
{
	partial class SuperQuickInfoPage
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
			this._SelectionQuickInfoBox = new System.Windows.Forms.CheckBox();
			this._ControlQuickInfoBox = new System.Windows.Forms.CheckBox();
			this._SuperQuickInfoTabs = new System.Windows.Forms.TabControl();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.groupBox4 = new Codist.Controls.CustomGroupBox();
			this._QuickInfoMaxHeightBox = new System.Windows.Forms.NumericUpDown();
			this.label2 = new System.Windows.Forms.Label();
			this.label1 = new System.Windows.Forms.Label();
			this._QuickInfoMaxWidthBox = new System.Windows.Forms.NumericUpDown();
			this._ColorQuickInfoBox = new System.Windows.Forms.CheckBox();
			this._SuperQuickInfoTabs.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.groupBox4.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this._QuickInfoMaxHeightBox)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this._QuickInfoMaxWidthBox)).BeginInit();
			this.SuspendLayout();
			// 
			// _SelectionQuickInfoBox
			// 
			this._SelectionQuickInfoBox.AutoSize = true;
			this._SelectionQuickInfoBox.Location = new System.Drawing.Point(15, 31);
			this._SelectionQuickInfoBox.Name = "_SelectionQuickInfoBox";
			this._SelectionQuickInfoBox.Size = new System.Drawing.Size(285, 19);
			this._SelectionQuickInfoBox.TabIndex = 2;
			this._SelectionQuickInfoBox.Text = "Show info about selection length";
			this._SelectionQuickInfoBox.UseVisualStyleBackColor = true;
			// 
			// _ControlQuickInfoBox
			// 
			this._ControlQuickInfoBox.AutoSize = true;
			this._ControlQuickInfoBox.Location = new System.Drawing.Point(15, 6);
			this._ControlQuickInfoBox.Name = "_ControlQuickInfoBox";
			this._ControlQuickInfoBox.Size = new System.Drawing.Size(365, 19);
			this._ControlQuickInfoBox.TabIndex = 1;
			this._ControlQuickInfoBox.Text = "Hide quick info until Shift key is pressed";
			this._ControlQuickInfoBox.UseVisualStyleBackColor = true;
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
			this._SuperQuickInfoTabs.TabIndex = 3;
			// 
			// tabPage2
			// 
			this.tabPage2.Controls.Add(this.groupBox4);
			this.tabPage2.Controls.Add(this._ColorQuickInfoBox);
			this.tabPage2.Controls.Add(this._ControlQuickInfoBox);
			this.tabPage2.Controls.Add(this._SelectionQuickInfoBox);
			this.tabPage2.Location = new System.Drawing.Point(4, 25);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(561, 293);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "General";
			this.tabPage2.UseVisualStyleBackColor = true;
			// 
			// groupBox4
			// 
			this.groupBox4.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.groupBox4.Controls.Add(this._QuickInfoMaxHeightBox);
			this.groupBox4.Controls.Add(this.label2);
			this.groupBox4.Controls.Add(this.label1);
			this.groupBox4.Controls.Add(this._QuickInfoMaxWidthBox);
			this.groupBox4.Location = new System.Drawing.Point(6, 93);
			this.groupBox4.Name = "groupBox4";
			this.groupBox4.Size = new System.Drawing.Size(549, 59);
			this.groupBox4.TabIndex = 10;
			this.groupBox4.TabStop = false;
			this.groupBox4.Text = "Quick Info Item Size (0: Unlimited)";
			// 
			// _QuickInfoMaxHeightBox
			// 
			this._QuickInfoMaxHeightBox.Increment = new decimal(new int[] {
            50,
            0,
            0,
            0});
			this._QuickInfoMaxHeightBox.Location = new System.Drawing.Point(348, 24);
			this._QuickInfoMaxHeightBox.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
			this._QuickInfoMaxHeightBox.Name = "_QuickInfoMaxHeightBox";
			this._QuickInfoMaxHeightBox.Size = new System.Drawing.Size(120, 25);
			this._QuickInfoMaxHeightBox.TabIndex = 3;
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(247, 26);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(95, 15);
			this.label2.TabIndex = 2;
			this.label2.Text = "Max height:";
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(7, 26);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(87, 15);
			this.label1.TabIndex = 0;
			this.label1.Text = "Max width:";
			// 
			// _QuickInfoMaxWidthBox
			// 
			this._QuickInfoMaxWidthBox.Increment = new decimal(new int[] {
            100,
            0,
            0,
            0});
			this._QuickInfoMaxWidthBox.Location = new System.Drawing.Point(111, 24);
			this._QuickInfoMaxWidthBox.Maximum = new decimal(new int[] {
            4000,
            0,
            0,
            0});
			this._QuickInfoMaxWidthBox.Name = "_QuickInfoMaxWidthBox";
			this._QuickInfoMaxWidthBox.Size = new System.Drawing.Size(120, 25);
			this._QuickInfoMaxWidthBox.TabIndex = 1;
			// 
			// _ColorQuickInfoBox
			// 
			this._ColorQuickInfoBox.AutoSize = true;
			this._ColorQuickInfoBox.Location = new System.Drawing.Point(15, 56);
			this._ColorQuickInfoBox.Name = "_ColorQuickInfoBox";
			this._ColorQuickInfoBox.Size = new System.Drawing.Size(197, 19);
			this._ColorQuickInfoBox.TabIndex = 3;
			this._ColorQuickInfoBox.Text = "Show info about color";
			this._ColorQuickInfoBox.UseVisualStyleBackColor = true;
			// 
			// SuperQuickInfoPage
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this._SuperQuickInfoTabs);
			this.Name = "SuperQuickInfoPage";
			this.Size = new System.Drawing.Size(575, 328);
			this.Load += new System.EventHandler(this.SuperQuickInfoPage_Load);
			this._SuperQuickInfoTabs.ResumeLayout(false);
			this.tabPage2.ResumeLayout(false);
			this.tabPage2.PerformLayout();
			this.groupBox4.ResumeLayout(false);
			this.groupBox4.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this._QuickInfoMaxHeightBox)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this._QuickInfoMaxWidthBox)).EndInit();
			this.ResumeLayout(false);

		}

		#endregion
		private System.Windows.Forms.CheckBox _ControlQuickInfoBox;
		private System.Windows.Forms.CheckBox _SelectionQuickInfoBox;
		private System.Windows.Forms.TabControl _SuperQuickInfoTabs;
		private System.Windows.Forms.TabPage tabPage2;
		private System.Windows.Forms.CheckBox _ColorQuickInfoBox;
		private Codist.Controls.CustomGroupBox groupBox4;
		private System.Windows.Forms.NumericUpDown _QuickInfoMaxHeightBox;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.NumericUpDown _QuickInfoMaxWidthBox;
	}
}
