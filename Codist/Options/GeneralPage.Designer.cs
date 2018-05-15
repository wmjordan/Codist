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
			this._BottomMarginBox = new System.Windows.Forms.NumericUpDown();
			this._TopMarginBox = new System.Windows.Forms.NumericUpDown();
			this.label2 = new System.Windows.Forms.Label();
			this.label1 = new System.Windows.Forms.Label();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this._NoSpaceBetweenWrappedLinesBox = new System.Windows.Forms.CheckBox();
			this._GlobalFeatureBox = new System.Windows.Forms.CheckBox();
			this.label3 = new System.Windows.Forms.Label();
			((System.ComponentModel.ISupportInitialize)(this._BottomMarginBox)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this._TopMarginBox)).BeginInit();
			this.groupBox1.SuspendLayout();
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
			this.groupBox1.Location = new System.Drawing.Point(3, 81);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(499, 84);
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
			// GeneralPage
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this.label3);
			this.Controls.Add(this._GlobalFeatureBox);
			this.Controls.Add(this.groupBox1);
			this.Name = "GeneralPage";
			this.Size = new System.Drawing.Size(575, 328);
			this.Load += new System.EventHandler(this.MiscPage_Load);
			((System.ComponentModel.ISupportInitialize)(this._BottomMarginBox)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this._TopMarginBox)).EndInit();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
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
		private System.Windows.Forms.CheckBox _GlobalFeatureBox;
		private System.Windows.Forms.Label label3;
	}
}
