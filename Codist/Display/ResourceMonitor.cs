using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CLR;
using Codist.Controls;
using Microsoft.VisualStudio.PlatformUI;
using R = Codist.Properties.Resources;
using Task = System.Threading.Tasks.Task;

namespace Codist.Display
{
	static class ResourceMonitor
	{
		static Timer __Timer;
		static readonly StackPanel __MeterContainer = new StackPanel {
			Orientation = Orientation.Horizontal,
			Children = { new ContentPresenter(), new ContentPresenter(), new ContentPresenter(), new ContentPresenter() }
		};
		static Meter __CpuMeter, __RamMeter, __DriveMeter, __NetworkMeter;
		static int __IsInited;
		static CancellationTokenSource __CancellationTokenSource;

		public static void Reload(DisplayOptimizations option) {
			if (option.HasAnyFlag(DisplayOptimizations.ResourceMonitors) == false) {
				Stop();
				return;
			}
			ToggleMeter<CpuMeter>(0, option, DisplayOptimizations.ShowCpu, ref __CpuMeter);
			ToggleMeter<DriveMeter>(1, option, DisplayOptimizations.ShowDrive, ref __DriveMeter);
			ToggleMeter<RamMeter>(2, option, DisplayOptimizations.ShowMemory, ref __RamMeter);
			ToggleMeter<NetworkMeter>(3, option, DisplayOptimizations.ShowNetwork, ref __NetworkMeter);
			if (__Timer == null) {
				__Timer = new Timer(Update, null, 1000, 1000);
			}
		}

		static void ToggleMeter<TMeter>(int index, DisplayOptimizations option, DisplayOptimizations flag, ref Meter meter) where TMeter : Meter, new() {
			if (option.MatchFlags(flag)) {
				if (meter != null) {
					meter.Start();
				}
				else {
					meter = new TMeter();
					meter.Start();
					__MeterContainer.Children.RemoveAt(index);
					__MeterContainer.Children.Insert(index, meter);
				}
			}
			else {
				meter?.Stop();
			}
		}

		static void Stop() {
			if (__Timer != null) {
				__Timer.Dispose();
				__Timer = null;
				__CpuMeter?.Stop();
				__RamMeter?.Stop();
				__DriveMeter?.Stop();
				__NetworkMeter?.Stop();
			}
		}

		static void Update(object dummy) {
			UpdateAsync(SyncHelper.CancelAndRetainToken(ref __CancellationTokenSource)).FireAndForget();
		}

		async static Task UpdateAsync(CancellationToken cancellationToken) {
			__CpuMeter?.Sample();
			__RamMeter?.Sample();
			__DriveMeter?.Sample();
			__NetworkMeter?.Sample();
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			if (__IsInited == 0) {
				Init();
				return;
			}
			__CpuMeter?.Update();
			__RamMeter?.Update();
			__DriveMeter?.Update();
			__NetworkMeter?.Update();
		}

		static void Init() {
			var statusPanel = Application.Current.MainWindow.GetFirstVisualChild<Panel>(i => i.Name == "StatusBarPanel");
			if (statusPanel == null) {
				return;
			}
			if (Interlocked.CompareExchange(ref __IsInited, 1, 0) == 0) {
				__IsInited = 1;
				statusPanel.Children.Insert(0, __MeterContainer);
				__MeterContainer.MouseLeftButtonUp += StartTaskMgr;
			}
		}

		static void StartTaskMgr(object sender, System.Windows.Input.MouseButtonEventArgs e) {
			try {
				ExternalCommand.OpenTaskManager();
			}
			catch (Exception ex) {
				Debug.WriteLine("Failed to start task manager: " + ex.ToString());
			}
		}

		abstract class Meter : StackPanel
		{
			readonly TextBlock _Label;

