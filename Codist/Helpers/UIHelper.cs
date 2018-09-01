using System;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.Text.Classification;
using GdiColor = System.Drawing.Color;

namespace Codist
{
	static class UIHelper
	{
		public static GdiColor Alpha(this GdiColor color, byte alpha) {
			return GdiColor.FromArgb(alpha, color.R, color.G, color.B);
		}

		public static string ToHexString(this GdiColor color) {
			return "#" + color.A.ToString("X2") + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
		}

		public static TabControl AddPage(this TabControl tabs, string name, Control pageContent, bool prepend) {
			var page = new TabPage(name) { UseVisualStyleBackColor = true };
			if (prepend) {
				tabs.TabPages.Insert(0, page);
				tabs.SelectedIndex = 0;
			}
			else {
				tabs.TabPages.Add(page);
			}
			pageContent.Dock = DockStyle.Fill;
			page.Controls.Add(pageContent);
			return tabs;
		}
		public static string GetClassificationType(this Type type, string field) {
			var f = type.GetField(field);
			var d = f.GetCustomAttribute<ClassificationTypeAttribute>();
			return d?.ClassificationTypeNames;
		}
	}
}
