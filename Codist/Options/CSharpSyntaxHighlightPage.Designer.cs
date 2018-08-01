namespace Codist.Options
{
	partial class CSharpSyntaxHighlightPage
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
			this._HighlightSpecialCommentBox = new System.Windows.Forms.CheckBox();
			this._HighlightDeclarationBracesBox = new System.Windows.Forms.CheckBox();
			this._OptionTabs = new System.Windows.Forms.TabControl();
			this.tabPage3 = new System.Windows.Forms.TabPage();
			this._HighlightParameterBracesBox = new System.Windows.Forms.CheckBox();
			this._MarkSpecialPunctuationBox = new System.Windows.Forms.CheckBox();
			this._HighlightResourceBracesBox = new System.Windows.Forms.CheckBox();
			this._HighlightLoopBracesBox = new System.Windows.Forms.CheckBox();
			this._HighlightBranchBracesBox = new System.Windows.Forms.CheckBox();
			this._OptionTabs.SuspendLayout();
			this.tabPage3.SuspendLayout();
			this.SuspendLayout();
			// 
			// _HighlightSpecialCommentBox
			// 
			this._HighlightSpecialCommentBox.AutoSize = true;
			this._HighlightSpecialCommentBox.Location = new System.Drawing.Point(15, 180);
			this._HighlightSpecialCommentBox.Name = "_HighlightSpecialCommentBox";
			this._HighlightSpecialCommentBox.Size = new System.Drawing.Size(149, 19);
			this._HighlightSpecialCommentBox.TabIndex = 2;
			this._HighlightSpecialCommentBox.Text = "Special comment";
			this._HighlightSpecialCommentBox.UseVisualStyleBackColor = true;
			// 
			// _HighlightDeclarationBracesBox
			// 
			this._HighlightDeclarationBracesBox.AutoSize = true;
			this._HighlightDeclarationBracesBox.Location = new System.Drawing.Point(15, 43);
			this._HighlightDeclarationBracesBox.Name = "_HighlightDeclarationBracesBox";
			this._HighlightDeclarationBracesBox.Size = new System.Drawing.Size(197, 19);
			this._HighlightDeclarationBracesBox.TabIndex = 0;
			this._HighlightDeclarationBracesBox.Text = "Declaration braces {}";
			this._HighlightDeclarationBracesBox.UseVisualStyleBackColor = true;
			// 
			// _OptionTabs
			// 
			this._OptionTabs.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this._OptionTabs.Controls.Add(this.tabPage3);
			this._OptionTabs.Location = new System.Drawing.Point(3, 3);
			this._OptionTabs.Name = "_OptionTabs";
			this._OptionTabs.SelectedIndex = 0;
			this._OptionTabs.Size = new System.Drawing.Size(532, 349);
			this._OptionTabs.TabIndex = 0;
			// 
			// tabPage3
			// 
			this.tabPage3.Controls.Add(this._HighlightParameterBracesBox);
			this.tabPage3.Controls.Add(this._HighlightSpecialCommentBox);
			this.tabPage3.Controls.Add(this._MarkSpecialPunctuationBox);
			this.tabPage3.Controls.Add(this._HighlightResourceBracesBox);
			this.tabPage3.Controls.Add(this._HighlightLoopBracesBox);
			this.tabPage3.Controls.Add(this._HighlightBranchBracesBox);
			this.tabPage3.Controls.Add(this._HighlightDeclarationBracesBox);
			this.tabPage3.Location = new System.Drawing.Point(4, 25);
			this.tabPage3.Name = "tabPage3";
			this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage3.Size = new System.Drawing.Size(524, 320);
			this.tabPage3.TabIndex = 2;
			this.tabPage3.Text = "Additional highlight";
			this.tabPage3.UseVisualStyleBackColor = true;
			// 
			// _HighlightParameterBracesBox
			// 
			this._HighlightParameterBracesBox.AutoSize = true;
			this._HighlightParameterBracesBox.Location = new System.Drawing.Point(15, 68);
			this._HighlightParameterBracesBox.Name = "_HighlightParameterBracesBox";
			this._HighlightParameterBracesBox.Size = new System.Drawing.Size(221, 19);
			this._HighlightParameterBracesBox.TabIndex = 1;
			this._HighlightParameterBracesBox.Text = "Parameter parentheses ()";
			this._HighlightParameterBracesBox.UseVisualStyleBackColor = true;
			// 
			// _MarkSpecialPunctuationBox
			// 
			this._MarkSpecialPunctuationBox.AutoSize = true;
			this._MarkSpecialPunctuationBox.Location = new System.Drawing.Point(15, 6);
			this._MarkSpecialPunctuationBox.Name = "_MarkSpecialPunctuationBox";
			this._MarkSpecialPunctuationBox.Size = new System.Drawing.Size(237, 19);
			this._MarkSpecialPunctuationBox.TabIndex = 5;
			this._MarkSpecialPunctuationBox.Text = "Make following braces bold";
			this._MarkSpecialPunctuationBox.UseVisualStyleBackColor = true;
			// 
			// _HighlightResourceBracesBox
			// 
			this._HighlightResourceBracesBox.AutoSize = true;
			this._HighlightResourceBracesBox.Location = new System.Drawing.Point(15, 143);
			this._HighlightResourceBracesBox.Name = "_HighlightResourceBracesBox";
			this._HighlightResourceBracesBox.Size = new System.Drawing.Size(389, 19);
			this._HighlightResourceBracesBox.TabIndex = 5;
			this._HighlightResourceBracesBox.Text = "Resource and exception braces and parentheses";
			this._HighlightResourceBracesBox.UseVisualStyleBackColor = true;
			// 
			// _HighlightLoopBracesBox
			// 
			this._HighlightLoopBracesBox.AutoSize = true;
			this._HighlightLoopBracesBox.Location = new System.Drawing.Point(15, 118);
			this._HighlightLoopBracesBox.Name = "_HighlightLoopBracesBox";
			this._HighlightLoopBracesBox.Size = new System.Drawing.Size(245, 19);
			this._HighlightLoopBracesBox.TabIndex = 4;
			this._HighlightLoopBracesBox.Text = "Loop braces and parentheses";
			this._HighlightLoopBracesBox.UseVisualStyleBackColor = true;
			// 
			// _HighlightBranchBracesBox
			// 
			this._HighlightBranchBracesBox.AutoSize = true;
			this._HighlightBranchBracesBox.Location = new System.Drawing.Point(15, 93);
			this._HighlightBranchBracesBox.Name = "_HighlightBranchBracesBox";
			this._HighlightBranchBracesBox.Size = new System.Drawing.Size(261, 19);
			this._HighlightBranchBracesBox.TabIndex = 3;
			this._HighlightBranchBracesBox.Text = "Branch braces and parentheses";
			this._HighlightBranchBracesBox.UseVisualStyleBackColor = true;
			// 
			// CSharpSyntaxHighlightPage
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this._OptionTabs);
			this.Name = "CSharpSyntaxHighlightPage";
			this.Size = new System.Drawing.Size(535, 355);
			this.Load += new System.EventHandler(this.CSharpSpecialHighlightPage_Load);
			this._OptionTabs.ResumeLayout(false);
			this.tabPage3.ResumeLayout(false);
			this.tabPage3.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion
		private System.Windows.Forms.CheckBox _HighlightDeclarationBracesBox;
		private System.Windows.Forms.CheckBox _HighlightSpecialCommentBox;
		private System.Windows.Forms.TabControl _OptionTabs;
		private System.Windows.Forms.TabPage tabPage3;
		private System.Windows.Forms.CheckBox _HighlightParameterBracesBox;
		private System.Windows.Forms.CheckBox _HighlightLoopBracesBox;
		private System.Windows.Forms.CheckBox _HighlightBranchBracesBox;
		private System.Windows.Forms.CheckBox _HighlightResourceBracesBox;
		private System.Windows.Forms.CheckBox _MarkSpecialPunctuationBox;
	}
}
