using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MouseHook
{
	public sealed class MouseHookSingleton
	{
		public event MouseHookEventHandler MouseHookEvent;

		public delegate void MouseHookEventHandler(int nCode, IntPtr wParam, IntPtr lParam, int x, int y);

		private const int WH_MOUSE_LL = 14;
		private static MouseHookSingleton s_instance;

		private LowLevelMouseProc _proc;
		private IntPtr _hookID = IntPtr.Zero;
		private Thread _mouseHookThread;
		private ApplicationContext _threadApplicationContext;

		public static MouseHookSingleton Instance
		{
			get { return s_instance ?? (s_instance = new MouseHookSingleton()); }
		}

		public void Initialize()
		{
			StartMouseHookThread();
		}

		public void Stop()
		{
			StopMouseHookThread();
			GC.SuppressFinalize(this);
		}

		~MouseHookSingleton()
		{
			StopMouseHookThread();
		}

		private void StopMouseHookThread()
		{
            UnsetHook();

			ApplicationContext threadApplicationContext = _threadApplicationContext;

			if (threadApplicationContext != null)
			{
				threadApplicationContext.ExitThread();
				_threadApplicationContext = null;
			}
		}

		private void StartMouseHookThread()
		{
			_mouseHookThread = new Thread(MouseHookThread)
			                   	{
			                   		Name = "MouseHookThread",
									IsBackground = true,
									Priority = ThreadPriority.AboveNormal
			                   	};
			_mouseHookThread.Start();
		}

		private void MouseHookThread()
		{
			_proc = HookCallback;
			_hookID = SetHook(_proc);

			try
			{
				_threadApplicationContext = new ApplicationContext();
				Application.Run(_threadApplicationContext);
			}
			catch (ThreadAbortException)
			{
				Thread.ResetAbort();
                Application.ExitThread();
			}
			finally
			{
				UnsetHook();
			}
		}

		private static IntPtr SetHook(LowLevelMouseProc proc)
		{
			using (Process curProcess = Process.GetCurrentProcess())
			{
				using (ProcessModule curModule = curProcess.MainModule)
				{
					Debug.Assert(curModule != null);
					return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
				}
			}
		}

		private void UnsetHook()
		{
			if (_hookID != IntPtr.Zero)
			{
				UnhookWindowsHookEx(_hookID);
				_hookID = IntPtr.Zero;
			}
		}

		private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
		{
			if (nCode >= 0)
			{
				try
				{
					MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT) Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

					// raise the mouse hook event
					if (MouseHookEvent != null)
					{
						MouseHookEvent(nCode, wParam, lParam, hookStruct.pt.x, hookStruct.pt.y);
					}
				}
				catch (Exception ex)
				{
				}
			}

			return CallNextHookEx(_hookID, nCode, wParam, lParam);
		}

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		[return : MarshalAs(UnmanagedType.Bool)]
		private static extern bool UnhookWindowsHookEx(IntPtr hhk);

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr GetModuleHandle(string lpModuleName);

		#region Nested type: LowLevelMouseProc

		private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

		#endregion

		#region Nested type: MSLLHOOKSTRUCT

		[StructLayout(LayoutKind.Sequential)]
		private struct MSLLHOOKSTRUCT
		{
			public POINT pt;
			public uint mouseData;
			public uint flags;
			public uint time;
			public IntPtr dwExtraInfo;
		}

		#endregion

		#region Nested type: POINT

		[StructLayout(LayoutKind.Sequential)]
		private struct POINT
		{
			public int x;
			public int y;
		}

		#endregion
	}

	public enum MouseMessages
	{
		WM_LBUTTONDOWN = 0x0201,
		WM_LBUTTONUP = 0x0202,
		WM_MOUSEMOVE = 0x0200,
		WM_MOUSEWHEEL = 0x020A,
		WM_RBUTTONDOWN = 0x0204,
		WM_RBUTTONUP = 0x0205
	}

	public enum MouseConstants
	{
		MK_LBUTTON = 0x01,
		MK_RBUTTON = 0x02,
	}
}