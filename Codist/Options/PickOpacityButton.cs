using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Codist.Options
{
	class PickOpacityButton : Button
	{
		const string _Title = "Opacity";
		ContextMenuStrip _ContextMenu;
		byte _Value;

		public PickOpacityButton() {
			Text = _Title;
		}

		public byte Value {
			get => _Value;
			set {
				_Value = value;
				Text = value == 0 ? "Opacity not set" : "Opacity: " + ((value + 1) / 16).ToString();
			}
		}

		protected override void OnClick(EventArgs e) {
			if (_ContextMenu == null) {
				_ContextMenu = new ContextMenuStrip() {
					RenderMode = ToolStripRenderMode.System,
					ShowCheckMargin = true,
					ShowImageMargin = false,
					Items = {
						new ToolStripMenuItem("Default") { Tag = 0 }
					}
				};
				var items = new ToolStripMenuItem[16];
				for (int i = 16; i > 0; i--) {
					items[16 - i] = new ToolStripMenuItem(i.ToString()) { Tag = i * 16 - 1 };
				}
				_ContextMenu.Items.AddRange(items);
				_ContextMenu.ItemClicked += (s, args) => {
					Value = (byte)(int)args.ClickedItem.Tag;
					base.OnClick(EventArgs.Empty);
				};
				_ContextMenu.Opening += (s, args) => {
					var m = s as ContextMenuStrip;
					foreach (var item in m.Items) {
						var i = item as ToolStripMenuItem;
						if (i != null) {
							i.Checked = false;
						}
					}
					(m.Items[Value == 0 ? 0 : 1 + 16 - (Value + 1) / 16] as ToolStripMenuItem).Checked = true;
				};
			}
			_ContextMenu.Show(this, 1, Height - 1);
		}

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			if (_ContextMenu != null) {
				_ContextMenu.Dispose();
			}
		}
	}
}
