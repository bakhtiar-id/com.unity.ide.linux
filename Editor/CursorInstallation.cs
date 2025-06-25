/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Unity Technologies.
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using SimpleJSON;
using IOPath = System.IO.Path;

namespace Microsoft.Unity.IdeLinux.Editor
{
    internal class CursorInstallation : LinuxIdeInstallation
    {
        private static readonly IGenerator _generator = GeneratorFactory.GetInstance(GeneratorStyle.SDK);

        public override bool SupportsAnalyzers
        {
            get
            {
                return true;
            }
        }

        public override Version LatestLanguageVersionSupported
        {
            get
            {
                return new Version(13, 0);
            }
        }

        private string GetExtensionPath()
        {
            var cursorExtensionsPath = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cursor", "extensions");
            if (!Directory.Exists(cursorExtensionsPath))
                return null;

            return Directory
                .EnumerateDirectories(cursorExtensionsPath, $"{MicrosoftUnityExtensionId}*") // publisherid.extensionid
                .OrderByDescending(n => n)
                .FirstOrDefault();
        }

        public override string[] GetAnalyzers()
        {
            var vstuPath = GetExtensionPath();
            if (string.IsNullOrEmpty(vstuPath))
                return Array.Empty<string>();

            return GetAnalyzers(vstuPath);
        }

        public override IGenerator ProjectGenerator
        {
            get
            {
                return _generator;
            }
        }

        private static bool IsCandidateForDiscovery(string path)
        {
            // Support for Cursor AI code editor
            return File.Exists(path) && (
                path.EndsWith("cursor", StringComparison.OrdinalIgnoreCase)
            );
        }

        [Serializable]
        internal class CursorManifest
        {
            public string name;
            public string version;
        }

        public static bool TryDiscoverInstallation(string editorPath, out ILinuxIdeInstallation installation)
        {
            installation = null;

            if (string.IsNullOrEmpty(editorPath))
                return false;

            if (!IsCandidateForDiscovery(editorPath))
                return false;

            Version version = null;

            try
            {
                var manifestBase = GetRealPath(editorPath);

                // on Linux, editorPath is a file, in a bin sub-directory
                var parent = Directory.GetParent(manifestBase);
                // but we can link to [cursor]/cursor or [cursor]/bin/cursor
                manifestBase = parent?.Name == "bin" ? parent.Parent?.FullName : parent?.FullName;

                if (manifestBase == null)
                    return false;

                var manifestFullPath = IOPath.Combine(manifestBase, "resources", "app", "package.json");
                if (File.Exists(manifestFullPath))
                {
                    var manifest = JsonUtility.FromJson<CursorManifest>(File.ReadAllText(manifestFullPath));
                    Version.TryParse(manifest.version.Split('-').First(), out version);
                }
            }
            catch (Exception)
            {
                // do not fail if we are not able to retrieve the exact version number
            }

            installation = new CursorInstallation()
            {
                IsPrerelease = false,
                Name = "Cursor" + (version != null ? $" [{version.ToString(3)}]" : string.Empty),
                Path = editorPath,
                Version = version ?? new Version()
            };

            return true;
        }

        public static IEnumerable<ILinuxIdeInstallation> GetLinuxIdeInstallations()
        {
            var candidates = new List<string>();

            // Well known locations for Cursor (deb/rpm packages)
            candidates.Add("/usr/bin/cursor");
            candidates.Add("/bin/cursor");
            candidates.Add("/usr/local/bin/cursor");

            // User-specific installations
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            candidates.Add(IOPath.Combine(homeDir, ".local/bin/cursor"));

            // Common AppImage locations
            candidates.Add(IOPath.Combine(homeDir, "Applications/cursor"));
            candidates.Add(IOPath.Combine(homeDir, "Applications/cursor.AppImage"));
            candidates.Add(IOPath.Combine(homeDir, "Applications/Cursor.AppImage"));
            candidates.Add("/opt/cursor/cursor");

            // Preference ordered base directories relative to which desktop files should be searched
            candidates.AddRange(GetXdgCandidates());

            foreach (var candidate in candidates.Distinct())
            {
                if (TryDiscoverInstallation(candidate, out var installation))
                    yield return installation;
            }
        }

        private static readonly Regex DesktopFileExecEntry = new Regex(@"Exec=(\S+)", RegexOptions.Singleline | RegexOptions.Compiled);

