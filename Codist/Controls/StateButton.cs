using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using AppHelpers;

namespace Codist.Controls
{
	sealed class StateButton<TState> : ToggleButton
		where TState : struct, Enum
	{
		readonly TState _State;
		readonly Func<TState> _StateGetter;
		readonly Action<TState, bool> _StateSetter;
		bool _lockUI;

		public StateButton(TState state, Func<TState> stateGetter, Action<TState, bool> stateSetter) {
			_State = state;
			_StateGetter = stateGetter;
			_StateSetter = stateSetter;
			this.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
		}

		public void UpdateState() {
			_lockUI = true;
			try {
				IsChecked = _StateGetter().MatchFlags(_State);
			}
			finally {
				_lockUI = false;
			}
		}

		protected override void OnChecked(RoutedEventArgs e) {
			base.OnChecked(e);
			if (_lockUI == false) {
				_StateSetter(_State, IsChecked.GetValueOrDefault());
			}
		}
	}
}