			protected Meter(int iconId, string tooltip) {
				Orientation = Orientation.Horizontal;
				Children.Add(ThemeHelper.GetImage(iconId).WrapMargin(WpfHelper.SmallHorizontalMargin));
				Children.Add(_Label = new TextBlock { MinWidth = 40, VerticalAlignment = VerticalAlignment.Center }.ReferenceProperty(Control.ForegroundProperty, EnvironmentColors.StatusBarDefaultTextBrushKey));
				ToolTip = new CommandToolTip(iconId, tooltip)
					.ReferenceCrispImageBackground(EnvironmentColors.ToolTipColorKey);
				this.SetTipPlacementTop();
			}

			protected TextBlock Label => _Label;

			public abstract void Sample();
			public abstract void Update();

			public virtual void Start() {
				Visibility = Visibility.Visible;
			}
			public virtual void Stop() {
				Visibility = Visibility.Collapsed;
			}
		}

		abstract class SinglePerformanceCounterMeter : Meter
		{
			PerformanceCounter _Counter;
			float _Value;

			protected SinglePerformanceCounterMeter(int iconId, string tooltip) : base(iconId, tooltip) {
				_Counter = CreateCounter();
			}

			protected abstract PerformanceCounter CreateCounter();
			protected virtual void UpdateSample(float counterValue) { }
			protected abstract void UpdateDisplay(float counterValue);

			public override void Start() {
				base.Start();
				if (_Counter == null) {
					_Counter = CreateCounter();
				}
			}

			public override void Stop() {
				base.Stop();
				if (_Counter != null) {
					_Counter.Dispose();
					_Counter = null;
				}
			}

			public override void Sample() {
				var c = _Counter;
				if (c != null) {
					UpdateSample(_Value = c.NextValue());
				}
			}

			public override void Update() {
				try {
					UpdateDisplay(_Value);
				}
				catch (Exception ex) {
					Debug.WriteLine(ex);
				}
			}
		}

		abstract class MultiPerformanceCounterMeter : Meter
		{
			PerformanceCounter[] _Counters;
			readonly float[] _Values;

			protected MultiPerformanceCounterMeter(int iconId, string tooltip) : base(iconId, tooltip) {
				_Counters = CreateCounters();
				_Values = new float[_Counters.Length];
			}

			protected abstract PerformanceCounter[] CreateCounters();
			protected virtual void UpdateSample(float[] counterValues) { }
			protected abstract void UpdateDisplay(float[] counterValues);

			public override void Start() {
				base.Start();
				if (_Counters == null) {
					_Counters = CreateCounters();
				}
			}

			public override void Stop() {
				base.Stop();
				if (_Counters != null) {
					foreach (var item in _Counters) {
						item.Dispose();
					}
					_Counters = null;
				}
			}

			public override void Sample() {
				var c = _Counters;
				if (c != null) {
					for (int i = 0; i < _Counters.Length; i++) {
						_Values[i] = _Counters[i].NextValue();
					}
					UpdateSample(_Values);
				}
			}

			public override void Update() {
				try {
					UpdateDisplay(_Values);
				}
				catch (Exception ex) {
					Debug.WriteLine(ex);
				}
			}
		}

		sealed class CpuMeter : SinglePerformanceCounterMeter
		{
			const int SampleCount = 10;
			readonly float[] _Samples = new float[SampleCount];
			readonly List<(uint pid, string name, float cpu)> _ProcessUsages = new List<(uint, string, float)>(3);
			readonly Dictionary<uint, (ulong counter, int version)> _ProcCpuUsages = new Dictionary<uint, (ulong, int)>();
			ManagementObjectSearcher _WmiSearcher;
			ulong _LastSys100ns;
			float _SampleSum, _LastCounter;
			int _SampleIndex, _ProcSampleState;
			bool _TooltipDisplayed;

			public CpuMeter() : base(IconIds.Cpu, R.T_CpuUsage) {
				ToolTipService.SetShowDuration(this, Int32.MaxValue);
			}

			public override void Start() {
				base.Start();
				_WmiSearcher = new ManagementObjectSearcher("SELECT IDProcess, Name, PercentProcessorTime, Timestamp_Sys100NS FROM Win32_PerfRawData_PerfProc_Process WHERE IDProcess != 0");
			}

