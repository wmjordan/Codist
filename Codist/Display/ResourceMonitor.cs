using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Codist.Controls;
using Microsoft.VisualStudio.PlatformUI;
using AppHelpers;
using R = Codist.Properties.Resources;

namespace Codist.Display
{
	static class ResourceMonitor
	{
		static Timer _Timer;
		static readonly StackPanel _MeterContainer = new StackPanel { Orientation = Orientation.Horizontal };
		static Meter _CpuMeter, _RamMeter;
		static int _IsInited;

		public static void Reload(DisplayOptimizations option) {
			if (option.HasAnyFlag(DisplayOptimizations.ShowCpu | DisplayOptimizations.ShowMemory) == false) {
				Stop();
				return;
			}
			if (option.MatchFlags(DisplayOptimizations.ShowCpu)) {
				if (_CpuMeter != null) {
					_CpuMeter.Start();
				}
				else {
					_CpuMeter = new CpuMeter();
					_MeterContainer.Children.Insert(0, _CpuMeter);
				}
			}
			else {
				_CpuMeter?.Stop();
			}
			if (option.MatchFlags(DisplayOptimizations.ShowMemory)) {
				if (_RamMeter != null) {
					_RamMeter.Start();
				}
				else {
					_RamMeter = new RamMeter();
					_MeterContainer.Children.Add(_RamMeter);
				}
			}
			else {
				_RamMeter?.Stop();
			}
			if (_Timer == null) {
				_Timer = new Timer(Update, null, 1000, 1000);
			}
		}

		static void Stop() {
			if (_Timer != null) {
				_Timer.Dispose();
				_Timer = null;
				_CpuMeter?.Stop();
				_RamMeter?.Stop();
			}
		}

		static void Update(object dummy) {
			if (_IsInited == 0) {
				_MeterContainer.Dispatcher.Invoke(Init);
				return;
			}
			_CpuMeter?.Update();
			_RamMeter?.Update();
		}

		static void Init() {
			var statusPanel = Application.Current.MainWindow.GetFirstVisualChild<Panel>(i => i.Name == "StatusBarPanel");
			if (statusPanel == null) {
				return;
			}
			if (Interlocked.CompareExchange(ref _IsInited, 1, 0) == 0) {
				_IsInited = 1;
				statusPanel.Children.Insert(0, _MeterContainer);
				_MeterContainer.MouseLeftButtonUp += StartTaskMgr;
			}
		}

		static void StartTaskMgr(object sender, System.Windows.Input.MouseButtonEventArgs e) {
			try {
				Process.Start("TaskMgr.exe");
			}
			catch (Exception ex) {
				Debug.WriteLine("Failed to start task manager: " + ex.ToString());
			}
		}

		abstract class Meter : StackPanel
		{
			readonly TextBlock _Label;
			PerformanceCounter _Counter;

			protected Meter(int iconId, string tooltip) {
				Orientation = Orientation.Horizontal;
				Children.Add(ThemeHelper.GetImage(iconId).WrapMargin(WpfHelper.SmallHorizontalMargin));
				Children.Add(_Label = new TextBlock { MinWidth = 40, VerticalAlignment = VerticalAlignment.Center });
				_Label.SetResourceReference(Control.ForegroundProperty, EnvironmentColors.StatusBarDefaultTextBrushKey);
				_Counter = CreateCounter();
				var t = new CommandToolTip(iconId, tooltip);
				t.SetResourceReference(ImageThemingUtilities.ImageBackgroundColorProperty, EnvironmentColors.ToolTipColorKey);
				ToolTip = t;
				ToolTipService.SetPlacement(this, System.Windows.Controls.Primitives.PlacementMode.Top);
			}

			protected TextBlock Label => _Label;

			public void Update() {
				try {
					Dispatcher.Invoke(UpdateMeter);
				}
				catch (System.Threading.Tasks.TaskCanceledException) {
					// ignore
				}
			}

			protected abstract void UpdateDisplay(float counterValue);
			protected abstract PerformanceCounter CreateCounter();

			public virtual void Start() {
				Visibility = Visibility.Visible;
				if (_Counter == null) {
					_Counter = CreateCounter();
				}
			}
			public virtual void Stop() {
				if (_Counter != null) {
					Visibility = Visibility.Collapsed;
					_Counter.Dispose();
					_Counter = null;
				}
			}

			void UpdateMeter() {
				var c = _Counter;
				if (c == null) {
					return;
				}
				try {
					UpdateDisplay(c.NextValue());
				}
				catch (Exception ex) {
					Debug.WriteLine(ex);
				}
			}
		}

		sealed class CpuMeter : Meter
		{
			const int SampleCount = 10;
			readonly float[] _Samples = new float[SampleCount];
			float _SampleSum;
			int _SampleIndex;

			public CpuMeter() : base(IconIds.Cpu, R.T_CpuUsage) {
			}

			protected override PerformanceCounter CreateCounter() {
				return new PerformanceCounter("Processor", "% Processor Time", "_Total");
			}

			protected override void UpdateDisplay(float counterValue) {
				Label.Text = counterValue.ToString("0") + "%";
				Label.Opacity = (Math.Min(50, counterValue) + 50) / 100;
				_SampleSum -= _Samples[_SampleIndex];
				_Samples[_SampleIndex] = counterValue;
				_SampleSum += counterValue;
				if (++_SampleIndex == SampleCount) {
					_SampleIndex = 0;
				}
				counterValue = Math.Min(50, Math.Min(counterValue, _SampleSum / SampleCount)) / 50;
				Background = counterValue < 0.2f ? Brushes.Transparent : Brushes.Red.Alpha(counterValue);
			}
		}

		sealed class RamMeter : Meter
		{
			public RamMeter() : base(IconIds.Memory, R.T_RamUsage) {
			}

			protected override PerformanceCounter CreateCounter() {
				return new PerformanceCounter("Memory", "% Committed Bytes In Use");
			}

			protected override void UpdateDisplay(float counterValue) {
				Label.Text = counterValue.ToString("0") + "%";
				Label.Opacity = (counterValue + 100) / 200;
			}
		}
	}
}
