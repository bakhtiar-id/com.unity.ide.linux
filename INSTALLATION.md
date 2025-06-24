# Installation Guide for Linux IDE Support Package

## 🚀 Quick Installation

### Method 1: Package Manager (Recommended)
1. Open Unity Editor
2. Go to `Window > Package Manager`
3. Click the `+` button in the top-left
4. Select `Add package from disk...`
5. Navigate to this folder and select the `package.json` file
6. Unity will install the package automatically

### Method 2: Packages Folder
1. Copy this entire folder to your project's `Packages/` directory
2. The structure should be:
   ```
   YourProject/
   ├── Assets/
   ├── Packages/
   │   └── com.unity.ide.linux/    ← This folder
   │       ├── package.json
   │       ├── Editor/
   │       └── Documentation~/
   └── ProjectSettings/
   ```

## ✅ Verification

After installation:
1. Check Package Manager - you should see "IDE Support for Linux" listed
2. Go to `Edit > Preferences > External Tools > External Script Editor`
3. The plugin will automatically detect VS Code Insiders installations
4. It will generate `.csproj` files for IntelliSense support

## 🎯 Currently Supports

- **VS Code Insiders** on Linux (all installation methods)
- Automatic discovery of installations
- Project generation for IntelliSense
- Debug configuration setup
- Unity integration

## 🔮 Future Support (Planned)

- **Cursor** editor
- **Zed** editor  
- Other VS Code engine-based editors

## ⚠️ Important Notes

- **DO NOT** put this in your `Assets/` folder
- This package is specifically for Linux systems
- It complements (doesn't replace) the official Unity VS Code support
- Focuses on VS Code engine-based editors that aren't covered by the official plugin

## 🐛 Troubleshooting

If you see assembly conflicts:
1. Make sure you only have one copy of this package installed
2. Check that no old assembly definition files remain
3. The package should appear as "IDE Support for Linux" in Package Manager

## 📝 Package Details

- **Package Name**: `com.unity.ide.linux`
- **Assembly**: `Unity.IdeLinux.Editor`
- **Namespace**: `Microsoft.Unity.IdeLinux.Editor`
- **Unity Version**: 2019.4+
