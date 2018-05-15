namespace Codist.Options
{
	partial class CSharpScrollbarMarkerPage
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
			this._DirectivesBox = new System.Windows.Forms.CheckBox();
			this._SpecialCommentsBox = new System.Windows.Forms.CheckBox();
			this._TypeDeclarationBox = new System.Windows.Forms.CheckBox();
			this._LongMethodBox = new System.Windows.Forms.CheckBox();
			this._MemberDeclarationBox = new System.Windows.Forms.CheckBox();
			this._OptionTabs = new System.Windows.Forms.TabControl();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this._OptionTabs.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.SuspendLayout();
			// 
			// _DirectivesBox
			// 
			this._DirectivesBox.AutoSize = true;
			this._DirectivesBox.Location = new System.Drawing.Point(15, 81);
			this._DirectivesBox.Name = "_DirectivesBox";
			this._DirectivesBox.Size = new System.Drawing.Size(173, 19);
			this._DirectivesBox.TabIndex = 3;
			this._DirectivesBox.Text = "Compiler directive";
			this._DirectivesBox.UseVisualStyleBackColor = true;
			// 
			// _SpecialCommentsBox
			// 
			this._SpecialCommentsBox.AutoSize = true;
			this._SpecialCommentsBox.Location = new System.Drawing.Point(15, 108);
			this._SpecialCommentsBox.Name = "_SpecialCommentsBox";
			this._SpecialCommentsBox.Size = new System.Drawing.Size(149, 19);
			this._SpecialCommentsBox.TabIndex = 4;
			this._SpecialCommentsBox.Text = "Special comment";
			this._SpecialCommentsBox.UseVisualStyleBackColor = true;
			// 
			// _TypeDeclarationBox
			// 
			this._TypeDeclarationBox.AutoSize = true;
			this._TypeDeclarationBox.Location = new System.Drawing.Point(39, 56);
			this._TypeDeclarationBox.Name = "_TypeDeclarationBox";
			this._TypeDeclarationBox.Size = new System.Drawing.Size(101, 19);
			this._TypeDeclarationBox.TabIndex = 2;
			this._TypeDeclarationBox.Text = "Type name";
			this._TypeDeclarationBox.UseVisualStyleBackColor = true;
			// 
			// _LongMethodBox
			// 
			this._LongMethodBox.AutoSize = true;
			this._LongMethodBox.Location = new System.Drawing.Point(39, 31);
			this._LongMethodBox.Name = "_LongMethodBox";
			this._LongMethodBox.Size = new System.Drawing.Size(157, 19);
			this._LongMethodBox.TabIndex = 1;
			this._LongMethodBox.Text = "Long method name";
			this._LongMethodBox.UseVisualStyleBackColor = true;
			// 
			// _MemberDeclarationBox
			// 
			this._MemberDeclarationBox.AutoSize = true;
			this._MemberDeclarationBox.Location = new System.Drawing.Point(15, 6);
			this._MemberDeclarationBox.Name = "_MemberDeclarationBox";
			this._MemberDeclarationBox.Size = new System.Drawing.Size(213, 19);
			this._MemberDeclarationBox.TabIndex = 0;
			this._MemberDeclarationBox.Text = "Member declaration line";
			this._MemberDeclarationBox.UseVisualStyleBackColor = true;
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
			this.tabPage2.Controls.Add(this._DirectivesBox);
			this.tabPage2.Controls.Add(this._SpecialCommentsBox);
			this.tabPage2.Controls.Add(this._MemberDeclarationBox);
			this.tabPage2.Controls.Add(this._TypeDeclarationBox);
			this.tabPage2.Controls.Add(this._LongMethodBox);
			this.tabPage2.Location = new System.Drawing.Point(4, 25);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(524, 320);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "Scrollbar marker";
			this.tabPage2.UseVisualStyleBackColor = true;
			// 
			// CSharpScrollbarMarkerPage
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this._OptionTabs);
			this.Name = "CSharpScrollbarMarkerPage";
			this.Size = new System.Drawing.Size(535, 355);
			this.Load += new System.EventHandler(this.CSharpScrollbarMarkerPage_Load);
			this._OptionTabs.ResumeLayout(false);
			this.tabPage2.ResumeLayout(false);
			this.tabPage2.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion
		private System.Windows.Forms.CheckBox _SpecialCommentsBox;
		private System.Windows.Forms.CheckBox _MemberDeclarationBox;
		private System.Windows.Forms.CheckBox _DirectivesBox;
		private System.Windows.Forms.CheckBox _LongMethodBox;
		private System.Windows.Forms.CheckBox _TypeDeclarationBox;
		private System.Windows.Forms.TabControl _OptionTabs;
		private System.Windows.Forms.TabPage tabPage2;
	}
}
