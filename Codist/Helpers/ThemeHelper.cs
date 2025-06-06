using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

namespace Codist
{
	static class ThemeHelper
	{
		static Guid __CurrentThemeId = InitThemeId();

		/// <summary>
		/// Event for theme changes.
		/// Parameter key is the GUID for the theme, and value is the theme name.
		/// </summary>
		internal static event EventHandler<EventArgs<KeyValuePair<Guid, string>>> ThemeChanged;

		static Guid InitThemeId() {
			VSColorTheme.ThemeChanged += OnThemeChanged;
			return GetCurrentThemeInfo().Key;
		}

		static KeyValuePair<Guid, string> GetCurrentThemeInfo() {
			var i = ServicesHelper.Get<Interop.IVsColorThemeService, Interop.SVsColorThemeService>();
			if (i == null) {
				"Failed to cast IVsColorThemeService.".Log();
				return CompatibleGetThemeInfo();
			}
			var t = i.CurrentTheme;
			$"Current theme: {t.Name} ({t.ThemeId})".Log();
			return new KeyValuePair<Guid, string>(t.ThemeId, t.Name);
		}

		// in VS 2022, SVsColorThemeService somehow can't be cast to IVsColorThemeService,
		// we have to use dynamic in this case
		static KeyValuePair<Guid, string> CompatibleGetThemeInfo() {
			dynamic s = ServiceProvider.GlobalProvider.GetService(new Guid("0D915B59-2ED7-472A-9DE8-9161737EA1C5"));
			if (s == null) {
				return new KeyValuePair<Guid, string>(Guid.Empty, String.Empty);
			}
			var t = s.CurrentTheme;
			$"Current theme: {t.Name} ({t.ThemeId})".Log();
			return new KeyValuePair<Guid, string>(t.ThemeId, t.Name);
		}

		static void OnThemeChanged(ThemeChangedEventArgs e) {
			("Theme changed: " + e.Message.ToText()).Log();
			var themeInfo = GetCurrentThemeInfo();
			var themeId = themeInfo.Key;
			if (themeId != __CurrentThemeId && themeId != Guid.Empty) {
				__CurrentThemeId = themeId;
				ThemeChanged?.Invoke(themeInfo, new EventArgs<KeyValuePair<Guid, string>>(themeInfo));
			}
		}
	}
}
