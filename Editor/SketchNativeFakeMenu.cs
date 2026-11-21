using UnityEditor;

namespace Sketch.Sdk.Editor
{
    // Toggles the Editor fake native in-rect (SketchEditorNativeView, same layout + countdown close as Android).
    public static class SketchNativeFakeMenu
    {
        const string MENU_PATH = "Tools/Sketch SDK/Editor Native Fake";

        [MenuItem(MENU_PATH)]
        static void Toggle() => SketchEditorNativeView.Enabled = !SketchEditorNativeView.Enabled;

        [MenuItem(MENU_PATH, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MENU_PATH, SketchEditorNativeView.Enabled);
            return true;
        }
    }
}
