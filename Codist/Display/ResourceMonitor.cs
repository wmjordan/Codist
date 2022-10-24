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
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace Codist.Display
{
	static class ResourceMonitor
	{
		static Timer _Timer;
		static readonly StackPanel _MeterContainer = new StackPanel {
			Orientation = Orientation.Horizontal,
			Children = { new ContentPresenter(), new ContentPresenter(), new ContentPresenter(), }
		};
		static Meter _CpuMeter, _RamMeter, _DriveMeter;
		static int _IsInited;
		static CancellationTokenSource _CancellationTokenSource;

		public static void Reload(DisplayOptimizations option) {
			if (option.HasAnyFlag(DisplayOptimizations.ResourceMonitors) == false) {
				Stop();
				return;
			}
			ToggleMeter<CpuMeter>(0, option, DisplayOptimizations.ShowCpu, ref _CpuMeter);
			ToggleMeter<DriveMeter>(1, option, DisplayOptimizations.ShowDrive, ref _DriveMeter);
			ToggleMeter<RamMeter>(2, option, DisplayOptimizations.ShowMemory, ref _RamMeter);
			if (_Timer == null) {
				_Timer = new Timer(Update, null, 1000, 1000);
			}
		}

		static void ToggleMeter<TMeter>(int index, DisplayOptimizations option, DisplayOptimizations flag, ref Meter meter) where TMeter : Meter, new() {
			if (option.MatchFlags(flag)) {
				if (meter != null) {
					meter.Start();
				}
				else {
					meter = new TMeter();
					_MeterContainer.Children.RemoveAt(index);
					_MeterContainer.Children.Insert(index, meter);
				}
			}
			else {
				meter?.Stop();
			}
		}

		static void Stop() {
			if (_Timer != null) {
				_Timer.Dispose();
				_Timer = null;
				_CpuMeter?.Stop();
				_RamMeter?.Stop();
				_DriveMeter?.Stop();
			}
		}

		static void Update(object dummy) {
			UpdateAsync(SyncHelper.CancelAndRetainToken(ref _CancellationTokenSource)).FireAndForget();
		}

		async static Task UpdateAsync(CancellationToken cancellationToken) {
			_CpuMeter?.Sample();
			_RamMeter?.Sample();
			_DriveMeter?.Sample();
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
			if (_IsInited == 0) {
				Init();
				return;
			}
			_CpuMeter?.Update();
			_RamMeter?.Update();
			_DriveMeter?.Update();
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
			float _Value;

			protected Meter(int iconId, string tooltip) {
				Orientation = Orientation.Horizontal;
				Children.Add(ThemeHelper.GetImage(iconId).WrapMargin(WpfHelper.SmallHorizontalMargin));
				Children.Add(_Label = new TextBlock { MinWidth = 40, VerticalAlignment = VerticalAlignment.Center }.ReferenceProperty(Control.ForegroundProperty, EnvironmentColors.StatusBarDefaultTextBrushKey));
				_Counter = CreateCounter();
				ToolTip = new CommandToolTip(iconId, tooltip)
					.ReferenceCrispImageBackground(EnvironmentColors.ToolTipColorKey)
					.SetTipPlacementTop();
			}

			protected TextBlock Label => _Label;

			public void Sample() {
				var c = _Counter;
				if (c != null) {
					UpdateSample(_Value = c.NextValue());
				}
			}

			public void Update() {
				try {
					UpdateDisplay(_Value);
				}
				catch (Exception ex) {
					Debug.WriteLine(ex);
				}
			}

			protected virtual void UpdateSample(float counterValue) { }
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
		}

		sealed class CpuMeter : Meter
		{
			const int SampleCount = 10;
			readonly float[] _Samples = new float[SampleCount];
			float _SampleSum, _LastCounter;
			int _SampleIndex;

			public CpuMeter() : base(IconIds.Cpu, R.T_CpuUsage) {
			}

			protected override PerformanceCounter CreateCounter() {
				return new PerformanceCounter("Processor", "% Processor Time", "_Total");
			}

			protected override void UpdateSample(float counterValue) {
				_SampleSum -= _Samples[_SampleIndex];
				_Samples[_SampleIndex] = counterValue;
				_SampleSum += counterValue;
				if (++_SampleIndex == SampleCount) {
					_SampleIndex = 0;
				}
			}

			protected override void UpdateDisplay(float counterValue) {
				Label.Text = counterValue.ToString("0") + "%";
				Label.Opacity = (Math.Min(50, counterValue) + 50) / 100;
				counterValue = Math.Min(50, Math.Min(counterValue, _SampleSum / SampleCount)) / 50;
				if (counterValue < 0.2f) {
					if (_LastCounter >= 0.2f) {
						ClearValue(BackgroundProperty);
					}
				}
				else {
					Background = (counterValue < 0.4f ? Brushes.Yellow : counterValue < 0.6f ? Brushes.Orange : Brushes.Red).Alpha(counterValue);
				}
				_LastCounter = counterValue;
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

		sealed class DriveMeter : Meter
		{
			const int SampleCount = 10;
			readonly float[] _Samples = new float[SampleCount];
			float _SampleSum, _LastCounter;
			int _SampleIndex;

			public DriveMeter() : base(IconIds.Drive, R.T_DriveUsage) {
			}

			protected override PerformanceCounter CreateCounter() {
				return new PerformanceCounter("PhysicalDisk", "% Idle Time", "_Total");
			}

			protected override void UpdateSample(float counterValue) {
				counterValue = (float)Math.Round(100 - counterValue, 0);
				_SampleSum -= _Samples[_SampleIndex];
				_Samples[_SampleIndex] = counterValue;
				_SampleSum += counterValue;
				if (++_SampleIndex == SampleCount) {
					_SampleIndex = 0;
				}
			}

			protected override void UpdateDisplay(float counterValue) {
				counterValue = (float)Math.Round(100 - counterValue, 0);
				Label.Text = counterValue.ToString("0") + "%";
				Label.Opacity = (Math.Min(50, counterValue) + 50) / 100;
				counterValue = Math.Min(30, Math.Min(counterValue, _SampleSum / SampleCount)) / 30;
				if (counterValue < 0.2f) {
					if (_LastCounter >= 0.2f) {
						ClearValue(BackgroundProperty);
					}
				}
				else {
					Background = Brushes.Red.Alpha(counterValue);
				}
				_LastCounter = counterValue;
			}
		}
	}
}
