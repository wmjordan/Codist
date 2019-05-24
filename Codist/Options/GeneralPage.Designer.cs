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
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this._GeneralPage = new System.Windows.Forms.TabPage();
			this.groupBox2 = new Codist.Controls.CustomGroupBox();
			this._CodeBarBox = new System.Windows.Forms.CheckBox();
			this._SmartBarBox = new System.Windows.Forms.CheckBox();
			this._ScrollbarMarkerBox = new System.Windows.Forms.CheckBox();
			this._SuperQuickInfoBox = new System.Windows.Forms.CheckBox();
			this._SyntaxHighlightBox = new System.Windows.Forms.CheckBox();
			this.label3 = new System.Windows.Forms.Label();
			this.groupBox3 = new Codist.Controls.CustomGroupBox();
			this._SaveConfigButton = new System.Windows.Forms.Button();
			this._LoadConfigButton = new System.Windows.Forms.Button();
			this._BuildPage = new System.Windows.Forms.TabPage();
			this.customGroupBox4 = new Codist.Controls.CustomGroupBox();
			this._IncrementVsixRevisionBox = new System.Windows.Forms.CheckBox();
			this.customGroupBox3 = new Codist.Controls.CustomGroupBox();
			this._BuildTimestampBox = new System.Windows.Forms.CheckBox();
			this._DisplayPage = new System.Windows.Forms.TabPage();
			this.customGroupBox1 = new Codist.Controls.CustomGroupBox();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this._OptimizeCodeWindowBox = new System.Windows.Forms.CheckBox();
			this._OptimizeMainWindowBox = new System.Windows.Forms.CheckBox();
			this.groupBox1 = new Codist.Controls.CustomGroupBox();
			this._NoSpaceBetweenWrappedLinesBox = new System.Windows.Forms.CheckBox();
			this._BottomMarginBox = new System.Windows.Forms.NumericUpDown();
			this.label2 = new System.Windows.Forms.Label();
			this._TopMarginBox = new System.Windows.Forms.NumericUpDown();
			this.label1 = new System.Windows.Forms.Label();
			this._AboutPage = new System.Windows.Forms.TabPage();
			this.customGroupBox2 = new Codist.Controls.CustomGroupBox();
			this.label7 = new System.Windows.Forms.Label();
			this.label6 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this._DonationLinkLabel = new System.Windows.Forms.LinkLabel();
			this._ReleaseLinkLabel = new System.Windows.Forms.LinkLabel();
			this._GitHubLinkLabel = new System.Windows.Forms.LinkLabel();
			this.label5 = new System.Windows.Forms.Label();
			this.tabControl1.SuspendLayout();
			this._GeneralPage.SuspendLayout();
			this.groupBox2.SuspendLayout();
			this.groupBox3.SuspendLayout();
			this._BuildPage.SuspendLayout();
			this.customGroupBox4.SuspendLayout();
			this.customGroupBox3.SuspendLayout();
			this._DisplayPage.SuspendLayout();
			this.customGroupBox1.SuspendLayout();
			this.groupBox1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this._BottomMarginBox)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this._TopMarginBox)).BeginInit();
			this._AboutPage.SuspendLayout();
			this.customGroupBox2.SuspendLayout();
			this.SuspendLayout();
			// 
			// tabControl1
			// 
			this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.tabControl1.Controls.Add(this._GeneralPage);
			this.tabControl1.Controls.Add(this._BuildPage);
			this.tabControl1.Controls.Add(this._DisplayPage);
			this.tabControl1.Controls.Add(this._AboutPage);
			this.tabControl1.Location = new System.Drawing.Point(3, 3);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(572, 325);
			this.tabControl1.TabIndex = 0;
			// 
			// _GeneralPage
			// 
			this._GeneralPage.Controls.Add(this.groupBox2);
			this._GeneralPage.Controls.Add(this.groupBox3);
			this._GeneralPage.Location = new System.Drawing.Point(4, 25);
			this._GeneralPage.Name = "_GeneralPage";
			this._GeneralPage.Padding = new System.Windows.Forms.Padding(3);
			this._GeneralPage.Size = new System.Drawing.Size(564, 296);
			this._GeneralPage.TabIndex = 0;
			this._GeneralPage.Text = "General";
			this._GeneralPage.UseVisualStyleBackColor = true;
			// 
			// groupBox2
			// 
			this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.groupBox2.Controls.Add(this._CodeBarBox);
			this.groupBox2.Controls.Add(this._SmartBarBox);
			this.groupBox2.Controls.Add(this._ScrollbarMarkerBox);
			this.groupBox2.Controls.Add(this._SuperQuickInfoBox);
			this.groupBox2.Controls.Add(this._SyntaxHighlightBox);
			this.groupBox2.Controls.Add(this.label3);
			this.groupBox2.Location = new System.Drawing.Point(6, 6);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(552, 151);
			this.groupBox2.TabIndex = 0;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "Feature Controllers";
			// 
			// _CodeBarBox
			// 
			this._CodeBarBox.AutoSize = true;
			this._CodeBarBox.Location = new System.Drawing.Point(251, 94);
			this._CodeBarBox.Name = "_CodeBarBox";
			this._CodeBarBox.Size = new System.Drawing.Size(141, 19);
			this._CodeBarBox.TabIndex = 5;
			this._CodeBarBox.Text = "Navigation bar";
			this._CodeBarBox.UseVisualStyleBackColor = true;
			// 
			// _SmartBarBox
			// 
			this._SmartBarBox.AutoSize = true;
			this._SmartBarBox.Location = new System.Drawing.Point(251, 69);
			this._SmartBarBox.Name = "_SmartBarBox";
			this._SmartBarBox.Size = new System.Drawing.Size(101, 19);
			this._SmartBarBox.TabIndex = 4;
			this._SmartBarBox.Text = "Smart bar";
			this._SmartBarBox.UseVisualStyleBackColor = true;
			// 
			// _ScrollbarMarkerBox
			// 
			this._ScrollbarMarkerBox.AutoSize = true;
			this._ScrollbarMarkerBox.Location = new System.Drawing.Point(9, 119);
			this._ScrollbarMarkerBox.Name = "_ScrollbarMarkerBox";
			this._ScrollbarMarkerBox.Size = new System.Drawing.Size(157, 19);
			this._ScrollbarMarkerBox.TabIndex = 3;
			this._ScrollbarMarkerBox.Text = "Scrollbar marker";
			this._ScrollbarMarkerBox.UseVisualStyleBackColor = true;
			// 
			// _SuperQuickInfoBox
			// 
			this._SuperQuickInfoBox.AutoSize = true;
			this._SuperQuickInfoBox.Location = new System.Drawing.Point(9, 94);
			this._SuperQuickInfoBox.Name = "_SuperQuickInfoBox";
			this._SuperQuickInfoBox.Size = new System.Drawing.Size(157, 19);
			this._SuperQuickInfoBox.TabIndex = 2;
			this._SuperQuickInfoBox.Text = "Super quick info";
			this._SuperQuickInfoBox.UseVisualStyleBackColor = true;
			// 
			// _SyntaxHighlightBox
			// 
			this._SyntaxHighlightBox.AutoSize = true;
			this._SyntaxHighlightBox.Location = new System.Drawing.Point(9, 69);
			this._SyntaxHighlightBox.Name = "_SyntaxHighlightBox";
			this._SyntaxHighlightBox.Size = new System.Drawing.Size(157, 19);
			this._SyntaxHighlightBox.TabIndex = 1;
			this._SyntaxHighlightBox.Text = "Syntax highlight";
			this._SyntaxHighlightBox.UseVisualStyleBackColor = true;
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(6, 21);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(439, 30);
			this.label3.TabIndex = 0;
			this.label3.Text = "NOTE: Changes will be applied on new document windows.\r\n* Turning off some featur" +
    "es might save battery power.";
			// 
			// groupBox3
			// 
			this.groupBox3.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.groupBox3.Controls.Add(this._SaveConfigButton);
			this.groupBox3.Controls.Add(this._LoadConfigButton);
			this.groupBox3.Location = new System.Drawing.Point(6, 163);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Size = new System.Drawing.Size(552, 58);
			this.groupBox3.TabIndex = 1;
			this.groupBox3.TabStop = false;
			this.groupBox3.Text = "Configuration File";
			// 
			// _SaveConfigButton
			// 
			this._SaveConfigButton.Location = new System.Drawing.Point(251, 24);
			this._SaveConfigButton.Name = "_SaveConfigButton";
			this._SaveConfigButton.Size = new System.Drawing.Size(111, 23);
			this._SaveConfigButton.TabIndex = 1;
			this._SaveConfigButton.Text = "&Save...";
			this._SaveConfigButton.UseVisualStyleBackColor = true;
			// 
			// _LoadConfigButton
			// 
			this._LoadConfigButton.Location = new System.Drawing.Point(9, 24);
			this._LoadConfigButton.Name = "_LoadConfigButton";
			this._LoadConfigButton.Size = new System.Drawing.Size(110, 23);
			this._LoadConfigButton.TabIndex = 0;
			this._LoadConfigButton.Text = "&Load...";
			this._LoadConfigButton.UseVisualStyleBackColor = true;
			// 
			// _BuildPage
			// 
			this._BuildPage.Controls.Add(this.customGroupBox4);
			this._BuildPage.Controls.Add(this.customGroupBox3);
			this._BuildPage.Location = new System.Drawing.Point(4, 25);
			this._BuildPage.Name = "_BuildPage";
			this._BuildPage.Padding = new System.Windows.Forms.Padding(3);
			this._BuildPage.Size = new System.Drawing.Size(564, 296);
			this._BuildPage.TabIndex = 3;
			this._BuildPage.Text = "Build";
			this._BuildPage.UseVisualStyleBackColor = true;
			// 
			// customGroupBox4
			// 
			this.customGroupBox4.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.customGroupBox4.Controls.Add(this._IncrementVsixRevisionBox);
			this.customGroupBox4.Location = new System.Drawing.Point(6, 96);
			this.customGroupBox4.Name = "customGroupBox4";
			this.customGroupBox4.Size = new System.Drawing.Size(552, 84);
			this.customGroupBox4.TabIndex = 1;
			this.customGroupBox4.TabStop = false;
			this.customGroupBox4.Text = "Visual Studio Extension Project";
			// 
			// _IncrementVsixRevisionBox
			// 
			this._IncrementVsixRevisionBox.AutoSize = true;
			this._IncrementVsixRevisionBox.Location = new System.Drawing.Point(9, 24);
			this._IncrementVsixRevisionBox.Name = "_IncrementVsixRevisionBox";
			this._IncrementVsixRevisionBox.Size = new System.Drawing.Size(501, 19);
			this._IncrementVsixRevisionBox.TabIndex = 4;
			this._IncrementVsixRevisionBox.Text = "Increment revision number in .vsixmanifest file after build";
			this._IncrementVsixRevisionBox.UseVisualStyleBackColor = true;
			// 
			// customGroupBox3
			// 
			this.customGroupBox3.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.customGroupBox3.Controls.Add(this._BuildTimestampBox);
			this.customGroupBox3.Location = new System.Drawing.Point(6, 6);
			this.customGroupBox3.Name = "customGroupBox3";
			this.customGroupBox3.Size = new System.Drawing.Size(552, 84);
			this.customGroupBox3.TabIndex = 1;
			this.customGroupBox3.TabStop = false;
			this.customGroupBox3.Text = "General Project";
			// 
			// _BuildTimestampBox
			// 
			this._BuildTimestampBox.AutoSize = true;
			this._BuildTimestampBox.Location = new System.Drawing.Point(9, 24);
			this._BuildTimestampBox.Name = "_BuildTimestampBox";
			this._BuildTimestampBox.Size = new System.Drawing.Size(405, 19);
			this._BuildTimestampBox.TabIndex = 4;
			this._BuildTimestampBox.Text = "Output time stamp before and after build events";
			this._BuildTimestampBox.UseVisualStyleBackColor = true;
			// 
			// _DisplayPage
			// 
			this._DisplayPage.Controls.Add(this.customGroupBox1);
			this._DisplayPage.Controls.Add(this.groupBox1);
			this._DisplayPage.Location = new System.Drawing.Point(4, 25);
			this._DisplayPage.Name = "_DisplayPage";
			this._DisplayPage.Padding = new System.Windows.Forms.Padding(3);
			this._DisplayPage.Size = new System.Drawing.Size(564, 296);
			this._DisplayPage.TabIndex = 1;
			this._DisplayPage.Text = "Display";
			this._DisplayPage.UseVisualStyleBackColor = true;
			// 
			// customGroupBox1
			// 
			this.customGroupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.customGroupBox1.Controls.Add(this.textBox1);
			this.customGroupBox1.Controls.Add(this._OptimizeCodeWindowBox);
			this.customGroupBox1.Controls.Add(this._OptimizeMainWindowBox);
			this.customGroupBox1.Location = new System.Drawing.Point(6, 96);
			this.customGroupBox1.Name = "customGroupBox1";
			this.customGroupBox1.Size = new System.Drawing.Size(552, 120);
			this.customGroupBox1.TabIndex = 1;
			this.customGroupBox1.TabStop = false;
			this.customGroupBox1.Text = "Force Grayscale Text Rendering";
			// 
			// textBox1
			// 
			this.textBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textBox1.Location = new System.Drawing.Point(6, 49);
			this.textBox1.Multiline = true;
			this.textBox1.Name = "textBox1";
			this.textBox1.ReadOnly = true;
			this.textBox1.Size = new System.Drawing.Size(537, 65);
			this.textBox1.TabIndex = 2;
			this.textBox1.Text = "Note: For best text rendering effects, it is recommended to use MacType, which co" +
    "uld be downloaded from: \r\nhttps://github.com/snowie2000/mactype/releases";
			// 
			// _OptimizeCodeWindowBox
			// 
			this._OptimizeCodeWindowBox.AutoSize = true;
			this._OptimizeCodeWindowBox.Location = new System.Drawing.Point(298, 24);
			this._OptimizeCodeWindowBox.Name = "_OptimizeCodeWindowBox";
			this._OptimizeCodeWindowBox.Size = new System.Drawing.Size(221, 19);
			this._OptimizeCodeWindowBox.TabIndex = 1;
			this._OptimizeCodeWindowBox.Text = "Apply to document window";
			this._OptimizeCodeWindowBox.UseVisualStyleBackColor = true;
			// 
			// _OptimizeMainWindowBox
			// 
			this._OptimizeMainWindowBox.AutoSize = true;
			this._OptimizeMainWindowBox.Location = new System.Drawing.Point(9, 24);
			this._OptimizeMainWindowBox.Name = "_OptimizeMainWindowBox";
			this._OptimizeMainWindowBox.Size = new System.Drawing.Size(189, 19);
			this._OptimizeMainWindowBox.TabIndex = 0;
			this._OptimizeMainWindowBox.Text = "Apply to main window";
			this._OptimizeMainWindowBox.UseVisualStyleBackColor = true;
			// 
			// groupBox1
			// 
			this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.groupBox1.Controls.Add(this._NoSpaceBetweenWrappedLinesBox);
			this.groupBox1.Controls.Add(this._BottomMarginBox);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Controls.Add(this._TopMarginBox);
			this.groupBox1.Controls.Add(this.label1);
			this.groupBox1.Location = new System.Drawing.Point(6, 6);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(552, 84);
			this.groupBox1.TabIndex = 0;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Extra Line Margins";
			// 
			// _NoSpaceBetweenWrappedLinesBox
			// 
			this._NoSpaceBetweenWrappedLinesBox.AutoSize = true;
			this._NoSpaceBetweenWrappedLinesBox.Location = new System.Drawing.Point(9, 55);
			this._NoSpaceBetweenWrappedLinesBox.Name = "_NoSpaceBetweenWrappedLinesBox";
			this._NoSpaceBetweenWrappedLinesBox.Size = new System.Drawing.Size(277, 19);
			this._NoSpaceBetweenWrappedLinesBox.TabIndex = 4;
			this._NoSpaceBetweenWrappedLinesBox.Text = "No margin between wrapped lines";
			this._NoSpaceBetweenWrappedLinesBox.UseVisualStyleBackColor = true;
			// 
			// _BottomMarginBox
			// 
			this._BottomMarginBox.Location = new System.Drawing.Point(313, 24);
			this._BottomMarginBox.Name = "_BottomMarginBox";
			this._BottomMarginBox.Size = new System.Drawing.Size(75, 25);
			this._BottomMarginBox.TabIndex = 3;
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(188, 26);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(119, 15);
			this.label2.TabIndex = 2;
			this.label2.Text = "Bottom margin:";
			// 
			// _TopMarginBox
			// 
			this._TopMarginBox.Location = new System.Drawing.Point(107, 24);
			this._TopMarginBox.Name = "_TopMarginBox";
			this._TopMarginBox.Size = new System.Drawing.Size(75, 25);
			this._TopMarginBox.TabIndex = 1;
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(6, 26);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(95, 15);
			this.label1.TabIndex = 0;
			this.label1.Text = "Top margin:";
			// 
			// _AboutPage
			// 
			this._AboutPage.Controls.Add(this.customGroupBox2);
			this._AboutPage.Location = new System.Drawing.Point(4, 25);
			this._AboutPage.Name = "_AboutPage";
			this._AboutPage.Padding = new System.Windows.Forms.Padding(3);
			this._AboutPage.Size = new System.Drawing.Size(564, 296);
			this._AboutPage.TabIndex = 2;
			this._AboutPage.Text = "About";
			this._AboutPage.UseVisualStyleBackColor = true;
			// 
			// customGroupBox2
			// 
			this.customGroupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.customGroupBox2.Controls.Add(this.label7);
			this.customGroupBox2.Controls.Add(this.label6);
			this.customGroupBox2.Controls.Add(this.label4);
			this.customGroupBox2.Controls.Add(this._DonationLinkLabel);
			this.customGroupBox2.Controls.Add(this._ReleaseLinkLabel);
			this.customGroupBox2.Controls.Add(this._GitHubLinkLabel);
			this.customGroupBox2.Controls.Add(this.label5);
			this.customGroupBox2.Location = new System.Drawing.Point(6, 6);
			this.customGroupBox2.Name = "customGroupBox2";
			this.customGroupBox2.Size = new System.Drawing.Size(552, 284);
			this.customGroupBox2.TabIndex = 0;
			this.customGroupBox2.TabStop = false;
			this.customGroupBox2.Text = "Thank you for using Codist";
			// 
			// label7
			// 
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(6, 75);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(127, 15);
			this.label7.TabIndex = 2;
			this.label7.Text = "Latest release:";
			// 
			// label6
			// 
			this.label6.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.label6.Location = new System.Drawing.Point(32, 165);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(495, 52);
			this.label6.TabIndex = 6;
			this.label6.Text = "Recommended donation value is $19.99. But you can modify the amount to any value " +
    "if you like.";
			// 
			// label4
			// 
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(6, 125);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(303, 15);
			this.label4.TabIndex = 4;
			this.label4.Text = "Support future development of Codist:";
			// 
			// _DonationLinkLabel
			// 
			this._DonationLinkLabel.AutoSize = true;
			this._DonationLinkLabel.Location = new System.Drawing.Point(32, 140);
			this._DonationLinkLabel.Name = "_DonationLinkLabel";
			this._DonationLinkLabel.Size = new System.Drawing.Size(143, 15);
			this._DonationLinkLabel.TabIndex = 5;
			this._DonationLinkLabel.TabStop = true;
			this._DonationLinkLabel.Text = "Donate via PayPal";
			this._DonationLinkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this._DonationLinkLabel_LinkClicked);
			// 
			// _ReleaseLinkLabel
			// 
			this._ReleaseLinkLabel.AutoSize = true;
			this._ReleaseLinkLabel.Location = new System.Drawing.Point(32, 90);
			this._ReleaseLinkLabel.Name = "_ReleaseLinkLabel";
			this._ReleaseLinkLabel.Size = new System.Drawing.Size(287, 15);
			this._ReleaseLinkLabel.TabIndex = 3;
			this._ReleaseLinkLabel.TabStop = true;
			this._ReleaseLinkLabel.Text = "github.com/wmjordan/Codist/releases";
			this._ReleaseLinkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this._ReleaseLinkLabel_LinkClicked);
			// 
			// _GitHubLinkLabel
			// 
			this._GitHubLinkLabel.AutoSize = true;
			this._GitHubLinkLabel.Location = new System.Drawing.Point(32, 49);
			this._GitHubLinkLabel.Name = "_GitHubLinkLabel";
			this._GitHubLinkLabel.Size = new System.Drawing.Size(215, 15);
			this._GitHubLinkLabel.TabIndex = 1;
			this._GitHubLinkLabel.TabStop = true;
			this._GitHubLinkLabel.Text = "github.com/wmjordan/Codist";
			this._GitHubLinkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this._GitHubLinkLabel_LinkClicked);
			// 
			// label5
			// 
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(6, 34);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(247, 15);
			this.label5.TabIndex = 0;
			this.label5.Text = "Report bugs and suggesions to:";
			// 
			// GeneralPage
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this.tabControl1);
			this.Name = "GeneralPage";
			this.Size = new System.Drawing.Size(575, 328);
			this.Load += new System.EventHandler(this.MiscPage_Load);
			this.tabControl1.ResumeLayout(false);
			this._GeneralPage.ResumeLayout(false);
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			this.groupBox3.ResumeLayout(false);
			this._BuildPage.ResumeLayout(false);
			this.customGroupBox4.ResumeLayout(false);
			this.customGroupBox4.PerformLayout();
			this.customGroupBox3.ResumeLayout(false);
			this.customGroupBox3.PerformLayout();
			this._DisplayPage.ResumeLayout(false);
			this.customGroupBox1.ResumeLayout(false);
			this.customGroupBox1.PerformLayout();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this._BottomMarginBox)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this._TopMarginBox)).EndInit();
			this._AboutPage.ResumeLayout(false);
			this.customGroupBox2.ResumeLayout(false);
			this.customGroupBox2.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.NumericUpDown _BottomMarginBox;
		private System.Windows.Forms.NumericUpDown _TopMarginBox;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.CheckBox _NoSpaceBetweenWrappedLinesBox;
		private System.Windows.Forms.CheckBox _ScrollbarMarkerBox;
		private System.Windows.Forms.CheckBox _SuperQuickInfoBox;
		private System.Windows.Forms.CheckBox _SyntaxHighlightBox;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.CheckBox _SmartBarBox;
		private System.Windows.Forms.Button _SaveConfigButton;
		private System.Windows.Forms.Button _LoadConfigButton;
		private System.Windows.Forms.CheckBox _CodeBarBox;
		private Controls.CustomGroupBox groupBox1;
		private Controls.CustomGroupBox groupBox2;
		private Controls.CustomGroupBox groupBox3;
		private System.Windows.Forms.TabControl tabControl1;
		private System.Windows.Forms.TabPage _GeneralPage;
		private System.Windows.Forms.TabPage _DisplayPage;
		private Controls.CustomGroupBox customGroupBox1;
		private System.Windows.Forms.CheckBox _OptimizeCodeWindowBox;
		private System.Windows.Forms.CheckBox _OptimizeMainWindowBox;
		private System.Windows.Forms.TextBox textBox1;
		private System.Windows.Forms.TabPage _AboutPage;
		private Controls.CustomGroupBox customGroupBox2;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.LinkLabel _DonationLinkLabel;
		private System.Windows.Forms.LinkLabel _GitHubLinkLabel;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.Label label6;
		private System.Windows.Forms.Label label7;
		private System.Windows.Forms.LinkLabel _ReleaseLinkLabel;
		private System.Windows.Forms.TabPage _BuildPage;
		private Controls.CustomGroupBox customGroupBox3;
		private System.Windows.Forms.CheckBox _BuildTimestampBox;
		private Controls.CustomGroupBox customGroupBox4;
		private System.Windows.Forms.CheckBox _IncrementVsixRevisionBox;
	}
}
