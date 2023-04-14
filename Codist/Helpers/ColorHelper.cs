using System;
using System.Collections.Generic;
using System.Windows.Media;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using GdiColor = System.Drawing.Color;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfColors = System.Windows.Media.Colors;

namespace Codist
{
	/// <summary>
	/// Used by symbol analysis tools and super quick info to provide color previews
	/// </summary>
	static class ColorHelper
	{
		static readonly Type __ObjectType = typeof(object);
		static readonly Type __ThemeResourceKeyType = typeof(ThemeResourceKey);

		static class NamedColorCache
		{
			static readonly Dictionary<string, SolidColorBrush> __Cache = GetBrushes();
			static readonly Dictionary<string, Func<SolidColorBrush>> __SystemColors = GetSystemColors();
			internal static SolidColorBrush GetBrush(string name) {
				UIHelper.ParseColor(name, out var c, out var a);
				if (c != WpfColors.Transparent) {
					return new SolidColorBrush(a == 0 ? c : c.Alpha(a));
				}
				var l = name.Length;
				return l >= 3 && l <= 20 && __Cache.TryGetValue(name, out var brush) ? brush : null;
			}
			internal static SolidColorBrush GetSystemBrush(string name) {
				return __SystemColors.TryGetValue(name, out var func) ? func() : null;
			}
			static Dictionary<string, SolidColorBrush> GetBrushes() {
				var c = Array.FindAll(typeof(WpfBrushes).GetProperties(), p => p.PropertyType == typeof(SolidColorBrush));
				var d = new Dictionary<string, SolidColorBrush>(c.Length, StringComparer.OrdinalIgnoreCase);
				foreach (var item in c) {
					d.Add(item.Name, item.GetValue(null) as SolidColorBrush);
				}
				return d;
			}
			static Dictionary<string, Func<SolidColorBrush>> GetSystemColors() {
				var c = Array.FindAll(typeof(System.Windows.SystemColors).GetProperties(), p => p.PropertyType == typeof(SolidColorBrush) || p.PropertyType == typeof(WpfColor));
				var d = new Dictionary<string, Func<SolidColorBrush>>(c.Length, StringComparer.OrdinalIgnoreCase);
				foreach (var item in c) {
					if (item.PropertyType == typeof(SolidColorBrush)) {
						d.Add(item.Name, (Func<SolidColorBrush>)item.GetGetMethod(false).CreateDelegate(typeof(Func<SolidColorBrush>)));
					}
					else {
						var getColor = (Func<WpfColor>)item.GetGetMethod(false).CreateDelegate(typeof(Func<WpfColor>));
						d.Add(item.Name, () => new SolidColorBrush(getColor()));
					}
				}
				c = typeof(System.Drawing.SystemColors).GetProperties();
				foreach (var item in c) {
					var getColor = (Func<GdiColor>)item.GetGetMethod(false).CreateDelegate(typeof(Func<GdiColor>));
					d.Add(item.Name, () => new SolidColorBrush(getColor().ToWpfColor()));
				}
				return d;
			}
		}
		public static SolidColorBrush GetSystemBrush(string symbolName) {
			return NamedColorCache.GetSystemBrush(symbolName);
		}

		public static SolidColorBrush GetBrush(ISymbol symbol, bool includeVsColors) {
			var n = symbol.ContainingType?.Name;
			switch (n) {
				case nameof(System.Windows.SystemColors):
				case nameof(System.Drawing.SystemBrushes):
				case nameof(System.Drawing.SystemPens):
					return NamedColorCache.GetSystemBrush(symbol.Name);
				case nameof(System.Drawing.KnownColor):
					return NamedColorCache.GetBrush(symbol.Name) ?? NamedColorCache.GetSystemBrush(symbol.Name);
				case nameof(System.Drawing.Color):
				case nameof(System.Drawing.Brushes):
				case nameof(System.Drawing.Pens):
				case nameof(Colors):
					return NamedColorCache.GetBrush(symbol.Name);
			}
			if (includeVsColors) {
				switch (n) {
					case nameof(EnvironmentColors):
						return GetVsThemeBrush(typeof(EnvironmentColors), symbol.Name);
					case nameof(CommonDocumentColors):
						return GetVsThemeBrush(typeof(CommonDocumentColors), symbol.Name);
					case nameof(CommonControlsColors):
						return GetVsThemeBrush(typeof(CommonControlsColors), symbol.Name);
					case nameof(InfoBarColors):
						return GetVsThemeBrush(typeof(InfoBarColors), symbol.Name);
					case nameof(StartPageColors):
						return GetVsThemeBrush(typeof(StartPageColors), symbol.Name);
					case nameof(HeaderColors):
						return GetVsThemeBrush(typeof(HeaderColors), symbol.Name);
					case nameof(ThemedDialogColors):
						return GetVsThemeBrush(typeof(ThemedDialogColors), symbol.Name);
					case nameof(ProgressBarColors):
						return GetVsThemeBrush(typeof(ProgressBarColors), symbol.Name);
					case nameof(SearchControlColors):
						return GetVsThemeBrush(typeof(SearchControlColors), symbol.Name);
					case nameof(TreeViewColors):
						return GetVsThemeBrush(typeof(TreeViewColors), symbol.Name);
					case nameof(VsColors):
						return GetVsResourceColor(typeof(VsColors), symbol.Name);
					case nameof(VsBrushes):
						return GetVsResourceBrush(typeof(VsBrushes), symbol.Name);
				}
			}
			return null;
		}
		public static SolidColorBrush GetBrush(string color) {
			return NamedColorCache.GetBrush(color);
		}

		public static SolidColorBrush GetVsResourceBrush(Type type, string name) {
			var p = type.GetProperty(name, __ObjectType)?.GetValue(null);
			return p == null
				? null
				: System.Windows.Application.Current.Resources.Get<SolidColorBrush>(p);
		}
		public static SolidColorBrush GetVsResourceColor(Type type, string name) {
			var p = type.GetProperty(name, __ObjectType)?.GetValue(null);
			return p == null
				? null
				: new SolidColorBrush(System.Windows.Application.Current.Resources.Get<WpfColor>(p));
		}

		public static SolidColorBrush GetVsThemeBrush(Type type, string name) {
			var p = type.GetProperty(name, __ThemeResourceKeyType);
			return (p?.GetValue(null) as ThemeResourceKey)?.GetWpfBrush();
		}

		public static bool IsDark(this WpfColor color) {
			return (299 * color.R + 587 * color.G + 114 * color.B) / 1000 < 128;
		}
	}
}
