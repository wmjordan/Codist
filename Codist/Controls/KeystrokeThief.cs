using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;

namespace Codist.Controls
{
	/// <summary>
	/// <para>Stops keystrokes on a WPF Window or Adornment from propagating to the Visual Studio code editor.</para>
	/// <para>See: https://gist.github.com/OmerRaviv/69dfd80fd5c13d3caef1</para>
	/// 
	/// <para>"The reason that keys like Backspace and Delete do not work in your WPF windows/Adornments is due to Visual Studio's usage of IOleComponentManager and IOleComponent. </para>
	/// <para>Visual Studio and WinForms both use IOleComponent as a way of tracking the active component in the application.</para> 
	///
	/// <para>WPF does not implement IOleComponent or use the IOleComponentManager for its windows. This means that when your WPF window is active, Visual Studio doesn't know that its 
	/// primary component should not be processing command keybindings. Since "Backspace", "Delete", and several other keys are bound to commands for the text editor, 
	/// Visual Studio continues processing those keystrokes as command bindings."</para>
	/// 
	/// <para>Adapted from code originally received from Microsoft as an answer to Omer's Connect ticket,</para>
	/// <para>https://connect.microsoft.com/VisualStudio/feedback/details/549866/msdn-visual-studio-extensibility-forum-backspace-tab-and-enter-key-are-not-captured-in-wpf-window-which-exist-in-a-package</para>
	/// </summary>
	public class KeystrokeThief
	{
		readonly IOleComponentManager _manager;
		uint _componentCookie;

		public KeystrokeThief(IOleComponentManager manager) {
			if (manager == null) {
				throw new ArgumentNullException("manager");
			}

			_manager = manager;
		}

		public bool IsStealing { get; set; }

		void RegisterDummyComponent() {
			var component = new EmptyOleComponent();
			var regInfo = new OLECRINFO { grfcrf = 0U, grfcadvf = 0U, uIdleTimeInterval = 0U };
			regInfo.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(regInfo);
			int result = _manager.FRegisterComponent(component, new[] { regInfo }, out _componentCookie);
			if (!ErrorHandler.Succeeded(result)) {
				throw new InvalidOperationException("Could not register the OleComponent");
			}

			_manager.FOnComponentActivate(_componentCookie);
		}

		void UnregisterDummyComponent() {
			_manager.FRevokeComponent(_componentCookie);
			_componentCookie = 0;
		}


		public void StartStealing() {
			RegisterDummyComponent();
			IsStealing = true;
		}

		public void StopStealing() {
			UnregisterDummyComponent();
			IsStealing = false;
		}

		/// <summary>
		/// The default IOleComponent for Visual Studio will translate keystrokes into
		/// commands for the active IVsWindowFrame.  By activating this component when this window is active,
		/// it will allow normal keyboard processing without command keybindings.
		/// </summary>
		class EmptyOleComponent : IOleComponent
		{
			public int FContinueMessageLoop(uint uReason, IntPtr pvLoopData, MSG[] pMsgPeeked) {
				return VSConstants.S_OK;
			}

			public int FDoIdle(uint grfidlef) {
				return VSConstants.S_OK;
			}

			public int FPreTranslateMessage(MSG[] pMsg) {
				return VSConstants.S_OK;
			}

			public int FQueryTerminate(int fPromptUser) {
				return 1;
			}

			public int FReserved1(uint dwReserved, uint message, IntPtr wParam, IntPtr lParam) {
				return VSConstants.S_OK;
			}

			public IntPtr HwndGetWindow(uint dwWhich, uint dwReserved) {
				return IntPtr.Zero;
			}

			public void OnActivationChange(IOleComponent pic, int fSameComponent, OLECRINFO[] pcrinfo, int fHostIsActivating, OLECHOSTINFO[] pchostinfo, uint dwReserved) {
			}

			public void OnAppActivate(int fActive, uint dwOtherThreadID) {
			}

			public void OnEnterState(uint uStateID, int fEnter) {
			}

			public void OnLoseActivation() {
			}

			public void Terminate() {
			}
		}
	}
}