/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Unity Technologies.
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Unity.IdeLinux.Editor
{
	internal static class Discovery
	{
		public static IEnumerable<ILinuxIdeInstallation> GetLinuxIdeInstallations()
		{
			// Currently supports VS Code Insiders and Cursor, with infrastructure ready for other VS Code engine-based editors
			foreach (var installation in VSCodeInsidersInstallation.GetLinuxIdeInstallations())
				yield return installation;

			foreach (var installation in CursorInstallation.GetLinuxIdeInstallations())
				yield return installation;
		}

		public static bool TryDiscoverInstallation(string editorPath, out ILinuxIdeInstallation installation)
		{
			try
			{
				// Currently supports VS Code Insiders and Cursor, with infrastructure ready for other VS Code engine-based editors
				if (VSCodeInsidersInstallation.TryDiscoverInstallation(editorPath, out installation))
					return true;

				if (CursorInstallation.TryDiscoverInstallation(editorPath, out installation))
					return true;
			}
			catch (IOException)
			{
				installation = null;
			}

			return false;
		}

		public static void Initialize()
		{
			// Initialize support for VS Code engine-based editors
			VSCodeInsidersInstallation.Initialize();
			CursorInstallation.Initialize();
		}
	}
}
