namespace Codist.Options
{
	partial class MiscPage
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
			this._SaveConfigButton = new System.Windows.Forms.Button();
			this._LoadConfigButton = new System.Windows.Forms.Button();
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
			this.groupBox1.Location = new System.Drawing.Point(3, 32);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(407, 84);
			this.groupBox1.TabIndex = 2;
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
			this._SaveConfigButton.Location = new System.Drawing.Point(12, 3);
			this._SaveConfigButton.Name = "_SaveConfigButton";
			this._SaveConfigButton.Size = new System.Drawing.Size(173, 23);
			this._SaveConfigButton.TabIndex = 0;
			this._SaveConfigButton.Text = "&Save configs...";
			this._SaveConfigButton.UseVisualStyleBackColor = true;
			// 
			// _LoadConfigButton
			// 
			this._LoadConfigButton.Location = new System.Drawing.Point(202, 3);
			this._LoadConfigButton.Name = "_LoadConfigButton";
			this._LoadConfigButton.Size = new System.Drawing.Size(173, 23);
			this._LoadConfigButton.TabIndex = 1;
			this._LoadConfigButton.Text = "&Load configs...";
			this._LoadConfigButton.UseVisualStyleBackColor = true;
			// 
			// MiscPage
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this._LoadConfigButton);
			this.Controls.Add(this._SaveConfigButton);
			this.Controls.Add(this.groupBox1);
			this.Name = "MiscPage";
			this.Size = new System.Drawing.Size(575, 328);
			this.Load += new System.EventHandler(this.MiscPage_Load);
			((System.ComponentModel.ISupportInitialize)(this._BottomMarginBox)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this._TopMarginBox)).EndInit();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.ResumeLayout(false);

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
	}
}