        private static IEnumerable<string> GetXdgCandidates()
        {
            var envdirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
            if (string.IsNullOrEmpty(envdirs))
            {
                // Default XDG data directories if not set
                envdirs = "/usr/local/share:/usr/share";
            }

            var dirs = envdirs.Split(':');
            var desktopFiles = new[] { "cursor.desktop" };

            // Also check user-specific applications directory
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var userAppsDir = IOPath.Combine(homeDir, ".local/share");
            var allDirs = dirs.Concat(new[] { userAppsDir }).ToArray();

            foreach (var dir in allDirs)
            {
                foreach (var desktopFileName in desktopFiles)
                {
                    Match match = null;

                    try
                    {
                        var desktopFile = IOPath.Combine(dir, "applications", desktopFileName);
                        if (!File.Exists(desktopFile))
                            continue;

                        var content = File.ReadAllText(desktopFile);
                        match = DesktopFileExecEntry.Match(content);
                    }
                    catch
                    {
                        // ignore and continue
                        continue;
                    }

                    if (match == null || !match.Success)
                        continue;

                    var pathFromDesktopFile = match.Groups[1].Value;
                    if (string.IsNullOrEmpty(pathFromDesktopFile))
                        continue;

                    yield return pathFromDesktopFile;
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("libc")]
        private static extern int readlink(string path, byte[] buffer, int buflen);

        internal static string GetRealPath(string path)
        {
            byte[] buf = new byte[512];
            int ret = readlink(path, buf, buf.Length);
            if (ret == -1) return path;
            char[] cbuf = new char[512];
            int chars = System.Text.Encoding.Default.GetChars(buf, 0, ret, cbuf, 0);
            return new String(cbuf, 0, chars);
        }

        public override void CreateExtraFiles(string projectDirectory)
        {
            try
            {
                var vscodeDirectory = IOPath.Combine(projectDirectory, ".vscode");
                if (!Directory.Exists(vscodeDirectory))
                    Directory.CreateDirectory(vscodeDirectory);

                CreateLaunchFile(vscodeDirectory, enablePatch: true);
                CreateSettingsFile(vscodeDirectory, enablePatch: true);
                CreateRecommendedExtensionsFile(vscodeDirectory, enablePatch: true);
            }
            catch (IOException)
            {
            }
        }

        private const string DefaultLaunchFileContent = @"{
    ""version"": ""0.2.0"",
    ""configurations"": [
        {
            ""name"": ""Attach to Unity"",
            ""type"": ""vstuc"",
            ""request"": ""attach""
        }
     ]
}";

        private static void CreateLaunchFile(string vscodeDirectory, bool enablePatch)
        {
            var launchFile = IOPath.Combine(vscodeDirectory, "launch.json");
            if (File.Exists(launchFile))
            {
                if (enablePatch)
                    PatchLaunchFile(launchFile);

                return;
            }

            File.WriteAllText(launchFile, DefaultLaunchFileContent);
        }

        private static void PatchLaunchFile(string launchFile)
        {
            try
            {
                var content = File.ReadAllText(launchFile);
                var launchData = JSON.Parse(content);
                var configurations = launchData["configurations"];

                if (configurations == null)
                {
                    return;
                }

                const string typeKey = "type";

                if (configurations.Linq.Any(entry => entry.Value[typeKey].Value == "vstuc"))
                    return;

                var unityAttachConfiguration = JSON.Parse(@"{
    ""name"": ""Attach to Unity"",
    ""type"": ""vstuc"",
    ""request"": ""attach""
}");

                configurations.Add(unityAttachConfiguration);

                WriteAllTextFromJObject(launchFile, launchData);
            }
            catch (Exception)
            {
                // do not fail patching
            }
        }

        private void CreateSettingsFile(string vscodeDirectory, bool enablePatch)
        {
            var settingsFile = IOPath.Combine(vscodeDirectory, "settings.json");
            if (File.Exists(settingsFile))
            {
                if (enablePatch)
                    PatchSettingsFile(settingsFile);

                return;
            }

            const string excludes = @"    ""files.exclude"": {
        ""**/.DS_Store"": true,
        ""**/.git"": true,
        ""**/.vs"": true,
        ""**/.gitmodules"": true,
        ""**/.vsconfig"": true,
        ""**/*.booproj"": true,
        ""**/*.pidb"": true,
        ""**/*.suo"": true,
        ""**/*.user"": true,
        ""**/*.userprefs"": true,
        ""**/*.unityproj"": true,
        ""**/*.dll"": true,
        ""**/*.exe"": true,
        ""**/*.pdf"": true,
        ""**/*.mid"": true,
        ""**/*.midi"": true,
        ""**/*.wav"": true,
        ""**/*.gif"": true,
        ""**/*.ico"": true,
        ""**/*.jpg"": true,
        ""**/*.jpeg"": true,
        ""**/*.png"": true,
        ""**/*.psd"": true,
        ""**/*.tga"": true,
        ""**/*.tif"": true,
        ""**/*.tiff"": true,
        ""**/*.3ds"": true,
        ""**/*.3DS"": true,
        ""**/*.fbx"": true,
        ""**/*.FBX"": true,
        ""**/*.lxo"": true,
        ""**/*.LXO"": true,
        ""**/*.ma"": true,
        ""**/*.MA"": true,
        ""**/*.obj"": true,
        ""**/*.OBJ"": true,
        ""**/*.asset"": true,
        ""**/*.cubemap"": true,
        ""**/*.flare"": true,
        ""**/*.mat"": true,
        ""**/*.meta"": true,
        ""**/*.prefab"": true,
        ""**/*.unity"": true,
        ""build/"": true,
        ""Build/"": true,
        ""Library/"": true,
        ""library/"": true,
        ""obj/"": true,
        ""Obj/"": true,
        ""Logs/"": true,
        ""logs/"": true,
        ""ProjectSettings/"": true,
        ""UserSettings/"": true,
        ""temp/"": true,
        ""Temp/"": true
    }";

            var content = @"{
" + excludes + @",
    ""files.associations"": {
        ""*.asset"": ""yaml"",
        ""*.meta"": ""yaml"",
        ""*.prefab"": ""yaml"",
        ""*.unity"": ""yaml"",
    },
    ""explorer.fileNesting.enabled"": true,
    ""explorer.fileNesting.patterns"": {
        ""*.sln"": ""*.csproj"",
    },
    ""dotnet.defaultSolution"": """ + IOPath.GetFileName(ProjectGenerator.SolutionFile()) + @"""
}";

