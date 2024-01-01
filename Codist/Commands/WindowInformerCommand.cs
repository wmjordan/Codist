using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using CLR;
using Codist.Controls;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Utilities;
using R = Codist.Properties.Resources;
using Window = EnvDTE.Window;

namespace Codist.Commands
{
	/// <summary>A command which displays information about the active window pane.</summary>
	internal static class WindowInformerCommand
	{
		const int SubSectionFontSize = 14;

		static readonly Thickness __ParagraphIndent = new Thickness(10, 0, 0, 0);
		static readonly Thickness __SectionIndent = new Thickness(10, 12, 0, 6);

		public static void Initialize() {
			Command.WindowInformer.Register(Execute, (s, args) => {
				ThreadHelper.ThrowIfNotOnUIThread();
				((OleMenuCommand)s).Visible = Config.Instance.DeveloperOptions.MatchFlags(DeveloperOptions.ShowWindowInformer)
					&& CodistPackage.DTE.ActiveWindow != null;
			});
		}

		static void Execute(object sender, EventArgs e) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var window = CodistPackage.DTE.ActiveWindow;
			if (window == null) {
				return;
			}
			DisplayWindowInfo(window);
		}

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		static void DisplayWindowInfo(Window window) {
			var tb = new RichTextBox {
				BorderThickness = WpfHelper.NoMargin,
				Background = ThemeHelper.DocumentPageBrush,
				Foreground = ThemeHelper.DocumentTextBrush,
				FontFamily = ThemeHelper.CodeTextFont,
				IsDocumentEnabled = true,
				IsReadOnly = true,
				IsReadOnlyCaretVisible = true,
				AcceptsReturn = false
			};
			tb.ApplyTemplate();
			tb.GetFirstVisualChild<ScrollViewer>().ReferenceStyle(VsResourceKeys.ScrollViewerStyleKey);
			var blocks = tb.Document.Blocks;
			blocks.Clear();
			Section s;

			var view = TextEditorHelper.GetActiveWpfInteractiveView();
			if (view?.VisualElement.IsFocused == false) {
				view = window.Document?.GetActiveWpfDocumentView();
			}

			if (view != null) {
				var d = view.TextBuffer.GetTextDocument();
				if (d != null) {
					s = NewSection(blocks, R.T_DocumentProperties);
					AppendNameValue(s, R.T_FilePath, d.FilePath);
					AppendNameValue(s, R.T_TextEncoding, d.Encoding.EncodingName);
					AppendNameValue(s, R.T_LastSaved, d.LastSavedTime == default ? R.T_NotSaved : (object)d.LastSavedTime.ToLocalTime());
					AppendNameValue(s, R.T_LastModified, d.LastContentModifiedTime.ToLocalTime());
				}

				s = NewSection(blocks, "IWpfTextView", SubSectionFontSize);
				AppendNameValue(s, R.T_LineCount + " (TextSnapshot.LineCount)", view.TextSnapshot.LineCount);
				AppendNameValue(s, R.T_CharacterCount + " (TextSnapshot.Length)", view.TextSnapshot.Length);

				AppendNameValue(s, R.T_Selection, $"[{view.Selection.Start.Position.Position}-{view.Selection.End.Position.Position})");
				AppendNameValue(s, R.T_SelectionLength, view.Selection.SelectedSpans.Sum(i => i.Length));
				AppendNameValue(s, "SelectedSpans.Count", view.Selection.SelectedSpans.Count);

				AppendNameValue(s, R.T_CaretPosition, view.Caret.Position.BufferPosition.Position);
				AppendNameValue(s, "Caret.OverwriteMode", view.Caret.OverwriteMode);
				AppendNameValue(s, "Caret.ContainingTextViewLine.Extent.Length", view.Caret.ContainingTextViewLine.Extent.Length);

				AppendNameValue(s, "ViewportLeft", view.ViewportLeft);
				AppendNameValue(s, "ViewportTop", view.ViewportTop);
				AppendNameValue(s, "ViewportWidth", view.ViewportWidth);
				AppendNameValue(s, "ViewportHeight", view.ViewportHeight);
				AppendNameValue(s, "VisualElement.ActualWidth", view.VisualElement.ActualWidth);
				AppendNameValue(s, "VisualElement.ActualHeight", view.VisualElement.ActualHeight);
				AppendNameValue(s, "TextViewLines.Count", view.TextViewLines.Count);

				Append(s, "TextBuffer.ContentType:");
				ShowContentType(view.TextBuffer.ContentType, s, new HashSet<IContentType>(), 2);

				if (view.TextBuffer is IProjectionBuffer projection) {
					Append(s, "Projection.SourceBuffers.ContentType:");
					foreach (var pb in projection.SourceBuffers) {
						ShowContentType(pb.ContentType, s, new HashSet<IContentType>(), 2);
					}
				}

				AppendNameValue(s, "Roles", view.Roles);

				ShowPropertyCollection(s, view.Properties, "Properties:");

				ShowPropertyCollection(s, view.TextBuffer.Properties, "TextBuffer.Properties:");
			}

			try {
				ShowDTEWindowProperties(blocks, window);
			}
			catch (Exception ex) {
				blocks.Add(new Paragraph(new Run(ex.ToString())));
			}

			MessageWindow.Show(tb, $"{R.T_ActiveWindowProperties} - {window.Caption}");
		}

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		static void ShowDTEWindowProperties(BlockCollection blocks, Window window) {
			var s = NewSection(blocks, "ActiveWindow", SubSectionFontSize);
			AppendNameValue(s, "Caption", window.Caption);
			AppendNameValue(s, "Kind", window.Kind);
			AppendNameValue(s, "Object", window.Object);
			if (window.Object != null) {
				try {
					AppendNameValue(s, "ObjectKind", window.ObjectKind);
				}
				catch (NotImplementedException) {
					// ignore
				}
			}
			AppendNameValue(s, "Type", window.Type);
			AppendNameValue(s, "AutoHides", window.AutoHides);
			AppendNameValue(s, "IsFloating", window.IsFloating);
			// AppendNameValue(s, "HWnd", window.HWnd); // this property is unavailable in 64bit VS
			AppendNameValue(s, "Left", window.Left);
			AppendNameValue(s, "Top", window.Top);
			AppendNameValue(s, "Width", window.Width);
			AppendNameValue(s, "Height", window.Height);
			AppendNameValue(s, "Linkable", window.Linkable);
			AppendNameValue(s, "LinkedWindowFrame.Caption", window.LinkedWindowFrame?.Caption);
			AppendNameValue(s, "Project.Name", window.Project?.Name);

			Section ss;
			var pi = window.ProjectItem;
			if (pi != null) {
				ss = NewIndentSection(s, "ProjectItem:");
				AppendNameValue(ss, "Name", pi.Name);
				try {
					AppendNameValue(ss, "ContainingProject", pi?.ContainingProject?.Name);
					AppendNameValue(ss, "ContainingProject.ExtenderNames", pi?.ContainingProject?.ExtenderNames);
				}
				catch (COMException ex) {
					AppendNameValue(ss, "ContainingProject", ex.Message);
				}
				if (pi.Properties?.Count != 0) {
					ss = NewIndentSection(ss, "Properties:");
					foreach (var item in pi.Properties.Enumerate().OrderBy(i => i.Key)) {
						AppendNameValue(ss, item.Key, item.Value);
					}
				}
			}

			var ca = window.ContextAttributes;
			if (ca != null && ca.Count != 0) {
				ss = NewIndentSection(s, "ContextAttributes:");
				foreach (ContextAttribute item in ca) {
					AppendPropertyValue(ss, item.Name, item.Values);
				}
			}

			var doc = window.Document;
			if (doc == null) {
				ShowSpecialWindowTypeInfo(blocks, window);
				return;
			}
			ss = NewIndentSection(s, "Document:");
			AppendNameValue(ss, "Language", doc.Language);
			try {
				AppendNameValue(ss, "Kind", doc.Kind);
			}
			catch (NotImplementedException) {
				// ignore
			}
			AppendNameValue(ss, "Type", doc.Type);
			AppendNameValue(ss, "ExtenderNames", doc.ExtenderNames);
			AppendNameValue(ss, "ExtenderCATID", doc.ExtenderCATID);
			AppendNameValue(ss, "DocumentData", window.DocumentData);
		}

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		static void ShowSpecialWindowTypeInfo(BlockCollection blocks, Window window) {
			switch (window.Type) {
				case vsWindowType.vsWindowTypeSolutionExplorer:
					ShowDTESolutionSelectedItems(blocks);
					break;
				case vsWindowType.vsWindowTypeOutput:
					ShowDTEOutputWindows(blocks);
					break;
			}
		}

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		static void ShowDTESolutionSelectedItems(BlockCollection blocks) {
			if (CodistPackage.DTE.ToolWindows.SolutionExplorer.SelectedItems is object[] items) {
				foreach (UIHierarchyItem hi in items.OfType<UIHierarchyItem>()) {
					var obj = hi.Object;
					if (obj is ProjectItem pi) {
						ShowDTEProjectItemProperties(blocks, pi);
					}
					else if (obj is Project p) {
						ShowDTEProjectProperties(blocks, p);
					}
					else if (obj is Solution s) {
						ShowDTESolutionProperties(blocks, s);
						ShowDTEProperties(blocks);
					}
					else {
						var ss = NewSection(blocks, "UIHierarchyItem", SubSectionFontSize);
						AppendNameValue(ss, "Name", hi.Name);
						AppendNameValue(ss, "Object", hi.Object);
					}
				}
			}
		}
		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		static void ShowDTEOutputWindows(BlockCollection blocks) {
			var s = NewSection(blocks, "OutputWindow", SubSectionFontSize);
			var o = CodistPackage.DTE.ToolWindows.OutputWindow;
			AppendNameValue(s, "ActivePane.Name", o.ActivePane?.Name);
			var ss = NewIndentSection(s, "OutputWindowPanes");
			foreach (var item in o.OutputWindowPanes.OfType<OutputWindowPane>()) {
				AppendNameValue(ss, item.Name, item.Guid);
			}
		}

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		static void ShowDTEProjectItemProperties(BlockCollection blocks, ProjectItem pi) {
			var s = NewSection(blocks, "ProjectItem", SubSectionFontSize);
			AppendNameValue(s, "Name", pi.Name);
			var fc = pi.FileCount;
			for (short i = 1; i <= fc; i++) {
				AppendNameValue(s, "FileNames", pi.FileNames[i]);
			}
			AppendNameValue(s, "FileCodeModel", pi.FileCodeModel);
			AppendNameValue(s, "Kind", pi.Kind);
			AppendNameValue(s, "ExtenderCATID", pi.ExtenderCATID);
			AppendNameValue(s, "ExtenderNames", pi.ExtenderNames);
			AppendNameValue(s, "ContainingProject.Name", pi.ContainingProject?.Name);
			AppendNameValue(s, "SubProject.Name", pi.SubProject?.Name);
			AppendNameValue(s, "Object", pi.Object);
			//AppendNameValue(s, "IsDirty", pi.IsDirty);
			AppendNameValue(s, "IsOpen", pi.IsOpen);
			//AppendNameValue(s, "Saved", pi.Saved);
			AppendNameValue(s, "ProjectItems.Count", pi.ProjectItems?.Count);
			AppendNameValue(s, "ProjectItems.Kind", pi.ProjectItems?.Kind);
			try {
				ShowPropertyCollection(s, pi.Properties, "Properties:");
			}
			catch (COMException ex) {
				AppendNameValue(s, "Properties", ex.Message);
			}
		}

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		static void ShowDTESolutionProperties(BlockCollection blocks, Solution solution) {
			var s = NewSection(blocks, "Solution", SubSectionFontSize);
			AppendNameValue(s, "FullName", solution.FullName);
			AppendNameValue(s, "IsDirty", solution.IsDirty);
			AppendNameValue(s, "Saved", solution.Saved);
			AppendNameValue(s, "Count", solution.Count);
			AppendNameValue(s, "ExtenderCATID", solution.ExtenderCATID);
			AppendNameValue(s, "ExtenderNames", solution.ExtenderNames);
			AppendNameValue(s, "Globals.VariableNames", solution.Globals?.VariableNames);
			ShowDTESolutionBuild(solution, s);
			ShowPropertyCollection(s, solution.Properties, "Properties:");
			var ss = NewIndentSection(s, "Projects:");
			foreach (Project project in solution.Projects) {
				AppendNameValue(ss, "Project.Name", project.Name);
			}
		}

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		static void ShowDTESolutionBuild(Solution solution, Section s) {
			var sb = solution.SolutionBuild;
			if (sb == null) {
				return;
			}
			s = NewIndentSection(s, "SolutionBuild:");
			var ac = sb.ActiveConfiguration;
			Section ss, cs;
			if (ac != null) {
				ss = NewIndentSection(s, "ActiveConfiguration:");
				AppendNameValue(ss, "Name", ac.Name);
				if (ac.SolutionContexts.Count != 0) {
					ss = NewIndentSection(ss, "SolutionContexts:");
					foreach (SolutionContext item in ac.SolutionContexts) {
						cs = NewIndentSection(ss, item.ProjectName);
						AppendNameValue(cs, "ProjectName", item.ProjectName);
						AppendNameValue(cs, "ConfigurationName", item.ConfigurationName);
						AppendNameValue(cs, "PlatformName", item.PlatformName);
						AppendNameValue(cs, "ShouldBuild", item.ShouldBuild);
						AppendNameValue(cs, "ShouldDeploy", item.ShouldDeploy);
					}
				}
			}
			var sc = sb.SolutionConfigurations;
			if (sc != null && sc.Count != 0) {
				ss = NewIndentSection(s, "SolutionConfigurations:");
				foreach (SolutionConfiguration item in sc) {
					AppendNameValue(ss, "SolutionConfigurations.Name", item.Name);
				}
			}
			AppendNameValue(s, "StartupProjects", sb.StartupProjects);
			var bd = sb.BuildDependencies;
			if (bd != null && bd.Count != 0) {
				ss = NewIndentSection(s, "BuildDependencies:");
				foreach (BuildDependency item in bd) {
					cs = NewIndentSection(ss, item.Project.Name);
					AppendNameValue(cs, "Project.Name", item.Project.Name);
					AppendNameValue(cs, "RequiredProjects", item.RequiredProjects);
				}
			}
		}

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		static void ShowDTEProjectProperties(BlockCollection blocks, Project project) {
			var s = NewSection(blocks, "Project", SubSectionFontSize);
			AppendNameValue(s, "Name", project.Name);
			AppendNameValue(s, "UniqueName", project.UniqueName);
			try {
				AppendNameValue(s, "FullName", project.FullName);
			}
			catch (NotImplementedException) {
				// ignore
			}
			AppendNameValue(s, "Kind", project.Kind);
			AppendNameValue(s, "IsDirty", project.IsDirty);
			AppendNameValue(s, "Saved", project.Saved);
			AppendNameValue(s, "ExtenderCATID", project.ExtenderCATID);
			AppendNameValue(s, "ExtenderNames", project.ExtenderNames);
			AppendNameValue(s, "Globals.VariableNames", project.Globals?.VariableNames);
			AppendNameValue(s, "Object", project.Object);
			var cm = project.ConfigurationManager;
			if (cm != null) {
				var ss = NewIndentSection(s, "ConfigurationManager:");
				AppendNameValue(ss, "ConfigurationRowNames", cm.ConfigurationRowNames);
				AppendNameValue(ss, "Count", cm.Count);
				AppendNameValue(ss, "PlatformNames", cm.PlatformNames);
				AppendNameValue(ss, "SupportedPlatforms", cm.SupportedPlatforms);
				var c = cm.ActiveConfiguration;
				if (c != null) {
					ss = NewIndentSection(ss, "ActiveConfiguration:");
					AppendNameValue(ss, "Type", c.Type);
					AppendNameValue(ss, "ConfigurationName", c.ConfigurationName);
					AppendNameValue(ss, "PlatformName", c.PlatformName);
					AppendNameValue(ss, "ExtenderCATID", c.ExtenderCATID);
					AppendNameValue(ss, "ExtenderNames", c.ExtenderNames);
					AppendNameValue(ss, "IsBuildable", c.IsBuildable);
					AppendNameValue(ss, "IsDeployable", c.IsDeployable);
					AppendNameValue(ss, "IsRunable", c.IsRunable);
					AppendNameValue(ss, "Object", c.Object);
					AppendNameValue(ss, "Owner", c.Owner);
					ShowOutputGroups(ss, c.OutputGroups, "OutputGroups:");
					try {
						ShowPropertyCollection(ss, c.Properties, "Properties:");
					}
					catch (COMException ex) {
						AppendNameValue(ss, "Properties", ex.Message);
					}
				}
			}
			try {
				ShowPropertyCollection(s, project.Properties, "Properties:");
			}
			catch (COMException ex) {
				AppendNameValue(s, "Properties", ex.Message);
			}
		}

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		static void ShowOutputGroups(Section ss, OutputGroups outputGroups, string title) {
			var s = NewIndentSection(ss, title);
			for (int i = 0; i < outputGroups.Count;) {
				var g = outputGroups.Item(++i);
				ss = NewIndentSection(s, "OutputGroup " + i.ToString());
				AppendNameValue(ss, "CanonicalName", g.CanonicalName);
				AppendNameValue(ss, "Description", g.Description);
				AppendNameValue(ss, "DisplayName", g.DisplayName);
				AppendNameValue(ss, "FileCount", g.FileCount);
				//AppendNameValue(ss, "FileNames", g.FileNames);
				//AppendNameValue(ss, "FileURLs", g.FileURLs);
			}
		}

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		static void ShowDTEProperties(BlockCollection blocks) {
			var s = NewSection(blocks, "DTE", SubSectionFontSize);
			var dte = CodistPackage.DTE;
			AppendNameValue(s, "Edition", dte.Edition);
			AppendNameValue(s, "DisplayMode", dte.DisplayMode);
			AppendNameValue(s, "Mode", dte.Mode);
			AppendNameValue(s, "Solution.FullName", dte.Solution.FullName);
			AppendNameValue(s, "ActiveDocument.Name", dte.ActiveDocument?.Name);
			AppendNameValue(s, "ActiveSolutionProjects", dte.ActiveSolutionProjects);
			AppendNameValue(s, "SelectedItems.Count", dte.SelectedItems.Count);
			AppendNameValue(s, "ActiveWindow.Caption", dte.ActiveWindow?.Caption);
			AppendNameValue(s, "CommandLineArguments", dte.CommandLineArguments);
			AppendNameValue(s, "RegistryRoot", dte.RegistryRoot);
			AppendNameValue(s, "WindowConfigurations.ActiveConfigurationName", dte.WindowConfigurations?.ActiveConfigurationName);
			AppendNameValue(s, "Windows/Caption", dte.Windows.OfType<Window>().Select(w => w.Caption).ToArray());
			AppendNameValue(s, "Version", dte.Version);

			var ca = dte.ContextAttributes;
			if (ca != null && ca.Count != 0) {
				s = NewIndentSection(s, "ContextAttributes:");
				foreach (ContextAttribute item in ca) {
					AppendPropertyValue(s, item.Name, item.Values);
				}
			}
		}

