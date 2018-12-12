namespace Codist.Options
{
	partial class CSharpNaviBarPage
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
			this._OptionTabs = new System.Windows.Forms.TabControl();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this._ParameterListParamNameBox = new System.Windows.Forms.CheckBox();
			this.titleLabel1 = new Codist.Controls.TitleLabel();
			this._RangeHighlightBox = new System.Windows.Forms.CheckBox();
			this._ParameterListBox = new System.Windows.Forms.CheckBox();
			this._RegionBox = new System.Windows.Forms.CheckBox();
			this._PartialClassBox = new System.Windows.Forms.CheckBox();
			this._FieldValueBox = new System.Windows.Forms.CheckBox();
			this._ToolTipBox = new System.Windows.Forms.CheckBox();
			this._SyntaxNodesBox = new System.Windows.Forms.CheckBox();
			this._AutoPropertyValueBox = new System.Windows.Forms.CheckBox();
			this.label1 = new System.Windows.Forms.Label();
			this._OptionTabs.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.SuspendLayout();
			// 
			// _OptionTabs
			// 
			this._OptionTabs.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this._OptionTabs.Controls.Add(this.tabPage2);
			this._OptionTabs.Location = new System.Drawing.Point(3, 3);
			this._OptionTabs.Name = "_OptionTabs";
			this._OptionTabs.SelectedIndex = 0;
			this._OptionTabs.Size = new System.Drawing.Size(532, 349);
			this._OptionTabs.TabIndex = 0;
			// 
			// tabPage2
			// 
			this.tabPage2.Controls.Add(this.label1);
			this.tabPage2.Controls.Add(this._AutoPropertyValueBox);
			this.tabPage2.Controls.Add(this._ParameterListParamNameBox);
			this.tabPage2.Controls.Add(this.titleLabel1);
			this.tabPage2.Controls.Add(this._RangeHighlightBox);
			this.tabPage2.Controls.Add(this._ParameterListBox);
			this.tabPage2.Controls.Add(this._RegionBox);
			this.tabPage2.Controls.Add(this._PartialClassBox);
			this.tabPage2.Controls.Add(this._FieldValueBox);
			this.tabPage2.Controls.Add(this._ToolTipBox);
			this.tabPage2.Controls.Add(this._SyntaxNodesBox);
			this.tabPage2.Location = new System.Drawing.Point(4, 25);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(524, 320);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "Navigation Bar";
			this.tabPage2.UseVisualStyleBackColor = true;
			// 
			// _ParameterListParamNameBox
			// 
			this._ParameterListParamNameBox.AutoSize = true;
			this._ParameterListParamNameBox.Location = new System.Drawing.Point(31, 141);
			this._ParameterListParamNameBox.Name = "_ParameterListParamNameBox";
			this._ParameterListParamNameBox.Size = new System.Drawing.Size(173, 19);
			this._ParameterListParamNameBox.TabIndex = 4;
			this._ParameterListParamNameBox.Text = "Use parameter name";
			this._ParameterListParamNameBox.UseVisualStyleBackColor = true;
			// 
			// titleLabel1
			// 
			this.titleLabel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.titleLabel1.Location = new System.Drawing.Point(12, 89);
			this.titleLabel1.Name = "titleLabel1";
			this.titleLabel1.Size = new System.Drawing.Size(488, 15);
			this.titleLabel1.TabIndex = 3;
			this.titleLabel1.Text = "Drop-down Item";
			// 
			// _RangeHighlightBox
			// 
			this._RangeHighlightBox.AutoSize = true;
			this._RangeHighlightBox.Location = new System.Drawing.Point(15, 56);
			this._RangeHighlightBox.Name = "_RangeHighlightBox";
			this._RangeHighlightBox.Size = new System.Drawing.Size(189, 19);
			this._RangeHighlightBox.TabIndex = 2;
			this._RangeHighlightBox.Text = "Code range highlight";
			this._RangeHighlightBox.UseVisualStyleBackColor = true;
			// 
			// _ParameterListBox
			// 
			this._ParameterListBox.AutoSize = true;
			this._ParameterListBox.Location = new System.Drawing.Point(15, 116);
			this._ParameterListBox.Name = "_ParameterListBox";
			this._ParameterListBox.Size = new System.Drawing.Size(157, 19);
			this._ParameterListBox.TabIndex = 3;
			this._ParameterListBox.Text = "Method parameter";
			this._ParameterListBox.UseVisualStyleBackColor = true;
			// 
			// _RegionBox
			// 
			this._RegionBox.AutoSize = true;
			this._RegionBox.Location = new System.Drawing.Point(15, 241);
			this._RegionBox.Name = "_RegionBox";
			this._RegionBox.Size = new System.Drawing.Size(165, 19);
			this._RegionBox.TabIndex = 8;
			this._RegionBox.Text = "#region directive";
			this._RegionBox.UseVisualStyleBackColor = true;
			// 
			// _PartialClassBox
			// 
			this._PartialClassBox.AutoSize = true;
			this._PartialClassBox.Location = new System.Drawing.Point(15, 216);
			this._PartialClassBox.Name = "_PartialClassBox";
			this._PartialClassBox.Size = new System.Drawing.Size(197, 19);
			this._PartialClassBox.TabIndex = 7;
			this._PartialClassBox.Text = "Partial class members";
			this._PartialClassBox.UseVisualStyleBackColor = true;
			// 
			// _FieldValueBox
			// 
			this._FieldValueBox.AutoSize = true;
			this._FieldValueBox.Location = new System.Drawing.Point(15, 166);
			this._FieldValueBox.Name = "_FieldValueBox";
			this._FieldValueBox.Size = new System.Drawing.Size(117, 19);
			this._FieldValueBox.TabIndex = 5;
			this._FieldValueBox.Text = "Field value";
			this._FieldValueBox.UseVisualStyleBackColor = true;
			// 
			// _ToolTipBox
			// 
			this._ToolTipBox.AutoSize = true;
			this._ToolTipBox.Location = new System.Drawing.Point(15, 31);
			this._ToolTipBox.Name = "_ToolTipBox";
			this._ToolTipBox.Size = new System.Drawing.Size(149, 19);
			this._ToolTipBox.TabIndex = 1;
			this._ToolTipBox.Text = "Symbol tool tip";
			this._ToolTipBox.UseVisualStyleBackColor = true;
			// 
			// _SyntaxNodesBox
			// 
			this._SyntaxNodesBox.AutoSize = true;
			this._SyntaxNodesBox.Location = new System.Drawing.Point(15, 6);
			this._SyntaxNodesBox.Name = "_SyntaxNodesBox";
			this._SyntaxNodesBox.Size = new System.Drawing.Size(133, 19);
			this._SyntaxNodesBox.TabIndex = 0;
			this._SyntaxNodesBox.Text = "Syntax detail";
			this._SyntaxNodesBox.UseVisualStyleBackColor = true;
			// 
			// _AutoPropertyValueBox
			// 
			this._AutoPropertyValueBox.AutoSize = true;
			this._AutoPropertyValueBox.Location = new System.Drawing.Point(31, 191);
			this._AutoPropertyValueBox.Name = "_AutoPropertyValueBox";
			this._AutoPropertyValueBox.Size = new System.Drawing.Size(133, 19);
			this._AutoPropertyValueBox.TabIndex = 6;
			this._AutoPropertyValueBox.Text = "Auto property";
			this._AutoPropertyValueBox.UseVisualStyleBackColor = true;
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(12, 279);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(391, 15);
			this.label1.TabIndex = 9;
			this.label1.Text = "Note: currently Navigation Bar works on C# only.";
			// 
			// CSharpNaviBarPage
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this._OptionTabs);
			this.Name = "CSharpNaviBarPage";
			this.Size = new System.Drawing.Size(535, 355);
			this.Load += new System.EventHandler(this.CSharpNaviBarPage_Load);
			this._OptionTabs.ResumeLayout(false);
			this.tabPage2.ResumeLayout(false);
			this.tabPage2.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion
		private System.Windows.Forms.TabControl _OptionTabs;
		private System.Windows.Forms.TabPage tabPage2;
		private System.Windows.Forms.CheckBox _ParameterListBox;
		private System.Windows.Forms.CheckBox _PartialClassBox;
		private System.Windows.Forms.CheckBox _FieldValueBox;
		private System.Windows.Forms.CheckBox _SyntaxNodesBox;
		private System.Windows.Forms.CheckBox _ToolTipBox;
		private System.Windows.Forms.CheckBox _RegionBox;
		private System.Windows.Forms.CheckBox _RangeHighlightBox;
		private Controls.TitleLabel titleLabel1;
		private System.Windows.Forms.CheckBox _ParameterListParamNameBox;
		private System.Windows.Forms.CheckBox _AutoPropertyValueBox;
		private System.Windows.Forms.Label label1;
	}
}
