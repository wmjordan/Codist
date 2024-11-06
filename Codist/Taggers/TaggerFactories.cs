using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using CLR;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Taggers
{
	[Export(typeof(IViewTaggerProvider))]
	[ContentType(Constants.CodeTypes.Text)]
	[ContentType(Constants.CodeTypes.Output)]
	[TagType(typeof(IClassificationTag))]
	[TextViewRole(PredefinedTextViewRoles.Document)]
	[TextViewRole(PredefinedTextViewRoles.Interactive)]
	sealed class CustomTaggerProvider : IViewTaggerProvider
	{
		readonly Dictionary<ITextView, CustomTagger> _Taggers = new Dictionary<ITextView, CustomTagger>();
		bool _Enabled;

		public CustomTaggerProvider() {
			_Enabled = Config.Instance.Features.MatchFlags(Features.SyntaxHighlight);
			Config.RegisterUpdateHandler(FeatureToggle);
		}

		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
			if (_Enabled == false
				|| Config.Instance.Features.MatchFlags(Features.SyntaxHighlight) == false
				|| buffer.MayBeEditor() == false && textView.TextBuffer.ContentType.IsOfType("RoslynPreviewContentType") == false
				|| textView.Roles.Contains("STICKYSCROLL_TEXT_VIEW")
				) {
				return null;
			}

			return (textView.TryGetProperty(out CustomTagger t)
				? t
				: CreateTaggerInternal(textView)) as ITagger<T>;
		}

		CustomTagger CreateTaggerInternal(ITextView textView) {
			CustomTagger t;
			var d = textView.TextBuffer.GetTextDocument();
			if (d == null) {
				return null;
			}
			var p = d.FilePath;
			if (String.IsNullOrEmpty(p)) {
				return null;
			}
			if (_Taggers.TryGetValue(textView, out var ct)) {
				return MarkAndHookEvent(textView, ct, this);
			}
			try {
				t = CustomTagger.Get(textView, d);
			}
			catch (Exception ex) {
				ex.Log();
				return null;
			}
			if (t != null) {
				_Taggers.Add(textView, t);
				return MarkAndHookEvent(textView, t, this);
			}
			return null;

			CustomTagger MarkAndHookEvent(ITextView tv, CustomTagger ct, CustomTaggerProvider p) {
				if (tv.Properties.ContainsProperty(typeof(CustomTagger)) == false) {
					tv.Properties.AddProperty(typeof(CustomTagger), ct);
					tv.Closed -= p.TextViewClosed;
					tv.Closed += p.TextViewClosed;
				}
				return ct;
			}
		}

		void FeatureToggle(ConfigUpdatedEventArgs args) {
			bool enabled;
			if (args.UpdatedFeature.MatchFlags(Features.SyntaxHighlight)
				&& (enabled = Config.Instance.Features.MatchFlags(Features.SyntaxHighlight)) != _Enabled) {
				_Enabled = enabled;
				foreach (var item in _Taggers) {
					item.Value.Disabled = !enabled;
				}
			}
		}

		void TextViewClosed(object sender, EventArgs args) {
			var textView = sender as ITextView;
			textView.Closed -= TextViewClosed;
			if (textView.TryGetProperty(out CustomTagger ct)) {
				ct.Drop();
				if (_Taggers.TryGetValue(textView, out var rt)) {
					_Taggers.Remove(textView);
				}
				textView.RemoveProperty<CustomTagger>();
			}
		}
	}

	[Export(typeof(IViewTaggerProvider))]
	[ContentType(Constants.CodeTypes.Code)]
	[ContentType(Constants.CodeTypes.HtmlxProjection)]
	[TagType(typeof(IClassificationTag))]
	sealed class CommentTaggerProvider : IViewTaggerProvider
	{
		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
			if (Config.Instance.Features.MatchFlags(Features.SyntaxHighlight) == false
			#region Suppress CreateTagAggregator call
				// note:
				//   CommentTagger will call CreateTagAggregator<IClassificationTag>
				//     to find out comments provided by other taggers,
				//     and this provider method will also be called from that method.
				//   we should return null when this happens, otherwise stack overflow will occur.
				|| textView.TryGetProperty(out CommentTagger t)
			#endregion
				|| CommentTagger.IsCommentTaggable(buffer) == false
				|| buffer.MayBeEditor() == false && textView.TextBuffer.ContentType.IsOfType("RoslynPreviewContentType") == false
				|| textView.Roles.Contains("STICKYSCROLL_TEXT_VIEW")
				) {
				return null;
			}
			var tags = textView.GetOrCreateSingletonProperty<TaggerResult>();
			textView.Properties.AddProperty(typeof(CommentTagger), t = CommentTagger.Create(ServicesHelper.Instance.ClassificationTypeRegistry, textView, buffer));
			textView.Closed -= TextViewClosed;
			textView.Closed += TextViewClosed;
			return t as ITagger<T>;
		}

		void TextViewClosed(object sender, EventArgs args) {
			var textView = sender as ITextView;
			textView.Closed -= TextViewClosed;
			if (textView.TryGetProperty(out CommentTagger ct)) {
				ct.Dispose();
				textView.RemoveProperty<CommentTagger>();
			}
			textView.RemoveProperty<TaggerResult>();
		}
	}

	[Export(typeof(IViewTaggerProvider))]
	[ContentType(Constants.CodeTypes.CSharp)]
	[TagType(typeof(IClassificationTag))]
	sealed class CSharpTaggerProvider : IViewTaggerProvider
	{
		static readonly string[] __TaggableRoles = new[] { PredefinedTextViewRoles.Document, PredefinedTextViewRoles.EmbeddedPeekTextView, PredefinedTextViewRoles.PreviewTextView };

		// note: we could have used WeakDictionary to hold our references,
		//   and release references when the view is finalized
		//   unfortunately memory leak in VS sometimes prevents IWpfTextView from being released properly,
		//   thus the WeakDictionary won't help
		readonly Dictionary<ITextView, Dictionary<ITextBuffer, CSharpTagger>> _Taggers = new Dictionary<ITextView, Dictionary<ITextBuffer, CSharpTagger>>();

		// note: cache the latest used tagger to improve performance
		//   In C# code editor, even displaying the Quick Info will call the CreateTagger method,
		//   thus we cache the last accessed tagger, identified by ITextView and ITextBuffer,
		//   to avoid quite a few dictionary lookup operations
		ITextView _LastView;
		ITextBuffer _LastTextBuffer;
		CSharpTagger _LastTagger;
		bool _Enabled;

		// for debug info
		int _taggerCount;

		public CSharpTaggerProvider() {
			_Enabled = Config.Instance.Features.MatchFlags(Features.SyntaxHighlight);
			Config.RegisterUpdateHandler(FeatureToggle);
		}

		void FeatureToggle(ConfigUpdatedEventArgs args) {
			bool enabled;
			if (args.UpdatedFeature.MatchFlags(Features.SyntaxHighlight)
				&& (enabled = Config.Instance.Features.MatchFlags(Features.SyntaxHighlight)) != _Enabled) {
				_Enabled = enabled;
				foreach (var item in _Taggers) {
					foreach (var tagger in item.Value) {
						tagger.Value.Disabled = !enabled;
					}
				}
			}
		}

		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
			return (typeof(T) == typeof(IClassificationTag)) ? GetTagger(textView, buffer) as ITagger<T> : null;
		}

		ITagger<IClassificationTag> GetTagger(ITextView textView, ITextBuffer buffer) {
			if (_Enabled == false
				|| buffer.MayBeEditor() == false // it seems that the analyzer preview windows do not call the View_Close event handler, thus we exclude them here
				|| textView.TextBuffer.ContentType.IsOfType("RoslynPreviewContentType")
				|| textView.Roles.ContainsAny(__TaggableRoles) == false && textView.TextBuffer.ContentType.TypeName != Constants.CodeTypes.InteractiveContent
				|| textView.Roles.Contains("PREVIEWTOOLTIPTEXTVIEWROLE")
				|| textView.Roles.Contains("STICKYSCROLL_TEXT_VIEW") // disables advanced highlight for it does not work well if any style contains font size definition
				) {
				return null;
			}
			if (_LastView == textView && _LastTextBuffer == buffer) {
				_LastTagger.Ref();
				return _LastTagger;
			}

			if (_Taggers.TryGetValue(textView, out var bufferTaggers)) {
				if (bufferTaggers.TryGetValue(buffer, out var tagger)) {
					tagger.Ref();
					return tagger;
				}
				bufferTaggers.Add(_LastTextBuffer = buffer, _LastTagger = CreateTagger());
				return _LastTagger;
			}
			_Taggers.Add(_LastView = textView, new Dictionary<ITextBuffer, CSharpTagger>() {
					{ _LastTextBuffer = buffer, _LastTagger = CreateTagger() }
				});
			textView.Closed += TextView_Closed;
			return _LastTagger;
		}

		void TextView_Closed(object sender, EventArgs e) {
			var view = sender as ITextView;
			view.Closed -= TextView_Closed;
			if (_LastView == view) {
				_LastTextBuffer = null;
				_LastTagger = null;
			}
			if (_Taggers.TryGetValue(view, out var viewTaggers)) {
				foreach (var item in viewTaggers) {
					item.Value.Dispose();
					--_taggerCount;
				}
				_Taggers.Remove(view);
			}
		}

		CSharpTagger CreateTagger() {
			++_taggerCount;
			return new CSharpTagger(CSharpParser.GetOrCreate(_LastView as IWpfTextView), _LastTextBuffer);
		}
	}

	[Export(typeof(IClassifierProvider))]
	[ContentType(Constants.CodeTypes.FindResults)]
	sealed class FindResultTaggerProvider : IClassifierProvider
	{
		public IClassifier GetClassifier(ITextBuffer buffer) {
			return Config.Instance.Features.MatchFlags(Features.SyntaxHighlight)
				? new FindResultTagger()
				: null;
		}
	}

	[Export(typeof(IViewTaggerProvider))]
	[ContentType(Constants.CodeTypes.Code)]
	[ContentType(Constants.CodeTypes.Markdown)]
	[ContentType(Constants.CodeTypes.VsMarkdown)]
	[TagType(typeof(IClassificationTag))]
	sealed class MarkdownTaggerProvider : IViewTaggerProvider
	{
		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
			// the results produced by the tagger are also reused by the NaviBar and ScrollbarMarker
			if (Config.Instance.Features.HasAnyFlag(Features.SyntaxHighlight | Features.NaviBar | Features.ScrollbarMarkers) == false
				|| textView.TextBuffer.LikeContentType(Constants.CodeTypes.Markdown) == false) {
				return null;
			}
			return textView.Properties.GetOrCreateSingletonProperty(() => new MarkdownTagger(textView, buffer, Config.Instance.Features.MatchFlags(Features.SyntaxHighlight))) as ITagger<T>;
		}
	}
}
