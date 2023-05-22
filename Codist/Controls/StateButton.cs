using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using CLR;

namespace Codist.Controls
{
	sealed class StateButton<TState> : ToggleButton
		where TState : struct, Enum
	{
		readonly TState _State;
		readonly Func<TState> _StateGetter;
		readonly Action<TState, bool> _StateSetter;
		bool _UiLock;

		public StateButton(TState state, Func<TState> stateGetter, Action<TState, bool> stateSetter) {
			_State = state;
			_StateGetter = stateGetter;
			_StateSetter = stateSetter;
			this.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
		}

		public void UpdateState() {
			_UiLock = true;
			try {
				IsChecked = _StateGetter().MatchFlags(_State);
			}
			finally {
				_UiLock = false;
			}
		}

		protected override void OnChecked(RoutedEventArgs e) {
			base.OnChecked(e);
			if (_UiLock == false) {
				_StateSetter(_State, IsChecked.GetValueOrDefault());
			}
		}
	}
}
