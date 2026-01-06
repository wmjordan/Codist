using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.Utilities;

namespace Codist.SnippetTexts
{
	readonly struct PlaceholderInfo(string name, int position, int length)
	{
		public string Name { get; } = name;
		public int Position { get; } = position;
		public int Length { get; } = length;
	}

	sealed class WrapText
	{
		string _Pattern;
		char _Indicator;

		string _Prefix;
		string _Suffix;
		string _Substitution;

		List<ISnippetCommand> _prefixCommands;
		List<ISnippetCommand> _suffixCommands;

		readonly List<PlaceholderInfo> _placeholderInfos = new List<PlaceholderInfo>();
		bool _IsSimple;

		public const char DefaultIndicator = '$';

		public WrapText(string pattern, string name = null, char indicator = DefaultIndicator) {
			_Indicator = indicator;
			Pattern = pattern;
			Name = name;
		}

		public string Name { get; set; }

		public char Indicator {
			get => _Indicator;
			set => _Indicator = value;
		}

		public string Pattern {
			get => _Pattern;
			set {
				_Pattern = value;
				InternalUpdate();
			}
		}

		internal string Prefix => _Prefix;
		internal string Suffix => _Suffix;
		internal bool HasSubstitution => _Substitution != null;
		internal bool HasMultilinePrefix => _prefixCommands?.Any(c => c.Type == CommandType.NewLine) == true;
		internal bool HasMultilineSuffix => _suffixCommands?.Any(c => c.Type == CommandType.NewLine) == true;
		internal List<PlaceholderInfo> PlaceholderInfos => _placeholderInfos;

		void InternalUpdate() {
			int splitPos = _Pattern != null ? _Pattern.IndexOf(_Indicator) : -1;
			_Prefix = splitPos >= 0 ? _Pattern.Substring(0, splitPos) : _Pattern;
			_Suffix = splitPos >= 0 ? _Pattern.Substring(splitPos + 1) : string.Empty;

			bool hasNewline = _Pattern.Contains('\n');
			bool hasPlaceholder = _Pattern.Contains("[[");
			_Substitution = _Suffix.Contains(_Indicator) ? _Indicator.ToString() : null;

			if (_IsSimple = !hasNewline && !hasPlaceholder && _Substitution == null) {
				_prefixCommands = _suffixCommands = null;
				return;
			}

			_prefixCommands = new List<ISnippetCommand>();
			_suffixCommands = new List<ISnippetCommand>();

			ParseToCommands(_Prefix?.Replace("\r\n", "\n"), _prefixCommands);
			ParseToCommands(_Suffix?.Replace("\r\n", "\n"), _suffixCommands);
		}

		void ParseToCommands(string text, List<ISnippetCommand> commands) {
			if (string.IsNullOrEmpty(text)) return;

			var lines = text.Split('\n');
			for (int i = 0; i < lines.Length; i++) {
				string line = lines[i];
				int lineIndex = 0;

				int tabCount = 0;
				while (lineIndex < line.Length && line[lineIndex] == '\t') {
					tabCount++;
					lineIndex++;
				}
				if (tabCount > 0) {
					commands.Add(new IndentCommand(tabCount));
				}

				while (lineIndex < line.Length) {
					#region Check placeholders
					if (lineIndex + 1 < line.Length && line[lineIndex] == '[' && line[lineIndex + 1] == '[') {
						int endBracket = line.IndexOf("]]", lineIndex + 2);
						if (endBracket > lineIndex) {
							string name = line.Substring(lineIndex + 2, endBracket - lineIndex - 2);
							commands.Add(new PlaceholderCommand(name));
							lineIndex = endBracket + 2;
							continue;
						}
					}
					#endregion

					if (line[lineIndex] == _Indicator) {
						commands.Add(SelectionCommand.Instance);
						lineIndex++;
						continue;
					}

					int textStart = lineIndex;
					while (lineIndex < line.Length) {
						if ((lineIndex + 1 < line.Length && line[lineIndex] == '[' && line[lineIndex + 1] == '[')
							|| line[lineIndex] == _Indicator) {
							break;
						}
						lineIndex++;
					}

					if (lineIndex > textStart) {
						commands.Add(new TextCommand(line.Substring(textStart, lineIndex - textStart)));
					}
				}

				if (i < lines.Length - 1) {
					commands.Add(NewLineCommand.Instance);
				}
			}
		}

		public string Wrap(string text) {
			return _Prefix + text + (_Substitution != null ? _Suffix.Replace(_Substitution, text) : _Suffix);
		}

		public string Wrap(string text, string startLineSpace, string endLineSpace, int indentStringSize, string newLineChar) {
			_placeholderInfos.Clear();

			using var sbr = ReusableStringBuilder.AcquireDefault(text.Length + _Pattern.Length);
			StringBuilder sb = sbr.Resource;

			ExecuteCommands(_prefixCommands, sb, startLineSpace, indentStringSize, newLineChar, null);

			int selectionStartPos = sb.Length;
			sb.Append(text);
			int selectionEndPos = sb.Length;

			ExecuteCommands(_suffixCommands, sb, endLineSpace, indentStringSize, newLineChar, text);

			return sb.ToString();
		}

