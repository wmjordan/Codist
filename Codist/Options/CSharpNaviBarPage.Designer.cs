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
			this._StripNonLetterCharactersBox = new System.Windows.Forms.CheckBox();
			this._RegionBox = new System.Windows.Forms.CheckBox();
			this.label1 = new System.Windows.Forms.Label();
			this._RangeHighlightBox = new System.Windows.Forms.CheckBox();
			this._ToolTipBox = new System.Windows.Forms.CheckBox();
			this._SyntaxNodesBox = new System.Windows.Forms.CheckBox();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this._AutoPropertyValueBox = new System.Windows.Forms.CheckBox();
			this._ParameterListParamNameBox = new System.Windows.Forms.CheckBox();
			this._ParameterListBox = new System.Windows.Forms.CheckBox();
			this._RegionItemBox = new System.Windows.Forms.CheckBox();
			this._PartialClassBox = new System.Windows.Forms.CheckBox();
			this._FieldValueBox = new System.Windows.Forms.CheckBox();
			this.customGroupBox1 = new Codist.Controls.CustomGroupBox();
			this.label2 = new System.Windows.Forms.Label();
			this._OptionTabs.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.customGroupBox1.SuspendLayout();
			this.SuspendLayout();
			// 
			// _OptionTabs
			// 
			this._OptionTabs.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this._OptionTabs.Controls.Add(this.tabPage2);
			this._OptionTabs.Controls.Add(this.tabPage1);
			this._OptionTabs.Location = new System.Drawing.Point(3, 3);
			this._OptionTabs.Name = "_OptionTabs";
			this._OptionTabs.SelectedIndex = 0;
			this._OptionTabs.Size = new System.Drawing.Size(532, 349);
			this._OptionTabs.TabIndex = 0;
			// 
			// tabPage2
			// 
			this.tabPage2.Controls.Add(this.customGroupBox1);
			this.tabPage2.Controls.Add(this._StripNonLetterCharactersBox);
			this.tabPage2.Controls.Add(this._RegionBox);
			this.tabPage2.Controls.Add(this._RangeHighlightBox);
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
			// _StripNonLetterCharactersBox
			// 
			this._StripNonLetterCharactersBox.AutoSize = true;
			this._StripNonLetterCharactersBox.Location = new System.Drawing.Point(39, 106);
			this._StripNonLetterCharactersBox.Name = "_StripNonLetterCharactersBox";
			this._StripNonLetterCharactersBox.Size = new System.Drawing.Size(237, 19);
			this._StripNonLetterCharactersBox.TabIndex = 11;
			this._StripNonLetterCharactersBox.Text = "Trim non-letter characters";
			this._StripNonLetterCharactersBox.UseVisualStyleBackColor = true;
			// 
			// _RegionBox
			// 
			this._RegionBox.AutoSize = true;
			this._RegionBox.Location = new System.Drawing.Point(15, 81);
			this._RegionBox.Name = "_RegionBox";
			this._RegionBox.Size = new System.Drawing.Size(165, 19);
			this._RegionBox.TabIndex = 10;
			this._RegionBox.Text = "Show #region name";
			this._RegionBox.UseVisualStyleBackColor = true;
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(6, 30);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(343, 15);
			this.label1.TabIndex = 9;
			this.label1.Text = "Currently Navigation Bar works on C# only.";
			// 
			// _RangeHighlightBox
			// 
			this._RangeHighlightBox.AutoSize = true;
			this._RangeHighlightBox.Location = new System.Drawing.Point(15, 56);
			this._RangeHighlightBox.Name = "_RangeHighlightBox";
			this._RangeHighlightBox.Size = new System.Drawing.Size(269, 19);
			this._RangeHighlightBox.TabIndex = 2;
			this._RangeHighlightBox.Text = "Highlight node range in editor";
			this._RangeHighlightBox.UseVisualStyleBackColor = true;
			// 
			// _ToolTipBox
			// 
			this._ToolTipBox.AutoSize = true;
			this._ToolTipBox.Location = new System.Drawing.Point(15, 31);
			this._ToolTipBox.Name = "_ToolTipBox";
			this._ToolTipBox.Size = new System.Drawing.Size(189, 19);
			this._ToolTipBox.TabIndex = 1;
			this._ToolTipBox.Text = "Show symbol info tip";
			this._ToolTipBox.UseVisualStyleBackColor = true;
			// 
			// _SyntaxNodesBox
			// 
			this._SyntaxNodesBox.AutoSize = true;
			this._SyntaxNodesBox.Location = new System.Drawing.Point(15, 6);
			this._SyntaxNodesBox.Name = "_SyntaxNodesBox";
			this._SyntaxNodesBox.Size = new System.Drawing.Size(173, 19);
			this._SyntaxNodesBox.TabIndex = 0;
			this._SyntaxNodesBox.Text = "Show syntax detail";
			this._SyntaxNodesBox.UseVisualStyleBackColor = true;
			// 
			// tabPage1
			// 
			this.tabPage1.Controls.Add(this._AutoPropertyValueBox);
			this.tabPage1.Controls.Add(this._ParameterListParamNameBox);
			this.tabPage1.Controls.Add(this._ParameterListBox);
			this.tabPage1.Controls.Add(this._RegionItemBox);
			this.tabPage1.Controls.Add(this._PartialClassBox);
			this.tabPage1.Controls.Add(this._FieldValueBox);
			this.tabPage1.Location = new System.Drawing.Point(4, 25);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(524, 320);
			this.tabPage1.TabIndex = 2;
			this.tabPage1.Text = "Drop-down Menu";
			this.tabPage1.UseVisualStyleBackColor = true;
			// 
			// _AutoPropertyValueBox
			// 
			this._AutoPropertyValueBox.AutoSize = true;
			this._AutoPropertyValueBox.Location = new System.Drawing.Point(31, 81);
			this._AutoPropertyValueBox.Name = "_AutoPropertyValueBox";
			this._AutoPropertyValueBox.Size = new System.Drawing.Size(181, 19);
			this._AutoPropertyValueBox.TabIndex = 12;
			this._AutoPropertyValueBox.Text = "Show property value";
			this._AutoPropertyValueBox.UseVisualStyleBackColor = true;
			// 
			// _ParameterListParamNameBox
			// 
			this._ParameterListParamNameBox.AutoSize = true;
			this._ParameterListParamNameBox.Location = new System.Drawing.Point(31, 31);
			this._ParameterListParamNameBox.Name = "_ParameterListParamNameBox";
			this._ParameterListParamNameBox.Size = new System.Drawing.Size(309, 19);
			this._ParameterListParamNameBox.TabIndex = 10;
			this._ParameterListParamNameBox.Text = "Show parameter name instead of type";
			this._ParameterListParamNameBox.UseVisualStyleBackColor = true;
			// 
			// _ParameterListBox
			// 
			this._ParameterListBox.AutoSize = true;
			this._ParameterListBox.Location = new System.Drawing.Point(15, 6);
			this._ParameterListBox.Name = "_ParameterListBox";
			this._ParameterListBox.Size = new System.Drawing.Size(197, 19);
			this._ParameterListBox.TabIndex = 9;
			this._ParameterListBox.Text = "Show method parameter";
			this._ParameterListBox.UseVisualStyleBackColor = true;
			// 
			// _RegionItemBox
			// 
			this._RegionItemBox.AutoSize = true;
			this._RegionItemBox.Location = new System.Drawing.Point(15, 131);
			this._RegionItemBox.Name = "_RegionItemBox";
			this._RegionItemBox.Size = new System.Drawing.Size(205, 19);
			this._RegionItemBox.TabIndex = 14;
			this._RegionItemBox.Text = "Show #region directive";
			this._RegionItemBox.UseVisualStyleBackColor = true;
			// 
			// _PartialClassBox
			// 
			this._PartialClassBox.AutoSize = true;
			this._PartialClassBox.Location = new System.Drawing.Point(15, 106);
			this._PartialClassBox.Name = "_PartialClassBox";
			this._PartialClassBox.Size = new System.Drawing.Size(189, 19);
			this._PartialClassBox.TabIndex = 13;
			this._PartialClassBox.Text = "Include partial type";
			this._PartialClassBox.UseVisualStyleBackColor = true;
			// 
			// _FieldValueBox
			// 
			this._FieldValueBox.AutoSize = true;
			this._FieldValueBox.Location = new System.Drawing.Point(15, 56);
			this._FieldValueBox.Name = "_FieldValueBox";
			this._FieldValueBox.Size = new System.Drawing.Size(157, 19);
			this._FieldValueBox.TabIndex = 11;
			this._FieldValueBox.Text = "Show field value";
			this._FieldValueBox.UseVisualStyleBackColor = true;
			// 
			// customGroupBox1
			// 
			this.customGroupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.customGroupBox1.Controls.Add(this.label2);
			this.customGroupBox1.Controls.Add(this.label1);
			this.customGroupBox1.Location = new System.Drawing.Point(6, 128);
			this.customGroupBox1.Name = "customGroupBox1";
			this.customGroupBox1.Size = new System.Drawing.Size(512, 100);
			this.customGroupBox1.TabIndex = 12;
			this.customGroupBox1.TabStop = false;
			this.customGroupBox1.Text = "Note:";
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(6, 59);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(367, 15);
			this.label2.TabIndex = 9;
			this.label2.Text = "Press Ctrl+` to open the first dropdown menu.";
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
			this.tabPage1.ResumeLayout(false);
			this.tabPage1.PerformLayout();
			this.customGroupBox1.ResumeLayout(false);
			this.customGroupBox1.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion
		private System.Windows.Forms.TabControl _OptionTabs;
		private System.Windows.Forms.TabPage tabPage2;
		private System.Windows.Forms.CheckBox _SyntaxNodesBox;
		private System.Windows.Forms.CheckBox _ToolTipBox;
		private System.Windows.Forms.CheckBox _RangeHighlightBox;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.CheckBox _RegionBox;
		private System.Windows.Forms.CheckBox _StripNonLetterCharactersBox;
		private System.Windows.Forms.TabPage tabPage1;
		private System.Windows.Forms.CheckBox _AutoPropertyValueBox;
		private System.Windows.Forms.CheckBox _ParameterListParamNameBox;
		private System.Windows.Forms.CheckBox _ParameterListBox;
		private System.Windows.Forms.CheckBox _RegionItemBox;
		private System.Windows.Forms.CheckBox _PartialClassBox;
		private System.Windows.Forms.CheckBox _FieldValueBox;
		private Controls.CustomGroupBox customGroupBox1;
		private System.Windows.Forms.Label label2;
	}
}
