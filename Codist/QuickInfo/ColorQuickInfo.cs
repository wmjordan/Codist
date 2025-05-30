﻿using System;
using System.Threading;
using System.Threading.Tasks;
using CLR;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Codist.QuickInfo
{
	sealed class ColorQuickInfo : SingletonQuickInfoSource
	{
		protected override Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			return Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Color) == false
				? Task.FromResult<QuickInfoItem>(null)
				: InternalGetQuickInfoItemAsync(session, cancellationToken);
		}

		static async Task<QuickInfoItem> InternalGetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			var buffer = session.TextView.TextBuffer;
			var snapshot = session.TextView.TextSnapshot;
			var navigator = ServicesHelper.Instance.TextStructureNavigator.GetTextStructureNavigator(buffer);
			var extent = navigator.GetExtentOfWord(session.GetTriggerPoint(snapshot).GetValueOrDefault()).Span;
			var word = snapshot.GetText(extent);
			var brush = ColorHelper.GetBrush(word);
			if (brush == null) {
				if ((extent.Length == 6 || extent.Length == 8) && extent.Span.Start > 0 && Char.IsPunctuation(snapshot.GetText(extent.Span.Start - 1, 1)[0])) {
					word = "#" + word;
				}
				brush = ColorHelper.GetBrush(word);
			}
			return brush != null && session.Mark(nameof(ColorQuickInfoUI))
				? new QuickInfoItem(extent.ToTrackingSpan(), new ColorInfoBlock(brush))
				: null;
		}
	}
}
