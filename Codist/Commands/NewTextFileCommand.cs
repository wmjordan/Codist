using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Codist.Commands;

/// <summary>A command which opens a new plain text code document window.</summary>
internal static class NewTextFileCommand
{
	public static void Initialize() {
		Command.NewTextFile.Register(Execute);
	}

	static void Execute(object sender, EventArgs e) {
		ThreadHelper.ThrowIfNotOnUIThread();
		CodistPackage.DTE.ItemOperations.NewFile();
		//IVsUIShell uiShell = ServicesHelper.Get<IVsUIShell, SVsUIShell>();
		//var adapterFactory = ServicesHelper.Instance.EditorAdaptersFactory;
		//var documentFactory = ServicesHelper.Instance.TextDocumentFactory;

		//if (uiShell == null || adapterFactory == null || documentFactory == null) return;

		//// 2. 【关键步骤】先创建 COM 层的 IVsTextBuffer 适配器
		//// 这一步会同时创建底层的 ITextBuffer 并建立连接
		//IVsTextBuffer vsTextBuffer = adapterFactory.CreateVsTextBufferAdapter(CodistPackage.Instance);

		//vsTextBuffer.InitializeContent("content", 7);
		//// 3. 获取关联的 MEF ITextBuffer
		//var textBuffer = adapterFactory.GetDataBuffer(vsTextBuffer);

		//// 5. 创建 ITextDocument

		//// 6. 准备视图
		//var serviceProvider = ServiceProvider.GlobalProvider.GetService(typeof(Microsoft.VisualStudio.OLE.Interop.IServiceProvider)) as Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
		//IVsCodeWindow codeWindow = adapterFactory.CreateVsCodeWindowAdapter(serviceProvider);
		//codeWindow.SetBuffer((IVsTextLines)vsTextBuffer);

		//// 7. 转换指针并创建窗口
		//IntPtr pUnkDocView = IntPtr.Zero;
		//IntPtr pUnkDocData = IntPtr.Zero;

		//try {
		//	pUnkDocView = Marshal.GetIUnknownForObject(codeWindow);
		//	pUnkDocData = Marshal.GetIUnknownForObject(vsTextBuffer);

		//	Guid editorTypeGuid = VSConstants.GUID_TextEditorFactory;
		//	Guid cmdUiGuid = VSConstants.VsEditorFactoryGuid.TextEditor_guid;
		//	IVsWindowFrame windowFrame;

		//	int hr = uiShell.CreateDocumentWindow(
		//		0,
		//		System.IO.Path.Combine(System.IO.Path.GetTempPath(), "New.txt"),
		//		null,
		//		(uint)VSConstants.VSITEMID.Nil,
		//		pUnkDocView,
		//		pUnkDocData,
		//		ref editorTypeGuid,
		//		VSConstants.LOGVIEWID.TextView_string,
		//		ref cmdUiGuid,
		//		null,
		//		"New.txt", // 窗口标题通常只显示文件名
		//		"",
		//		new int[1],
		//		out windowFrame);

		//	if (hr == VSConstants.S_OK && windowFrame != null) {
		//		windowFrame.Show();
		//	}
		//}
		//finally {
		//	if (pUnkDocView != IntPtr.Zero) Marshal.Release(pUnkDocView);
		//	if (pUnkDocData != IntPtr.Zero) Marshal.Release(pUnkDocData);
		//}
	}
}
