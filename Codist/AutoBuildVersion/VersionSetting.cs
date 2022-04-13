using System;
using Newtonsoft.Json;

namespace Codist.AutoBuildVersion
{
	public sealed class VersionSetting
	{
		const int MaxVersionNumber = 65535;

		static readonly IFormatProvider __Format = System.Globalization.CultureInfo.InvariantCulture;
		public VersionRewriteMode Major { get; set; }
		public VersionRewriteMode Minor { get; set; }
		public VersionRewriteMode Build { get; set; }
		public VersionRewriteMode Revision { get; set; }
		[JsonIgnore]
		public bool ShouldRewrite => Revision != 0 || Build != 0 || Minor != 0 || Major != 0;

		public VersionSetting() {}
		public VersionSetting(VersionRewriteMode major, VersionRewriteMode minor, VersionRewriteMode build, VersionRewriteMode revision) {
			Major = major;
			Minor = minor;
			Build = build;
			Revision = revision;
		}

		public string Rewrite(string major, string minor, string build, string revision) {
			return $"{WritePart(major, Major)}.{WritePart(minor, Minor)}.{WritePart(build, Build)}.{WritePart(revision, Revision)}";
		}

		static string WritePart(string part, VersionRewriteMode mode) {
			switch (mode) {
				case VersionRewriteMode.Increment: return Int32.TryParse(part, System.Globalization.NumberStyles.Integer, __Format, out var n) ? NormalizeNumber(++n) : part;
				case VersionRewriteMode.Zero: return "0";
				case VersionRewriteMode.Year: return DateTime.Now.Year.ToText();
				case VersionRewriteMode.Month: return DateTime.Now.Month.ToText();
				case VersionRewriteMode.Day: return DateTime.Now.Day.ToText();
				case VersionRewriteMode.ShortYear: return (DateTime.Now.Year % 100).ToText();
				case VersionRewriteMode.YearMonth: return (DateTime.Now.Year % 100).ToText() + DateTime.Now.Month.ToString("00", __Format);
				case VersionRewriteMode.MonthDay: return DateTime.Now.Month.ToText() + DateTime.Now.Day.ToString("00", __Format);
				case VersionRewriteMode.DayOfYear: return DateTime.Now.DayOfYear.ToText();
				case VersionRewriteMode.Hour: return DateTime.Now.Hour.ToText();
				case VersionRewriteMode.HourMinute: return DateTime.Now.Hour.ToText() + DateTime.Now.Minute.ToString("00", __Format);
				case VersionRewriteMode.DaySinceY2K: return NormalizeNumber((int)(DateTime.Now.Date - new DateTime(2000, 1, 1)).TotalDays);
				case VersionRewriteMode.MidNightSecond: return ((int)((DateTime.Now - DateTime.Today).TotalSeconds / 2)).ToText();
				default: return part;
			}
		}

		public override string ToString() {
			return $"{Major}.{Minor}.{Build}.{Revision}";
		}

		static string NormalizeNumber(int n) {
			return (n > MaxVersionNumber ? MaxVersionNumber : n < 0 ? 0 : n).ToText();
		}
	}
}
