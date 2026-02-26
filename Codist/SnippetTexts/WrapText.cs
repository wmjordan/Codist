using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.Utilities;
using R = Codist.Properties.Resources;

namespace Codist.SnippetTexts;

readonly struct PlaceholderInfo(string name, int position, int length)
{
	public string Name { get; } = name;
	public int Position { get; } = position;
	public int Length { get; } = length;

	public SnapshotSpan ToSnapshotSpan(ITextSnapshot snapshot) {
		return new SnapshotSpan(snapshot, Position, Length);
	}
}

sealed class WrapText
{
	string _Pattern;
	char _Indicator;

	string _Prefix;
	string _Suffix;
	string _Substitution;

	int _PlaceholderCount;
	bool _IsSimple, _HasMultilinePrefix, _HasMultilineSuffix;
	List<ISnippetCommand> _Commands;
	Func<ITextView, IEnumerable<SnapshotSpan>> _WrapAction;

	public const char DefaultIndicator = '$';

	public WrapText(string pattern, string name = null, char indicator = DefaultIndicator) {
		_Indicator = indicator;
		Pattern = pattern;
		Name = name;
		_WrapAction = UndeterminedWrapAction;
	}

	public string Name { get; set; }

	public char Indicator {
		get => _Indicator;
		set {
			if (_Indicator != value) {
				_Indicator = value;
				_WrapAction = UndeterminedWrapAction;
			}
		}
	}

	public string Pattern {
		get => _Pattern;
		set {
			value ??= String.Empty;
			if (_Pattern != value) {
				_Pattern = value;
				_WrapAction = UndeterminedWrapAction;
			}
		}
	}

	public IEnumerable<SnapshotSpan> WrapSelections(ITextView view) {
		return _WrapAction(view);
	}

	public static IEnumerable<SnapshotSpan> Wrap(ITextView view, string prefix, string suffix) {
		return WrapOrUnwrap(view, prefix + suffix, prefix, suffix, null);
	}

	IEnumerable<SnapshotSpan> UndeterminedWrapAction(ITextView view) {
		// we have not yet parsed the pattern, do it here
		ParsePattern();
		// set the delegate to appropriated method,
		// so we don't need to parse and branch control flow again
		return (_WrapAction = _IsSimple ? SimpleWrap : ComplexWrap)(view);
	}

	void ParsePattern() {
		int splitPos = _Pattern.Length != 0 ? _Pattern.IndexOf(_Indicator) : -1;
		_Prefix = splitPos >= 0 ? _Pattern.Substring(0, splitPos) : _Pattern;
		_Suffix = splitPos >= 0 ? _Pattern.Substring(splitPos + 1) : string.Empty;

		bool hasNewline = _Pattern.Contains('\n');
		bool hasPlaceholder = _Pattern.Contains("[[");
		_Substitution = _Suffix.Contains(_Indicator) ? _Indicator.ToString() : null;

		if (_IsSimple = !hasNewline && !hasPlaceholder && _Substitution == null) {
			_Commands = null;
			_HasMultilinePrefix = _HasMultilineSuffix = false;
			_PlaceholderCount = 0;
			return;
		}

		_Commands = new List<ISnippetCommand>();
		int placeholderCount = 0;

		ParseToCommands(_Prefix?.Replace("\r\n", "\n"), _Commands, ref placeholderCount, ref _HasMultilinePrefix);
		_Commands.Add(SelectionCommand.Instance);
		ParseToCommands(_Suffix?.Replace("\r\n", "\n"), _Commands, ref placeholderCount, ref _HasMultilineSuffix);

		_PlaceholderCount = placeholderCount;
	}

