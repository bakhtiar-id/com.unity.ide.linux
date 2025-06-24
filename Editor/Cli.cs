/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
using System;
using System.Linq;
using Unity.CodeEditor;

namespace Microsoft.Unity.IdeLinux.Editor
{
	internal static class Cli
	{
		internal static void Log(string message)
		{       // Use writeline here, instead of UnityEngine.Debug.Log to not include the stacktrace in the editor.log
			Console.WriteLine($"[IdeLinux.Editor.{nameof(Cli)}] {message}");
		}

		internal static string GetInstallationDetails(ILinuxIdeInstallation installation)
		{
			return $"{installation.ToCodeEditorInstallation().Name} Path:{installation.Path}, LanguageVersionSupport:{installation.LatestLanguageVersionSupported} AnalyzersSupport:{installation.SupportsAnalyzers}";
		}

		internal static void GenerateSolutionWith(LinuxIdeEditor vse, string installationPath)
		{
			if (vse != null && vse.TryGetLinuxIdeInstallationForPath(installationPath, lookupDiscoveredInstallations: true, out var vsi))
			{
				Log($"Using {GetInstallationDetails(vsi)}");
				vse.SyncAll();
			}
			else
			{
				Log($"No IDE installation found in ${installationPath}!");
			}
		}

		internal static void GenerateSolution()
		{
			if (CodeEditor.CurrentEditor is LinuxIdeEditor vse)
			{
				Log($"Using default editor settings for IDE installation");
				GenerateSolutionWith(vse, CodeEditor.CurrentEditorInstallation);
			}
			else
			{
				Log($"IDE is not set as your default editor, looking for installations");
				try
				{
					var installations = Discovery
				.GetLinuxIdeInstallations()
					.Cast<LinuxIdeInstallation>()
					.OrderByDescending(vsi => !vsi.IsPrerelease)
					.ThenBy(vsi => vsi.Version)
					.ToArray();

					foreach (var vsi in installations)
					{
						Log($"Detected {GetInstallationDetails(vsi)}");
					}

					var installation = installations
							.FirstOrDefault();

					if (installation != null)
					{
						var current = CodeEditor.CurrentEditorInstallation;
						try
						{
							CodeEditor.SetExternalScriptEditor(installation.Path);
							GenerateSolutionWith(CodeEditor.CurrentEditor as LinuxIdeEditor, installation.Path);
						}
						finally
						{
							CodeEditor.SetExternalScriptEditor(current);
						}
					}
					else
					{
						Log($"No IDE installation found!");
					}
				}
				catch (Exception ex)
				{
					Log($"Error detecting IDE installations: {ex}");
				}
			}
		}
	}
}
