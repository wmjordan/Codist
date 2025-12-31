using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.Utilities;

namespace Codist
{
	sealed class WrapText
	{
		string _Prefix, _Suffix, _Pattern, _Substitution;
		char _Indicator;

		string[] _prefixLines;
		string[] _suffixLines;
		int[] _prefixTabCounts;
		int[] _suffixTabCounts;
		string[] _prefixWithoutLeadingTabs;
		string[] _suffixWithoutLeadingTabs;
		bool _hasPlaceholder;

		public const char DefaultIndicator = '$';

		public WrapText(string pattern, string name = null, char indicator = DefaultIndicator) {
			_Indicator = indicator;
			Pattern = pattern;
			Name = name;
		}

		public string Name { get; set; }

		public string Pattern {
			get => _Pattern;
			set {
				_Pattern = value;
				InternalUpdate();
			}
		}

		public char Indicator {
			get => _Indicator;
			set {
				_Indicator = value;
				InternalUpdate();
			}
		}

		internal string Prefix => _Prefix;
		internal string Suffix => _Suffix;
		internal string Substitution => _Substitution;
		internal bool HasMultilinePrefix => _prefixLines != null;
		internal bool HasMultilineSuffix => _suffixLines != null;
		internal bool HasPlaceholder => _hasPlaceholder;

		public string Wrap(string text) {
			return _Prefix
				+ text
				+ (_Substitution != null ? _Suffix.Replace(_Substitution, text) : _Suffix);
		}

		public string Wrap(string text, string startLineSpace, string endLineSpace, int indentStringSize, string newLineChar) {
			// 计算大致的容量，考虑到多行情况
			int estimatedCapacity = _Prefix.Length + _Suffix.Length + text.Length +
				(startLineSpace?.Length ?? 0 + endLineSpace?.Length ?? 0) *
				Math.Max(_prefixLines?.Length ?? 1, _suffixLines?.Length ?? 1);

			using var sbr = ReusableStringBuilder.AcquireDefault(estimatedCapacity);
			StringBuilder sb = sbr.Resource;

			// 处理前缀部分
			AppendParts(startLineSpace, indentStringSize, newLineChar, _Prefix, _prefixLines, _prefixTabCounts, _prefixWithoutLeadingTabs, sb);

			// 添加原始文本
			sb.Append(text);

			// 处理后缀部分
			string suffix = _Suffix;
			// 如果存在替换模式，先进行替换
			if (_Substitution != null) {
				suffix = suffix.Replace(_Substitution, text);
				// 替换后需要重新计算多行后缀
				if (_suffixLines != null) {
					ProcessSuffixForMultiline(sb, suffix, endLineSpace, indentStringSize, newLineChar);
				}
				else {
					sb.Append(suffix);
				}
			}
			else {
				AppendParts(endLineSpace, indentStringSize, newLineChar, suffix, _suffixLines, _suffixTabCounts, _suffixWithoutLeadingTabs, sb);
			}
			return sb.ToString();
		}

		static void AppendParts(string startLineSpace, int indentStringSize, string newLineChar, string fullContent, string[] lines, int[] tabCounts, string[] contentLines, StringBuilder sb) {
			if (lines == null) {
				sb.Append(fullContent);
				return;
			}
			for (int i = 0; i < lines.Length; i++) {
                if (i == 0) {
                    sb.Append(lines[i]);
					continue;
                }
                sb.Append(newLineChar);
                sb.Append(startLineSpace);

                // 处理行首的制表符
                if (indentStringSize >= 0 && tabCounts[i] > 0) {
                    // 使用预计算的行内容和制表符数量
                    sb.Append(' ', tabCounts[i] * indentStringSize);
                    sb.Append(contentLines[i]);
                }
                else {
                    sb.Append(lines[i]);
                }
            }
		}