			public override void Stop() {
				base.Stop();
				_WmiSearcher?.Dispose();
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
				if (_TooltipDisplayed) {
					SampleProcessCpuUsage();
				}
			}

			void SampleProcessCpuUsage() {
				if (_ProcCpuUsages.Count == 0) {
					if (_ProcSampleState != 0) {
						return;
					}
					_ProcSampleState = 1;
					InitCpuUsages();
					return;
				}
				if (_ProcSampleState == 1) {
					_ProcSampleState = 2;
					CalculateCpuUsages();
					_ProcSampleState = 1;
				}
			}

			void InitCpuUsages() {
				ulong t = 0;
				foreach (var item in _WmiSearcher.Get()) {
					if (t == 0) {
						_LastSys100ns = t = (ulong)item.GetPropertyValue("Timestamp_Sys100NS");
					}
					_ProcCpuUsages[(uint)item.GetPropertyValue("IDProcess")] = ((ulong)item.GetPropertyValue("PercentProcessorTime"), (int)t);
				}
			}

			void CalculateCpuUsages() {
				_ProcessUsages.Clear();
				ulong t = 0;
				ulong deltaT = 0;
				int c = 0;
				int n = 0;
				foreach (var item in _WmiSearcher.Get()) {
					if (t == 0) {
						t = (ulong)item.GetPropertyValue("Timestamp_Sys100NS");
						deltaT = t - _LastSys100ns;
						_LastSys100ns = t;
						n = (int)t;
					}
					++c;
					uint pid = (uint)item.GetPropertyValue("IDProcess");
					ulong proc = (ulong)item.GetPropertyValue("PercentProcessorTime");
					float cpu = (float)(proc - (_ProcCpuUsages.TryGetValue(pid, out var u) ? u.counter : 0)) / deltaT;
					_ProcCpuUsages[pid] = (proc, n);
					if (cpu != 0) {
						string name = (string)item.GetPropertyValue("Name");
						_ProcessUsages.Add((pid, name, cpu));
					}
				}
				if (_ProcCpuUsages.Count != c) {
					// process count changed, remove inexistent processes
					RemoveInexistentCpuUsage(n);
				}
				_ProcessUsages.Sort((x, y) => y.cpu.CompareTo(x.cpu));
			}

			void RemoveInexistentCpuUsage(int n) {
				var remove = new List<uint>();
				foreach (var item in _ProcCpuUsages) {
					if (item.Value.version != n) {
						remove.Add(item.Key);
					}
				}
				foreach (var item in remove) {
					_ProcCpuUsages.Remove(item);
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
				if (_TooltipDisplayed && (ToolTip as CommandToolTip)?.Description is TextBlock t) {
					ShowToolTip(t);
				}
			}

			void ShowToolTip(TextBlock t) {
				using (var r = Microsoft.VisualStudio.Utilities.ReusableStringBuilder.AcquireDefault(100)) {
					var sb = r.Resource;
					var pc = 100f / Environment.ProcessorCount;
					int c = 0, i;
					foreach (var (pid, name, usage) in _ProcessUsages) {
						if (sb.Length > 0) {
							sb.AppendLine();
						}
						i = name.IndexOf('#');
						(i < 0 ? sb.Append(name) : sb.Append(name, 0, i))
							.Append(" (").Append(pid).Append("): ")
							.Append((usage * pc).ToString("0.0")).Append('%');
						if (++c == 4) {
							break;
						}
					}
					t.Text = sb.ToString();
				}
			}

			protected override void OnToolTipOpening(ToolTipEventArgs e) {
				base.OnToolTipOpening(e);
				_TooltipDisplayed = true;
			}

			protected override void OnToolTipClosing(ToolTipEventArgs e) {
				base.OnToolTipClosing(e);
				_TooltipDisplayed = false;
			}
		}

		sealed class RamMeter : SinglePerformanceCounterMeter
		{
			public RamMeter() : base(IconIds.Memory, R.T_MemoryUsage) {
			}

			protected override PerformanceCounter CreateCounter() {
				return new PerformanceCounter("Memory", "% Committed Bytes In Use");
			}

