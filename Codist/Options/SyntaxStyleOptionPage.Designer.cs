namespace Codist.Options
{
	partial class SyntaxStyleOptionPage
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SyntaxStyleOptionPage));
			this.label1 = new System.Windows.Forms.Label();
			this._StyleSettingsBox = new System.Windows.Forms.GroupBox();
			this._BackgroundOpacityButton = new Codist.Options.PickOpacityButton();
			this._ForegroundOpacityButton = new Codist.Options.PickOpacityButton();
			this._ResetButton = new System.Windows.Forms.Button();
			this.label2 = new System.Windows.Forms.Label();
			this._BackgroundEffectBox = new System.Windows.Forms.ComboBox();
			this._FontBox = new System.Windows.Forms.ComboBox();
			this.label8 = new System.Windows.Forms.Label();
			this._PreviewBox = new System.Windows.Forms.PictureBox();
			this._BackColorButton = new Codist.Options.PickColorButton();
			this._ForeColorButton = new Codist.Options.PickColorButton();
			this._FontSizeBox = new System.Windows.Forms.NumericUpDown();
			this.label4 = new System.Windows.Forms.Label();
			this._StrikeBox = new System.Windows.Forms.CheckBox();
			this._UnderlineBox = new System.Windows.Forms.CheckBox();
			this._ItalicBox = new System.Windows.Forms.CheckBox();
			this._BoldBox = new System.Windows.Forms.CheckBox();
			this._SyntaxListBox = new Codist.Options.SyntaxListView();
			this._NameColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this._StyleSettingsBox.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this._PreviewBox)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this._FontSizeBox)).BeginInit();
			this.SuspendLayout();
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(0, 3);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(159, 15);
			this.label1.TabIndex = 0;
			this.label1.Text = "Syntax definitions:";
			// 
			// _StyleSettingsBox
			// 
			this._StyleSettingsBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this._StyleSettingsBox.Controls.Add(this._BackgroundOpacityButton);
			this._StyleSettingsBox.Controls.Add(this._ForegroundOpacityButton);
			this._StyleSettingsBox.Controls.Add(this._ResetButton);
			this._StyleSettingsBox.Controls.Add(this.label2);
			this._StyleSettingsBox.Controls.Add(this._BackgroundEffectBox);
			this._StyleSettingsBox.Controls.Add(this._FontBox);
			this._StyleSettingsBox.Controls.Add(this.label8);
			this._StyleSettingsBox.Controls.Add(this._PreviewBox);
			this._StyleSettingsBox.Controls.Add(this._BackColorButton);
			this._StyleSettingsBox.Controls.Add(this._ForeColorButton);
			this._StyleSettingsBox.Controls.Add(this._FontSizeBox);
			this._StyleSettingsBox.Controls.Add(this.label4);
			this._StyleSettingsBox.Controls.Add(this._StrikeBox);
			this._StyleSettingsBox.Controls.Add(this._UnderlineBox);
			this._StyleSettingsBox.Controls.Add(this._ItalicBox);
			this._StyleSettingsBox.Controls.Add(this._BoldBox);
			this._StyleSettingsBox.Location = new System.Drawing.Point(248, 3);
			this._StyleSettingsBox.Name = "_StyleSettingsBox";
			this._StyleSettingsBox.Size = new System.Drawing.Size(271, 417);
			this._StyleSettingsBox.TabIndex = 2;
			this._StyleSettingsBox.TabStop = false;
			this._StyleSettingsBox.Text = "Syntax Style";
			// 
			// _BackgroundOpacityButton
			// 
			this._BackgroundOpacityButton.Location = new System.Drawing.Point(132, 163);
			this._BackgroundOpacityButton.Name = "_BackgroundOpacityButton";
			this._BackgroundOpacityButton.Size = new System.Drawing.Size(120, 23);
			this._BackgroundOpacityButton.TabIndex = 21;
			this._BackgroundOpacityButton.Text = "Opacity not set";
			this._BackgroundOpacityButton.UseVisualStyleBackColor = true;
			this._BackgroundOpacityButton.Value = ((byte)(0));
			// 
			// _ForegroundOpacityButton
			// 
			this._ForegroundOpacityButton.Location = new System.Drawing.Point(132, 134);
			this._ForegroundOpacityButton.Name = "_ForegroundOpacityButton";
			this._ForegroundOpacityButton.Size = new System.Drawing.Size(120, 23);
			this._ForegroundOpacityButton.TabIndex = 21;
			this._ForegroundOpacityButton.Text = "Opacity not set";
			this._ForegroundOpacityButton.UseVisualStyleBackColor = true;
			this._ForegroundOpacityButton.Value = ((byte)(0));
			// 
			// _ResetButton
			// 
			this._ResetButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this._ResetButton.Location = new System.Drawing.Point(190, 0);
			this._ResetButton.Name = "_ResetButton";
			this._ResetButton.Size = new System.Drawing.Size(75, 23);
			this._ResetButton.TabIndex = 20;
			this._ResetButton.Text = "Reset";
			this._ResetButton.UseVisualStyleBackColor = true;
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(6, 199);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(95, 15);
			this.label2.TabIndex = 19;
			this.label2.Text = "Background:";
			// 
			// _BackgroundEffectBox
			// 
			this._BackgroundEffectBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this._BackgroundEffectBox.FormattingEnabled = true;
			this._BackgroundEffectBox.Location = new System.Drawing.Point(122, 196);
			this._BackgroundEffectBox.Name = "_BackgroundEffectBox";
			this._BackgroundEffectBox.Size = new System.Drawing.Size(130, 23);
			this._BackgroundEffectBox.TabIndex = 18;
			// 
			// _FontBox
			// 
			this._FontBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this._FontBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this._FontBox.FormattingEnabled = true;
			this._FontBox.Location = new System.Drawing.Point(66, 21);
			this._FontBox.Name = "_FontBox";
			this._FontBox.Size = new System.Drawing.Size(186, 23);
			this._FontBox.TabIndex = 17;
			// 
			// label8
			// 
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(6, 24);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(47, 15);
			this.label8.TabIndex = 16;
			this.label8.Text = "Font:";
			// 
			// _PreviewBox
			// 
			this._PreviewBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this._PreviewBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this._PreviewBox.Location = new System.Drawing.Point(7, 225);
			this._PreviewBox.Name = "_PreviewBox";
			this._PreviewBox.Size = new System.Drawing.Size(249, 186);
			this._PreviewBox.TabIndex = 15;
			this._PreviewBox.TabStop = false;
			// 
			// _BackColorButton
			// 
			this._BackColorButton.DefaultColor = System.Drawing.Color.Empty;
			this._BackColorButton.Image = ((System.Drawing.Image)(resources.GetObject("_BackColorButton.Image")));
			this._BackColorButton.Location = new System.Drawing.Point(6, 163);
			this._BackColorButton.Name = "_BackColorButton";
			this._BackColorButton.SelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
			this._BackColorButton.Size = new System.Drawing.Size(120, 23);
			this._BackColorButton.TabIndex = 12;
			this._BackColorButton.Text = "Background";
			this._BackColorButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
			this._BackColorButton.UseVisualStyleBackColor = true;
			// 
			// _ForeColorButton
			// 
			this._ForeColorButton.DefaultColor = System.Drawing.Color.Empty;
			this._ForeColorButton.Image = ((System.Drawing.Image)(resources.GetObject("_ForeColorButton.Image")));
			this._ForeColorButton.Location = new System.Drawing.Point(6, 134);
			this._ForeColorButton.Name = "_ForeColorButton";
			this._ForeColorButton.SelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
			this._ForeColorButton.Size = new System.Drawing.Size(120, 23);
			this._ForeColorButton.TabIndex = 8;
			this._ForeColorButton.Text = "Foreground";
			this._ForeColorButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
			this._ForeColorButton.UseVisualStyleBackColor = true;
			// 
			// _FontSizeBox
			// 
			this._FontSizeBox.Location = new System.Drawing.Point(123, 53);
			this._FontSizeBox.Minimum = new decimal(new int[] {
            10,
            0,
            0,
            -2147483648});
			this._FontSizeBox.Name = "_FontSizeBox";
			this._FontSizeBox.Size = new System.Drawing.Size(129, 25);
			this._FontSizeBox.TabIndex = 6;
			// 
			// label4
			// 
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(6, 55);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(87, 15);
			this.label4.TabIndex = 5;
			this.label4.Text = "Font size:";
			// 
			// _StrikeBox
			// 
			this._StrikeBox.AutoSize = true;
			this._StrikeBox.Location = new System.Drawing.Point(123, 109);
			this._StrikeBox.Name = "_StrikeBox";
			this._StrikeBox.Size = new System.Drawing.Size(133, 19);
			this._StrikeBox.TabIndex = 4;
			this._StrikeBox.Text = "Strikethrough";
			this._StrikeBox.ThreeState = true;
			this._StrikeBox.UseVisualStyleBackColor = true;
			// 
			// _UnderlineBox
			// 
			this._UnderlineBox.AutoSize = true;
			this._UnderlineBox.Location = new System.Drawing.Point(6, 109);
			this._UnderlineBox.Name = "_UnderlineBox";
			this._UnderlineBox.Size = new System.Drawing.Size(101, 19);
			this._UnderlineBox.TabIndex = 3;
			this._UnderlineBox.Text = "Underline";
			this._UnderlineBox.ThreeState = true;
			this._UnderlineBox.UseVisualStyleBackColor = true;
			// 
			// _ItalicBox
			// 
			this._ItalicBox.AutoSize = true;
			this._ItalicBox.Location = new System.Drawing.Point(123, 84);
			this._ItalicBox.Name = "_ItalicBox";
			this._ItalicBox.Size = new System.Drawing.Size(77, 19);
			this._ItalicBox.TabIndex = 2;
			this._ItalicBox.Text = "Italic";
			this._ItalicBox.ThreeState = true;
			this._ItalicBox.UseVisualStyleBackColor = true;
			// 
			// _BoldBox
			// 
			this._BoldBox.AutoSize = true;
			this._BoldBox.Location = new System.Drawing.Point(6, 84);
			this._BoldBox.Name = "_BoldBox";
			this._BoldBox.Size = new System.Drawing.Size(61, 19);
			this._BoldBox.TabIndex = 1;
			this._BoldBox.Text = "Bold";
			this._BoldBox.ThreeState = true;
			this._BoldBox.UseVisualStyleBackColor = true;
			// 
			// _SyntaxListBox
			// 
			this._SyntaxListBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
			this._SyntaxListBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
			this._SyntaxListBox.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._NameColumn});
			this._SyntaxListBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
			this._SyntaxListBox.FullRowSelect = true;
			this._SyntaxListBox.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
			this._SyntaxListBox.HideSelection = false;
			this._SyntaxListBox.Location = new System.Drawing.Point(3, 24);
			this._SyntaxListBox.MultiSelect = false;
			this._SyntaxListBox.Name = "_SyntaxListBox";
			this._SyntaxListBox.ShowItemToolTips = true;
			this._SyntaxListBox.Size = new System.Drawing.Size(239, 390);
			this._SyntaxListBox.TabIndex = 1;
			this._SyntaxListBox.UseCompatibleStateImageBehavior = false;
			this._SyntaxListBox.View = System.Windows.Forms.View.Details;
			// 
			// _NameColumn
			// 
			this._NameColumn.Text = "Name";
			this._NameColumn.Width = 200;
			// 
			// SyntaxStyleOptionPage
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this._SyntaxListBox);
			this.Controls.Add(this._StyleSettingsBox);
			this.Controls.Add(this.label1);
			this.Name = "SyntaxStyleOptionPage";
			this.Size = new System.Drawing.Size(522, 423);
			this._StyleSettingsBox.ResumeLayout(false);
			this._StyleSettingsBox.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this._PreviewBox)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this._FontSizeBox)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.GroupBox _StyleSettingsBox;
		private PickColorButton _BackColorButton;
		private PickColorButton _ForeColorButton;
		private System.Windows.Forms.NumericUpDown _FontSizeBox;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.CheckBox _StrikeBox;
		private System.Windows.Forms.CheckBox _UnderlineBox;
		private System.Windows.Forms.CheckBox _ItalicBox;
		private System.Windows.Forms.CheckBox _BoldBox;
		private SyntaxListView _SyntaxListBox;
		private System.Windows.Forms.ColumnHeader _NameColumn;
		private System.Windows.Forms.PictureBox _PreviewBox;
		private System.Windows.Forms.ComboBox _FontBox;
		private System.Windows.Forms.Label label8;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.ComboBox _BackgroundEffectBox;
		private System.Windows.Forms.Button _ResetButton;
		private PickOpacityButton _BackgroundOpacityButton;
		private PickOpacityButton _ForegroundOpacityButton;
	}
}
