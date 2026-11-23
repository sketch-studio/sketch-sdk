# Sketch SDK Installer

Editor-only package. It installs and updates the Sketch SDK as plain files in `Assets/SketchSdk` (like Bounce SDK), with safer updates.

Install: **Package Manager > + > Add package from git URL**

```
https://github.com/sketch-studio/sketch-sdk.git?path=/Installer#v1.3.0
```

- Checks the newest `vX.Y.Z` tag once per editor session (`git ls-remote`, needs git on PATH) and offers Install / Update.
- Copies `Runtime`, `Editor`, `Tests`, `Splash`, `Plugins`, `README.md`, `CHANGELOG.md`, `package.json` and their `.meta` files.
- `Assets/SketchSdk/.sketch-install.txt` = installed tag + owned files + SHA1. Locally edited owned files are backed up to `SketchSdkBackups~/<timestamp>/` after a confirm dialog, outside Unity's imported Assets tree.
- Never touches non-owned files, `SketchSdkSettings` or `SketchSplashHook`.
- After install: removes an old `com.sketch.sdk` entry from `Packages/manifest.json`, resolves packages, builds the splash scene if missing and puts it at build index 0.

Menus: **Sketch SDK > Check For Updates**, **Reinstall Current**, **Install Version...**. Skipped in batch mode.