		static void ShowContentType (IContentType type, Section section, HashSet<IContentType> dedup, int indent) {
			Append(section, type.DisplayName != type.TypeName ? $"{type.DisplayName} ({type.TypeName})" : type.DisplayName, indent * 10);
			foreach (var bt in type.BaseTypes) {
				if (dedup.Add(bt)) {
					ShowContentType(bt, section, dedup, indent + 1);
				}
			}
		}

		static void ShowPropertyCollection(Section section, PropertyCollection properties, string title) {
			var s = NewIndentSection(section, title);
			foreach (var (n, k, v) in properties.PropertyList.Select(i => (n: i.Key is Type t ? GetTypeName(t) : i.Key.ToString(), k: i.Key, v: i.Value)).OrderBy(i => i.n)) {
				AppendPropertyValue(s, k, v);
			}
		}
		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		static void ShowPropertyCollection(Section section, EnvDTE.Properties properties, string title) {
			if (properties == null || properties.Count == 0) {
				return;
			}
			var s = NewIndentSection(section, title);
			foreach (var item in properties.OfType<EnvDTE.Property>().Select(p => {
				object val;
				if (p.NumIndices != 0) {
					val = null;
				}
				else {
					try {
						val = p.Value;
					}
					catch (COMException) {
						val = null;
					}
				}
				return new KeyValuePair<string, object>(p.Name, val);
			}).OrderBy(i => i.Key)) {
				AppendPropertyValue(s, item.Key, item.Value);
			}
		}