	void ParseToCommands(string text, List<ISnippetCommand> commands, ref int placeholderCount, ref bool multiline) {
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
						++placeholderCount;
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
				multiline = true;
			}
		}
	}

	IEnumerable<SnapshotSpan> SimpleWrap(ITextView view) {
		return WrapOrUnwrap(view, Name, _Prefix, _Suffix, _Substitution);
	}

	static IEnumerable<SnapshotSpan> WrapOrUnwrap(ITextView view, string name, string prefix, string suffix, string substitution) {
		var offset = 0;
		string replacement = null;
		var modified = new Chain<Span>();
		var undoHistory = ServicesHelper.Instance.TextUndoHistory.GetHistory(view.TextBuffer);
		using (var tran = undoHistory.CreateTransaction(R.T_WrapTextName.Replace("<NAME>", name))) {
			var eo = ServicesHelper.Instance.EditorOperationsFactory.GetEditorOperations(view);
			eo.AddBeforeTextBufferChangePrimitive();
			using var edit = view.TextSnapshot.TextBuffer.CreateEdit();
			foreach (var item in view.Selection.SelectedSpans) {
				var t = item.GetText();
				int strippedLength;
				var psLength = prefix.Length + suffix.Length;
				if ((strippedLength = item.Length - psLength) > 0
					&& t.StartsWith(prefix, StringComparison.Ordinal)
					&& t.EndsWith(suffix, StringComparison.Ordinal)
					&& t.IndexOf(prefix, prefix.Length, strippedLength) <= t.IndexOf(suffix, prefix.Length, strippedLength)) {
					// unwrap
					if (edit.Replace(item, t.Substring(prefix.Length, strippedLength))) {
						modified.Add(new Span(item.Start.Position + offset, strippedLength));
						offset -= psLength;
					}
				}
				else {
					// wrap
					replacement = prefix + t + (substitution != null ? suffix.Replace(substitution, t) : suffix);
					if (edit.Replace(item, replacement)) {
						modified.Add(new Span(item.Start.Position + offset, replacement.Length));
						offset += replacement.Length - t.Length;
					}
				}
			}
			if (edit.HasEffectiveChanges) {
				var snapshot = edit.Apply();
				tran.Complete();
				return modified.Select(i => new SnapshotSpan(snapshot, i));
			}
		}
		return Enumerable.Empty<SnapshotSpan>();
	}

	IEnumerable<SnapshotSpan> ComplexWrap(ITextView view) {
		var offset = 0;
		var placeholders = _PlaceholderCount != 0 ? new List<PlaceholderInfo>() : null;
		var modified = new Chain<Span>();
		var undoHistory = ServicesHelper.Instance.TextUndoHistory.GetHistory(view.TextBuffer);

		using (var tran = undoHistory.CreateTransaction(R.T_WrapTextName.Replace("<NAME>", Name))) {
			var eo = ServicesHelper.Instance.EditorOperationsFactory.GetEditorOperations(view);
			eo.AddBeforeTextBufferChangePrimitive();
			using var edit = view.TextSnapshot.TextBuffer.CreateEdit();
			foreach (var item in view.Selection.SelectedSpans) {
				var t = item.GetText();
				var start = item.Start.Position;
				var length = item.Length;
				var replacement = Wrap(
					t,
					_HasMultilinePrefix ? view.TextSnapshot.GetLinePrecedingWhitespaceAtPosition(start) : null,
					_HasMultilineSuffix ? view.TextSnapshot.GetLinePrecedingWhitespaceAtPosition(item.End.Position) : null,
					view.Options.GetIndentStringSize(),
					view.Options.GetNewLineCharacter(),
					placeholders,
					start + offset
				);

				if (edit.Replace(item, replacement)) {
					modified.Add(new Span(start + offset, replacement.Length));
					offset += replacement.Length - length;
				}
			}

			if (edit.HasEffectiveChanges) {
				var snapshot = edit.Apply();
				tran.Complete();
				if (placeholders.Count != 0) {
					if (view.TryGetProperty(out SnippetSession session)) {
						session.Terminate();
					}
					view.Properties[typeof(SnippetSession)] = new SnippetSession((IWpfTextView)view, placeholders, undoHistory);
					return new Chain<SnapshotSpan>(placeholders[0].ToSnapshotSpan(snapshot));
				}
				return modified.Select(i => new SnapshotSpan(snapshot, i));
			}
		}
		return Enumerable.Empty<SnapshotSpan>();
	}

	string Wrap(string text, string startLineSpace, string endLineSpace, int indentStringSize, string newLineChar, List<PlaceholderInfo> placeholders, int placeholderOffset) {
		using var sbr = ReusableStringBuilder.AcquireDefault(text.Length + _Pattern.Length);
		var sb = sbr.Resource;

		ExecuteCommands(_Commands, sb, startLineSpace, indentStringSize, newLineChar, placeholders, placeholderOffset, text);

		return sb.ToString();
	}
	static void ExecuteCommands(List<ISnippetCommand> commands, StringBuilder sb, string linePrefix, int indentSize, string newLineChar, List<PlaceholderInfo> placeholders, int placeholderOffset, string selectionText) {
		int currentIndentLevel = 0;

		foreach (var cmd in commands) {
			switch (cmd.Type) {
				case CommandType.Text:
					sb.Append(((TextCommand)cmd).Content);
					break;

				case CommandType.Indent:
					var ic = (IndentCommand)cmd;
					if (ic.TabCount > 0) {
						currentIndentLevel += ic.TabCount;
						if (indentSize > 0) {
							sb.Append(' ', ic.TabCount * indentSize);
						}
						else {
							sb.Append('\t', ic.TabCount);
						}
					}
					break;

				case CommandType.NewLine:
					sb.Append(newLineChar);
					if (!string.IsNullOrEmpty(linePrefix)) {
						sb.Append(linePrefix);
					}
					currentIndentLevel = 0;
					break;

				case CommandType.Selection:
					AppendSelection(sb, indentSize, newLineChar, selectionText, currentIndentLevel);
					break;

				case CommandType.Placeholder:
					int pos = sb.Length;
					string name = ((PlaceholderCommand)cmd).Name;
					sb.Append(name);
					placeholders.Add(new PlaceholderInfo(name, placeholderOffset + pos, name.Length));
					break;
			}
		}
	}

	static void AppendSelection(StringBuilder sb, int indentSize, string newLineChar, string selectionText, int currentIndentLevel) {
		int start = 0;
		int index;
		while ((index = selectionText.IndexOf('\n', start)) != -1) {
			int contentEnd = index;
			if (index > start && selectionText[index - 1] == '\r') {
				contentEnd--;
			}
			if (contentEnd > start) {
				sb.Append(selectionText, start, contentEnd - start);
			}
			sb.Append(newLineChar);

			if (currentIndentLevel > 0) {
				if (indentSize > 0) {
					sb.Append(' ', currentIndentLevel * indentSize);
				}
				else {
					sb.Append('\t', currentIndentLevel);
				}
			}

			start = index + 1;
		}
		if (start < selectionText.Length) {
			sb.Append(selectionText, start, selectionText.Length - start);
		}
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