			protected override void UpdateDisplay(float counterValue) {
				Label.Text = counterValue.ToString("0") + "%";
				Label.Opacity = (counterValue + 100) / 200;
			}
		}

		sealed class DriveMeter : SinglePerformanceCounterMeter
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
				counterValue = counterValue > 100f ? 0f : (float)Math.Round(100 - counterValue, 0);
				Label.Text = counterValue.ToString("0") + "%";
				Label.Opacity = (Math.Min(50, counterValue) + 50) / 100;
				counterValue = Math.Min(30, Math.Min(counterValue, _SampleSum / SampleCount)) / 30;
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

		sealed class NetworkMeter : MultiPerformanceCounterMeter
		{
			const float MBit = 1024 * 1024, KBit = 1024;

			static readonly Comparer<(string, float)> __Comparer = Comparer<(string, float)>.Create((x, y) => y.Item2.CompareTo(x.Item2));
			static readonly string __0bps = "0" + R.T_Bps;
			bool _TooltipDisplayed;
			float _LastCounter;
			PerformanceCounter[] _Counters;

			public NetworkMeter() : base(IconIds.Network, R.T_NetworkUsage) {
				ToolTipService.SetShowDuration(this, Int32.MaxValue);
				Label.Opacity = 0.4;
			}

			protected override PerformanceCounter[] CreateCounters() {
				var cc = new PerformanceCounterCategory("Network Interface");
				var names = cc.GetInstanceNames();
				var pc = new PerformanceCounter[names.Length];
				for (int i = 0; i < pc.Length; i++) {
					pc[i] = cc.GetCounters(names[i])[0];
				}
				return _Counters = pc;
			}

			protected override void UpdateDisplay(float[] counterValues) {
				var v = counterValues.Sum();
				Label.Text = FlowToReading(v);
				if (v > 30 * KBit) {
					Label.Opacity = v > MBit ? 0.8 : 0.6;
				}
				else if (_LastCounter > 30 * KBit) {
					Label.Opacity = 0.4;
				}
				_LastCounter = v;
				if (_TooltipDisplayed && (ToolTip as CommandToolTip)?.Description is TextBlock t) {
					ShowToolTip(counterValues, t);
				}
			}

			void ShowToolTip(float[] counterValues, TextBlock t) {
				if (counterValues.Length == 1) {
					t.Text = _Counters[0].InstanceName + ": " + FlowToReading(counterValues[0]) + R.T_Bps;
					return;
				}
				ShowMultiValuesOnToolTip(counterValues, t);
			}

			void ShowMultiValuesOnToolTip(float[] counterValues, TextBlock t) {
				(string name, float val)[] cv = new (string, float)[counterValues.Length];
				for (int i = 0; i < cv.Length; i++) {
					var v = counterValues[i];
					cv[i] = v > 0 ? (_Counters[i].InstanceName, v) : default;
				}
				Array.Sort(cv, __Comparer);
				using (var r = Microsoft.VisualStudio.Utilities.ReusableStringBuilder.AcquireDefault(100)) {
					var sb = r.Resource;
					foreach (var (name, val) in cv) {
						if (val != 0) {
							if (sb.Length > 0) {
								sb.AppendLine();
							}
							sb.Append(name).Append(": ").Append(FlowToReading(val)).Append(R.T_Bps);
						}
					}
					t.Text = sb.Length == 0 ? __0bps : sb.ToString();
				}
			}

			static string FlowToReading(float v) {
				return v > MBit ? ((v / MBit).ToString("0.0") + "M")
					: v > KBit ? ((v / KBit).ToString("0.0") + "K")
					: v.ToString("0");
			}

			protected override void OnToolTipOpening(ToolTipEventArgs e) {
				base.OnToolTipOpening(e);
				_TooltipDisplayed = true;
			}

			protected override void OnToolTipClosing(ToolTipEventArgs e) {
				base.OnToolTipClosing(e);
				_TooltipDisplayed = false;
			}
		}
	}
}