		static Section NewSection(BlockCollection blocks, string title) {
			Section section = new Section {
				Margin = __SectionIndent,
				Blocks = {
					new Paragraph(new Run(title) {
						FontSize = 18,
						FontWeight = FontWeights.Bold,
						Foreground = SymbolFormatter.Instance.Class
					}) {
						TextIndent = -5,
						Margin = WpfHelper.SmallMargin
					}
				}
			};
			blocks.Add(section);
			return section;
		}
		static Section NewSection(BlockCollection blocks, string title, int fontSize) {
			Section section = new Section {
				Margin = __SectionIndent,
				Blocks = {
					new Paragraph(new Run(title) {
						FontSize = fontSize,
						FontWeight = FontWeights.Bold,
						Foreground = SymbolFormatter.Instance.Class
					}) {
						TextIndent = -10,
						Margin = WpfHelper.SmallMargin
					}
				}
			};
			blocks.Add(section);
			return section;
		}
		static Section NewIndentSection(Section block, string title) {
			Append(block, title);
			Section section = new Section {
				Margin = new Thickness(block.Margin.Left, 0, 0, 0),
			};
			block.Blocks.Add(section);
			return section;
		}
		static void AppendNameValue(Section section, string name, object value) {
			var p = new Paragraph {
				Margin = __ParagraphIndent,
				TextIndent = -10,
				Inlines = {
					name.Render(SymbolFormatter.Instance.Property),
					new Run(" = "),
				}
			};
			AppendValueRun(p.Inlines, value);
			section.Blocks.Add(p);
		}
		static void AppendPropertyValue(Section section, object key, object value) {
			var p = new Paragraph {
				Margin = __ParagraphIndent,
				TextIndent = -10,
				Inlines = {
					CreateRun(key).SetValue(TextElement.SetForeground, SymbolFormatter.Instance.Property),
					new Run(": "),
				}
			};
			AppendValueRun(p.Inlines, value);
			section.Blocks.Add(p);
		}
		static void Append(Section section, string text, int indent = 0) {
			section.Blocks.Add(new Paragraph {
				Margin = __ParagraphIndent,
				TextIndent = indent - 10,
				Inlines = {
					text.Render(SymbolFormatter.Instance.Property)
				}
			});
		}
		static void AppendValueRun(InlineCollection inlines, object value) {
			if (value is Array a) {
				inlines.Add("[".Render(SymbolFormatter.SemiTransparent.PlainText));
				for (int i = 0; i < a.Length; i++) {
					if (i > 0) {
						inlines.Add(", ".Render(SymbolFormatter.SemiTransparent.PlainText));
					}
					inlines.Add(CreateRun(a.GetValue(i)));
				}
				inlines.Add("]".Render(SymbolFormatter.SemiTransparent.PlainText));
			}
			else {
				inlines.Add(CreateRun(value));
			}
		}

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		static Run CreateRun(object value) {
			var f = SymbolFormatter.Instance;
			if (value == null) {
				return "null".Render(f.Keyword);
			}
			if (value is Type type) {
				return new TypeRun(type, f);
			}
			type = value.GetType();
			if (type.IsEnum) {
				return value.ToString().Render(f.EnumField);
			}
			switch (Type.GetTypeCode(type)) {
				case TypeCode.Object:
					if (type.Name == "__ComObject" && type.Namespace == "System") {
						return (ReflectionHelper.GetTypeNameFromComObject(value) ?? "System.__ComObject").Render(f.Class);
					}
					if (value is Project p) {
						return new TypeRun(type, p.Name, f);
					}
					var toString = type.GetMethod("ToString", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public, null, Type.EmptyTypes, null);
					if (toString?.DeclaringType != typeof(object)) {
						goto default;
					}
					return new TypeRun(type, f);
				case TypeCode.Boolean: return ((bool)value ? "true" : "false").Render(f.Keyword);
				case TypeCode.Char: return value.ToString().Render(f.Text);
				case TypeCode.SByte:
				case TypeCode.Byte:
				case TypeCode.Int16:
				case TypeCode.UInt16:
				case TypeCode.Int32:
				case TypeCode.UInt32:
				case TypeCode.Int64:
				case TypeCode.UInt64:
				case TypeCode.Single:
				case TypeCode.Double:
				case TypeCode.Decimal: return value.ToString().Render(f.Number);
				case TypeCode.String:
					var s = value.ToString();
					return s.Length > 0
						? s.Render(f.Text)
						: "\"\"".Render(SymbolFormatter.SemiTransparent.PlainText);
				default:
					return new Run(value.ToString());
			}
		}
		static string GetTypeName(Type type) {
			return (type.DeclaringType != null
				? $"{GetTypeName(type.DeclaringType)}+{type.Name}"
				: type.Name)
				+ (type.IsGenericType
					? $"<{String.Join(",", type.GenericTypeArguments.Select(GetTypeName))}>"
					: String.Empty);
		}
		static Brush GetTypeBrush(SymbolFormatter f, Type type) {
			return type.IsClass ? f.Class
				: type.IsInterface ? f.Interface
				: type.IsValueType ? f.Struct
				: type.IsEnum ? f.Enum
				: f.PlainText;
		}

		sealed class TypeRun : Run
		{
			readonly Type _Type;

			public TypeRun(Type type, SymbolFormatter formatter) : this(type, GetTypeName(type), formatter) {
			}

			public TypeRun(Type type, string alias, SymbolFormatter formatter) : base(alias) {
				_Type = type;
				Foreground = GetTypeBrush(formatter, type);
				ToolTip = String.Empty;
			}

			protected override void OnToolTipOpening(ToolTipEventArgs e) {
				base.OnToolTipOpening(e);
				if ((ToolTip as string)?.Length == 0) {
					var tip = new ThemedToolTip();
					tip.Title.Text = GetTypeName(_Type);
					tip.Content.Append(R.T_Type, true)
						.Append(_Type.FullName)
						.AppendLine()
						.Append(R.T_Assembly, true)
						.Append(_Type.Assembly.FullName)
						.AppendLine()
						.Append(R.T_AssemblyFile, true)
						.Append(_Type.Assembly.Location);
					ToolTip = tip;
					ToolTipService.SetShowDuration(this, 10000);
				}
			}
		}
	}
}