            File.WriteAllText(settingsFile, content);
        }

        private void PatchSettingsFile(string settingsFile)
        {
            try
            {
                var content = File.ReadAllText(settingsFile);
                var settingsData = JSON.Parse(content);
                var excludes = settingsData["files.exclude"];
                var defaultSolution = settingsData["dotnet.defaultSolution"];
                var solutionFile = IOPath.GetFileName(ProjectGenerator.SolutionFile());
                var patched = false;

                if (excludes == null)
                {
                    return;
                }

                if (defaultSolution == null || defaultSolution.Value != solutionFile)
                {
                    settingsData["dotnet.defaultSolution"] = solutionFile;
                    patched = true;
                }

                if (!patched)
                    return;

                WriteAllTextFromJObject(settingsFile, settingsData);
            }
            catch (Exception)
            {
                // do not fail patching
            }
        }

        private const string MicrosoftUnityExtensionId = "visualstudiotoolsforunity.vstuc";
        private const string DefaultRecommendedExtensionsContent = @"{
    ""recommendations"": [
      """ + MicrosoftUnityExtensionId + @"""
    ]
}
";

        private static void CreateRecommendedExtensionsFile(string vscodeDirectory, bool enablePatch)
        {
            // see https://tattoocoder.com/recommending-vscode-extensions-within-your-open-source-projects/
            var extensionFile = IOPath.Combine(vscodeDirectory, "extensions.json");
            if (File.Exists(extensionFile))
            {
                if (enablePatch)
                    PatchRecommendedExtensionsFile(extensionFile);

                return;
            }

            File.WriteAllText(extensionFile, DefaultRecommendedExtensionsContent);
        }

        private static void PatchRecommendedExtensionsFile(string extensionFile)
        {
            try
            {
                var content = File.ReadAllText(extensionFile);
                var extensionData = JSON.Parse(content);
                var recommendations = extensionData["recommendations"];

                if (recommendations == null)
                {
                    return;
                }

                if (recommendations.Linq.Any(entry => entry.Value.Value == MicrosoftUnityExtensionId))
                    return;

                recommendations.Add(MicrosoftUnityExtensionId);

                WriteAllTextFromJObject(extensionFile, extensionData);
            }
            catch (Exception)
            {
                // do not fail patching
            }
        }

        private static void WriteAllTextFromJObject(string file, JSONNode node)
        {
            using (var fs = File.Open(file, FileMode.Create))
            using (var sw = new StreamWriter(fs))
            {
                // Keep formatting/indent in sync with default contents
                sw.Write(node.ToString(aIndent: 4));
            }
        }

        public override bool Open(string path, int line, int column, string solution)
        {
            var application = Path;

            line = Math.Max(1, line);
            column = Math.Max(0, column);

            var directory = IOPath.GetDirectoryName(solution);
            var workspace = TryFindWorkspace(directory);

            var target = workspace ?? directory;

            ProcessRunner.Start(string.IsNullOrEmpty(path)
                ? ProcessStartInfoFor(application, $"\"{target}\"")
                : ProcessStartInfoFor(application, $"\"{target}\" -g \"{path}\":{line}:{column}"));

            return true;
        }

        private static string TryFindWorkspace(string directory)
        {
            var files = Directory.GetFiles(directory, "*.code-workspace", SearchOption.TopDirectoryOnly);
            if (files.Length == 0 || files.Length > 1)
                return null;

            return files[0];
        }

        private static ProcessStartInfo ProcessStartInfoFor(string application, string arguments)
        {
            return ProcessRunner.ProcessStartInfoFor(application, arguments, redirect: false);
        }

        public static void Initialize()
        {
        }
    }
}
