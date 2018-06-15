namespace Codist.Options
{
	partial class CommentTaggerOptionPage
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
			this._SyntaxListBox = new System.Windows.Forms.ListView();
			this._NameColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.label1 = new System.Windows.Forms.Label();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this._ApplyContentTagBox = new System.Windows.Forms.RadioButton();
			this._PreviewBox = new System.Windows.Forms.PictureBox();
			this._EndWithPunctuationBox = new System.Windows.Forms.CheckBox();
			this._ApplyTagBox = new System.Windows.Forms.RadioButton();
			this._ApplyContentBox = new System.Windows.Forms.RadioButton();
			this.label4 = new System.Windows.Forms.Label();
			this._StyleBox = new System.Windows.Forms.ComboBox();
			this.label3 = new System.Windows.Forms.Label();
			this._IgnoreCaseBox = new System.Windows.Forms.CheckBox();
			this._TagTextBox = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this._AddTagButton = new System.Windows.Forms.Button();
			this._RemoveTagButton = new System.Windows.Forms.Button();
			this._CommentTaggerTabs = new System.Windows.Forms.TabControl();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.groupBox1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this._PreviewBox)).BeginInit();
			this._CommentTaggerTabs.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.SuspendLayout();
			// 
			// _SyntaxListBox
			// 
			this._SyntaxListBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
			this._SyntaxListBox.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._NameColumn});
			this._SyntaxListBox.FullRowSelect = true;
			this._SyntaxListBox.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
			this._SyntaxListBox.HideSelection = false;
			this._SyntaxListBox.Location = new System.Drawing.Point(9, 24);
			this._SyntaxListBox.MultiSelect = false;
			this._SyntaxListBox.Name = "_SyntaxListBox";
			this._SyntaxListBox.ShowGroups = false;
			this._SyntaxListBox.Size = new System.Drawing.Size(239, 263);
			this._SyntaxListBox.TabIndex = 1;
			this._SyntaxListBox.UseCompatibleStateImageBehavior = false;
			this._SyntaxListBox.View = System.Windows.Forms.View.Details;
			// 
			// _NameColumn
			// 
			this._NameColumn.Text = "Name";
			this._NameColumn.Width = 200;
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(6, 6);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(111, 15);
			this.label1.TabIndex = 0;
			this.label1.Text = "Comment Tags:";
			// 
			// groupBox1
			// 
			this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.groupBox1.Controls.Add(this._ApplyContentTagBox);
			this.groupBox1.Controls.Add(this._PreviewBox);
			this.groupBox1.Controls.Add(this._EndWithPunctuationBox);
			this.groupBox1.Controls.Add(this._ApplyTagBox);
			this.groupBox1.Controls.Add(this._ApplyContentBox);
			this.groupBox1.Controls.Add(this.label4);
			this.groupBox1.Controls.Add(this._StyleBox);
			this.groupBox1.Controls.Add(this.label3);
			this.groupBox1.Controls.Add(this._IgnoreCaseBox);
			this.groupBox1.Controls.Add(this._TagTextBox);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Location = new System.Drawing.Point(254, 6);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(311, 281);
			this.groupBox1.TabIndex = 2;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Tag Definition";
			// 
			// _ApplyContentTagBox
			// 
			this._ApplyContentTagBox.AutoSize = true;
			this._ApplyContentTagBox.Location = new System.Drawing.Point(89, 159);
			this._ApplyContentTagBox.Name = "_ApplyContentTagBox";
			this._ApplyContentTagBox.Size = new System.Drawing.Size(148, 19);
			this._ApplyContentTagBox.TabIndex = 9;
			this._ApplyContentTagBox.TabStop = true;
			this._ApplyContentTagBox.Text = "Tag and content";
			this._ApplyContentTagBox.UseVisualStyleBackColor = true;
			// 
			// _PreviewBox
			// 
			this._PreviewBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this._PreviewBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this._PreviewBox.Location = new System.Drawing.Point(7, 184);
			this._PreviewBox.Name = "_PreviewBox";
			this._PreviewBox.Size = new System.Drawing.Size(294, 91);
			this._PreviewBox.TabIndex = 8;
			this._PreviewBox.TabStop = false;
			// 
			// _EndWithPunctuationBox
			// 
			this._EndWithPunctuationBox.AutoSize = true;
			this._EndWithPunctuationBox.Location = new System.Drawing.Point(89, 80);
			this._EndWithPunctuationBox.Name = "_EndWithPunctuationBox";
			this._EndWithPunctuationBox.Size = new System.Drawing.Size(189, 19);
			this._EndWithPunctuationBox.TabIndex = 3;
			this._EndWithPunctuationBox.Text = "End with punctuation";
			this._EndWithPunctuationBox.UseVisualStyleBackColor = true;
			// 
			// _ApplyTagBox
			// 
			this._ApplyTagBox.AutoSize = true;
			this._ApplyTagBox.Location = new System.Drawing.Point(89, 134);
			this._ApplyTagBox.Name = "_ApplyTagBox";
			this._ApplyTagBox.Size = new System.Drawing.Size(52, 19);
			this._ApplyTagBox.TabIndex = 7;
			this._ApplyTagBox.TabStop = true;
			this._ApplyTagBox.Text = "Tag";
			this._ApplyTagBox.UseVisualStyleBackColor = true;
			// 
			// _ApplyContentBox
			// 
			this._ApplyContentBox.AutoSize = true;
			this._ApplyContentBox.Location = new System.Drawing.Point(147, 134);
			this._ApplyContentBox.Name = "_ApplyContentBox";
			this._ApplyContentBox.Size = new System.Drawing.Size(84, 19);
			this._ApplyContentBox.TabIndex = 8;
			this._ApplyContentBox.TabStop = true;
			this._ApplyContentBox.Text = "Content";
			this._ApplyContentBox.UseVisualStyleBackColor = true;
			// 
			// label4
			// 
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(4, 136);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(79, 15);
			this.label4.TabIndex = 6;
			this.label4.Text = "Apply on:";
			// 
			// _StyleBox
			// 
			this._StyleBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this._StyleBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this._StyleBox.FormattingEnabled = true;
			this._StyleBox.Location = new System.Drawing.Point(89, 105);
			this._StyleBox.Name = "_StyleBox";
			this._StyleBox.Size = new System.Drawing.Size(212, 23);
			this._StyleBox.TabIndex = 5;
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(6, 105);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(55, 15);
			this.label3.TabIndex = 4;
			this.label3.Text = "Style:";
			// 
			// _IgnoreCaseBox
			// 
			this._IgnoreCaseBox.AutoSize = true;
			this._IgnoreCaseBox.Location = new System.Drawing.Point(89, 55);
			this._IgnoreCaseBox.Name = "_IgnoreCaseBox";
			this._IgnoreCaseBox.Size = new System.Drawing.Size(157, 19);
			this._IgnoreCaseBox.TabIndex = 2;
			this._IgnoreCaseBox.Text = "Case insensitive";
			this._IgnoreCaseBox.UseVisualStyleBackColor = true;
			// 
			// _TagTextBox
			// 
			this._TagTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this._TagTextBox.Location = new System.Drawing.Point(89, 24);
			this._TagTextBox.Name = "_TagTextBox";
			this._TagTextBox.Size = new System.Drawing.Size(212, 25);
			this._TagTextBox.TabIndex = 1;
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(6, 27);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(79, 15);
			this.label2.TabIndex = 0;
			this.label2.Text = "Tag text:";
			// 
			// _AddTagButton
			// 
			this._AddTagButton.Location = new System.Drawing.Point(123, 6);
			this._AddTagButton.Name = "_AddTagButton";
			this._AddTagButton.Size = new System.Drawing.Size(54, 23);
			this._AddTagButton.TabIndex = 3;
			this._AddTagButton.Text = "Add";
			this._AddTagButton.UseVisualStyleBackColor = true;
			// 
			// _RemoveTagButton
			// 
			this._RemoveTagButton.Location = new System.Drawing.Point(183, 6);
			this._RemoveTagButton.Name = "_RemoveTagButton";
			this._RemoveTagButton.Size = new System.Drawing.Size(65, 23);
			this._RemoveTagButton.TabIndex = 3;
			this._RemoveTagButton.Text = "Remove";
			this._RemoveTagButton.UseVisualStyleBackColor = true;
			// 
			// _CommentTaggerTabs
			// 
			this._CommentTaggerTabs.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this._CommentTaggerTabs.Controls.Add(this.tabPage2);
			this._CommentTaggerTabs.Location = new System.Drawing.Point(3, 3);
			this._CommentTaggerTabs.Name = "_CommentTaggerTabs";
			this._CommentTaggerTabs.SelectedIndex = 0;
			this._CommentTaggerTabs.Size = new System.Drawing.Size(569, 322);
			this._CommentTaggerTabs.TabIndex = 5;
			// 
			// tabPage2
			// 
			this.tabPage2.Controls.Add(this._RemoveTagButton);
			this.tabPage2.Controls.Add(this.groupBox1);
			this.tabPage2.Controls.Add(this._AddTagButton);
			this.tabPage2.Controls.Add(this.label1);
			this.tabPage2.Controls.Add(this._SyntaxListBox);
			this.tabPage2.Location = new System.Drawing.Point(4, 25);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(561, 293);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "Tags";
			this.tabPage2.UseVisualStyleBackColor = true;
			// 
			// CommentTaggerOptionPage
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this._CommentTaggerTabs);
			this.Name = "CommentTaggerOptionPage";
			this.Size = new System.Drawing.Size(575, 328);
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this._PreviewBox)).EndInit();
			this._CommentTaggerTabs.ResumeLayout(false);
			this.tabPage2.ResumeLayout(false);
			this.tabPage2.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.ListView _SyntaxListBox;
		private System.Windows.Forms.ColumnHeader _NameColumn;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.TextBox _TagTextBox;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.CheckBox _IgnoreCaseBox;
		private System.Windows.Forms.ComboBox _StyleBox;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.RadioButton _ApplyTagBox;
		private System.Windows.Forms.RadioButton _ApplyContentBox;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.CheckBox _EndWithPunctuationBox;
		private System.Windows.Forms.PictureBox _PreviewBox;
		private System.Windows.Forms.RadioButton _ApplyContentTagBox;
		private System.Windows.Forms.Button _AddTagButton;
		private System.Windows.Forms.Button _RemoveTagButton;
		private System.Windows.Forms.TabControl _CommentTaggerTabs;
		private System.Windows.Forms.TabPage tabPage2;
	}
}
