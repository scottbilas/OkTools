using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Zodiacon.DebugHelp {
	static class Extensions {
		public static bool ThrowIfWin32Failed(this bool ok, int error = 0) {
			if(!ok)
				throw new Win32Exception(error != 0 ? error : Marshal.GetLastWin32Error());
			return true;
		}
	}
}