		void ExecuteCommands(List<ISnippetCommand> commands, StringBuilder sb, string linePrefix, int indentSize, string newLineChar, string selectionText) {
			foreach (var cmd in commands) {
				switch (cmd.Type) {
					case CommandType.Text:
						sb.Append(((TextCommand)cmd).Content);
						break;
					case CommandType.Indent:
						var ic = (IndentCommand)cmd;
						if (indentSize > 0 && ic.TabCount > 0) {
							sb.Append(' ', ic.TabCount * indentSize);
						}
						break;
					case CommandType.NewLine:
						sb.Append(newLineChar);
						if (!string.IsNullOrEmpty(linePrefix)) {
							sb.Append(linePrefix);
						}
						break;
					case CommandType.Selection:
						if (selectionText != null) {
							sb.Append(selectionText);
						}
						break;
					case CommandType.Placeholder:
						int pos = sb.Length;
						string name = ((PlaceholderCommand)cmd).Name;
						sb.Append(name);
						_placeholderInfos.Add(new PlaceholderInfo(name, pos, name.Length));
						break;
				}
			}
		}

		public IEnumerable<SnapshotSpan> WrapInView(ITextView view) {
			return _IsSimple ? SimpleWrapOrUnwrap(view) : ComplexWrap(view);
		}

		IEnumerable<SnapshotSpan> SimpleWrapOrUnwrap(ITextView view) {
			var offset = 0;
			string replacement = null;
			var modified = new Chain<Span>();
			using (var edit = view.TextSnapshot.TextBuffer.CreateEdit()) {
				foreach (var item in view.Selection.SelectedSpans) {
					var t = item.GetText();
					int strippedLength;
					var psLength = _Prefix.Length + _Suffix.Length;
					if ((strippedLength = item.Length - psLength) > 0
						&& t.StartsWith(_Prefix, StringComparison.Ordinal)
						&& t.EndsWith(_Suffix, StringComparison.Ordinal)
						&& t.IndexOf(_Prefix, _Prefix.Length, strippedLength) <= t.IndexOf(_Suffix, _Prefix.Length, strippedLength)) {
						// unwrap
						if (edit.Replace(item, t.Substring(_Prefix.Length, strippedLength))) {
							modified.Add(new Span(item.Start.Position + offset, strippedLength));
							offset -= psLength;
						}
					}
					else {
						// wrap
						replacement = Wrap(t);
						if (edit.Replace(item, replacement)) {
							modified.Add(new Span(item.Start.Position + offset, replacement.Length));
							offset += replacement.Length - t.Length;
						}
					}
				}
				if (edit.HasEffectiveChanges) {
					var snapshot = edit.Apply();
					return modified.Select(i => new SnapshotSpan(snapshot, i));
				}
			}
			return Enumerable.Empty<SnapshotSpan>();
		}

		IEnumerable<SnapshotSpan> ComplexWrap(ITextView view) {
			var offset = 0;
			string replacement = null;
			var modified = new Chain<Span>();
			using (var edit = view.TextSnapshot.TextBuffer.CreateEdit()) {
				foreach (var item in view.Selection.SelectedSpans) {
					var t = item.GetText();
					int start = item.Start.Position;

					replacement = Wrap(
						t,
						HasMultilinePrefix ? view.TextSnapshot.GetLinePrecedingWhitespaceAtPosition(start) : null,
						HasMultilineSuffix ? view.TextSnapshot.GetLinePrecedingWhitespaceAtPosition(item.End.Position) : null,
						view.Options.GetIndentStringSize(),
						view.Options.GetNewLineCharacter()
					);

					if (edit.Replace(item, replacement)) {
						modified.Add(new Span(start + offset, replacement.Length));
						offset += replacement.Length - t.Length;
					}

					if (edit.HasEffectiveChanges) {
						edit.Apply();
						if (_placeholderInfos.Count != 0) {
							if (view.TryGetProperty(out SnippetSession session)) {
								session.Terminate();
							}
							view.Properties[typeof(SnippetSession)] = new SnippetSession((IWpfTextView)view, _placeholderInfos, start, ServicesHelper.Instance.TextUndoHistory.GetHistory(view.TextBuffer));
						}
						return null;
					}
				}
			}
			return Enumerable.Empty<SnapshotSpan>();

		}

		interface ISnippetCommand {
			CommandType Type { get; }
		}
		sealed class TextCommand(string content) : ISnippetCommand
		{
			public CommandType Type => CommandType.Text;
			public readonly string Content = content;
		}
		sealed class PlaceholderCommand(string name) : ISnippetCommand
		{
			public CommandType Type => CommandType.Placeholder;
			public readonly string Name = name;
		}
		sealed class SelectionCommand : ISnippetCommand
		{
			public CommandType Type => CommandType.Selection;
			private SelectionCommand() { }
			public static readonly SelectionCommand Instance = new();
		}
		sealed class NewLineCommand : ISnippetCommand
		{
			public CommandType Type => CommandType.NewLine;
			private NewLineCommand() { }
			public static readonly NewLineCommand Instance = new();
		}
		sealed class IndentCommand(int tabCount) : ISnippetCommand
		{
			public CommandType Type => CommandType.Indent;
			public readonly int TabCount = tabCount;
		}

		enum CommandType
		{
			Text,
			Placeholder,
			Selection,
			NewLine,
			Indent
		}
	}
}
