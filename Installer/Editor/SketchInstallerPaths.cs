namespace Sketch.Sdk.Installer
{
    static class SketchInstallerPaths
    {
        public const string REPO_URL = "https://github.com/sketch-studio/sketch-sdk.git";
        public const string ARCHIVE_URL_FORMAT = "https://github.com/sketch-studio/sketch-sdk/archive/refs/tags/{0}.zip";
        public const string INSTALL_ROOT = "Assets/SketchSdk";
        public const string MANIFEST_PATH = INSTALL_ROOT + "/.sketch-install.txt";
        public const string BACKUP_ROOT = "Assets/SketchSdk~backup";
        public const string SPLASH_SCENE_PATH = INSTALL_ROOT + "/Splash/SketchSplash.unity";
        public const string BUILD_SPLASH_MENU = "Sketch SDK/Build Splash Scene";
        public const string UPM_PACKAGE_NAME = "com.sketch.sdk";

        // Folders and files copied from the package root. Samples~, Tests~, Installer, docs stay out.
        public static readonly string[] PAYLOAD_DIRECTORIES = { "Runtime", "Editor", "Tests", "Splash" };
        public static readonly string[] PAYLOAD_FILES = { "README.md", "CHANGELOG.md", "package.json" };

        public const string SESSION_DECLINED = "SketchSdkInstaller.DeclinedTag";
        public const string SESSION_CHECKED = "SketchSdkInstaller.CheckedThisSession";
        public const string SESSION_PENDING_FINALIZE = "SketchSdkInstaller.PendingFinalize";
    }
}