		public IEnumerable<SnapshotSpan> WrapInView(ITextView view) {
			var modified = new Chain<Span>();
			var prefix = Prefix;
			var suffix = Suffix;
			var substitution = Substitution;
			var psLength = prefix.Length + suffix.Length;
			var offset = 0;
			int strippedLength;
			string replacement = null;
			using (var edit = view.TextSnapshot.TextBuffer.CreateEdit()) {
				foreach (var item in view.Selection.SelectedSpans) {
					var t = item.GetText();
					// remove surrounding items
					if (substitution == null
						&& (strippedLength = item.Length - psLength) > 0
						&& t.StartsWith(prefix, StringComparison.Ordinal)
						&& t.EndsWith(suffix, StringComparison.Ordinal)
						&& t.IndexOf(prefix, prefix.Length, strippedLength) <= t.IndexOf(suffix, prefix.Length, strippedLength)) {
						if (edit.Replace(item, t.Substring(prefix.Length, strippedLength))) {
							modified.Add(new Span(item.Start.Position + offset, strippedLength));
							offset -= psLength;
						}
						continue;
					}
					// surround items
					replacement = HasMultilinePrefix || HasMultilineSuffix ? Wrap(t, HasMultilinePrefix ? view.TextSnapshot.GetLinePrecedingWhitespaceAtPosition(item.Start.Position) : null, HasMultilineSuffix ? view.TextSnapshot.GetLinePrecedingWhitespaceAtPosition(item.End.Position) : null, view.Options.GetIndentStringSize(), view.Options.GetNewLineCharacter()) : Wrap(t);
					if (edit.Replace(item, replacement)) {
						modified.Add(new Span(item.Start.Position + offset, replacement.Length));
						offset += replacement.Length - t.Length;
					}
					if (HasPlaceholder && edit.HasEffectiveChanges) {
						edit.Apply();
						var session = new SnippetTexts.SnippetSession((IWpfTextView)view, replacement, item.Start.Position);
						return null;
					}
				}
				if (edit.HasEffectiveChanges) {
					var snapshot = edit.Apply();
					return modified.Select(i => new SnapshotSpan(snapshot, i));
				}
			}
			return Enumerable.Empty<SnapshotSpan>();
		}

		// 处理替换后的多行后缀
		static void ProcessSuffixForMultiline(StringBuilder sb, string suffix, string endLineSpace, int indentStringSize, string newLineChar) {
			string[] suffixLines = suffix.Split('\n');
			for (int i = 0; i < suffixLines.Length; i++) {
				if (i > 0) {
					sb.Append(newLineChar);
					sb.Append(endLineSpace);

					// 处理行首的制表符
					if (indentStringSize >= 0) {
						string line = suffixLines[i];
						int tabCount = CountLeadingTabs(line);
						if (tabCount > 0) {
							sb.Append(' ', tabCount * indentStringSize);
							sb.Append(line, tabCount, line.Length - tabCount);
						}
						else {
							sb.Append(line);
						}
					}
					else {
						sb.Append(suffixLines[i]);
					}
				}
				else {
					sb.Append(suffixLines[i]);
				}
			}
		}

		// 计算行首的制表符数量
		static int CountLeadingTabs(string line) {
			int count = 0;
			while (count < line.Length && line[count] == '\t') {
				count++;
			}
			return count;
		}

		void InternalUpdate() {
			int p;
			if (_Pattern != null && (p = _Pattern.IndexOf(_Indicator)) >= 0) {
				_Prefix = _Pattern.Substring(0, p).Replace("\r\n", "\n");
				_Suffix = _Pattern.Substring(p + 1).Replace("\r\n", "\n");
			}
			else {
				_Prefix = _Pattern?.Replace("\r\n", "\n") ?? String.Empty;
				_Suffix = String.Empty;
			}

			_Substitution = _Suffix.Contains(_Indicator) ? _Indicator.ToString() : null;

			// 预计算多行前缀
			UpdateMultilineData(_Prefix, _Prefix.Contains('\n'), ref _prefixLines, ref _prefixTabCounts, ref _prefixWithoutLeadingTabs);

			// 预计算多行后缀（只有当不包含替换时才预计算）
			if (_Substitution == null) {
				UpdateMultilineData(_Suffix, _Suffix.Contains('\n'), ref _suffixLines, ref _suffixTabCounts, ref _suffixWithoutLeadingTabs);
			}
			else {
				ClearMultilineData(ref _suffixLines, ref _suffixTabCounts, ref _suffixWithoutLeadingTabs);
			}

			_hasPlaceholder = _Prefix.Contains("[[") || _Suffix.Contains("[[");
		}

		// 更新多行数据的辅助方法
		static void UpdateMultilineData(string text, bool isMultiline,
			ref string[] lines, ref int[] tabCounts, ref string[] withoutTabs) {

			if (isMultiline) {
				lines = text.Split('\n');
				tabCounts = new int[lines.Length];
				withoutTabs = new string[lines.Length];

				for (int i = 0; i < lines.Length; i++) {
					int tabCount = CountLeadingTabs(lines[i]);
					tabCounts[i] = tabCount;
					withoutTabs[i] = tabCount > 0 ?
						lines[i].Substring(tabCount) : lines[i];
				}
			}
			else {
				ClearMultilineData(ref lines, ref tabCounts, ref withoutTabs);
			}
		}

		// 清空多行数据的辅助方法
		static void ClearMultilineData(ref string[] linesArray,
			ref int[] tabCountsArray, ref string[] withoutTabsArray) {

			linesArray = null;
			tabCountsArray = null;
			withoutTabsArray = null;
		}
	}
}