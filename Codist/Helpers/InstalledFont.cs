using System;
using System.Linq;
using System.Windows.Media;

namespace Codist
{
	sealed class InstalledFont
	{
		internal static readonly string SystemLang = System.Globalization.CultureInfo.CurrentCulture.IetfLanguageTag;
		FamilyTypeface[] _ExtraTypefaces;

		public InstalledFont(FontFamily font) {
			Font = font;
			foreach (var item in Font.FamilyNames) {
				if (String.Equals(item.Key.IetfLanguageTag, SystemLang, StringComparison.OrdinalIgnoreCase)) {
					Name = item.Value;
					break;
				}
			}
			if (Name == null) {
				Name = Font.Source;
			}
		}
		public FontFamily Font { get; }
		public string Name { get; }
		public FamilyTypeface[] ExtraTypefaces => _ExtraTypefaces ?? (_ExtraTypefaces = Font.FamilyTypefaces.Where(i => i.IsStandardStyle() == false).ToArray());

		public override string ToString() {
			return Name;
		}
	}
}
