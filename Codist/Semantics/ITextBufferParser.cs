using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;

namespace Codist
{
	/// <summary>
	/// Provides access to C# parser for a specific <see cref="ITextBuffer"/>.
	/// </summary>
	interface ITextBufferParser : IDisposable
	{
		ITextBuffer TextBuffer { get; }
		bool IsDisposed { get; }

		/// <summary>
		/// Gets <see cref="SemanticState"/> from given <see cref="ITextSnapshot"/>. If the <paramref name="snapshot"/> is not the same as the <see cref="SemanticState.Snapshot"/> in the <paramref name="state"/>, a new parsing will be scheduled. After the parsing, the semantic state can be retrieved via the <see cref="StateUpdated"/> event.
		/// </summary>
		/// <param name="snapshot">The snapshot to parse.</param>
		/// <param name="state">The previously state, or <see langword="null"/> if no successful parsing has ever achieved.</param>
		/// <returns>Returns <see langword="true"/> if the <paramref name="snapshot"/> is the same as the one in <paramref name="state"/> (the <paramref name="state"/> is up to date), otherwise <see langword="false"/>.</returns>
		bool TryGetSemanticState(ITextSnapshot snapshot, out SemanticState state);

		/// <summary>
		/// Gets <see cref="SemanticState"/> from given <see cref="ITextSnapshot"/>. Parsing might be scheduled, if the state is not up to date.
		/// </summary>
		Task<SemanticState> GetSemanticStateAsync(ITextSnapshot snapshot, CancellationToken cancellationToken);

		/// <summary>This event notifies subscribers that a new <see cref="SemanticState"/> is ready.</summary>
		event EventHandler<EventArgs<SemanticState>> StateUpdated;
	}
}