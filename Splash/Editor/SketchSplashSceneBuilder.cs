using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sketch.Sdk.Splash.Editor
{
    /// <summary>
    /// Menu Sketch SDK/Build Splash Scene: (re)creates the SDK-owned splash scene, wires it to the hook asset
    /// (created once at Assets/Resources/SketchSplashHook.asset) and puts it at build index 0. Idempotent: the scene is
    /// rebuilt from scratch every run; the hook asset is never overwritten.
    /// </summary>
    static class SketchSplashSceneBuilder
    {
        public const string SCENE_PATH = "Assets/SketchSdk/Splash/SketchSplash.unity";
        const string MENU = "Sketch SDK/Build Splash Scene";
        static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

        [MenuItem(MENU, priority = 50)]
        public static void Build()
        {
            SketchSplashHook hook = FindOrCreateHook();

            Scene scene;
            bool additive = CanCreateAdditive();
            if (additive) scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            else
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            try
            {
                // A Single-mode NewScene unloads unused assets, so a hook loaded before it can be a destroyed object.
                if (hook == null) hook = FindOrCreateHook();
                Populate(scene, hook);
                Directory.CreateDirectory(Path.GetDirectoryName(SCENE_PATH));
                if (!EditorSceneManager.SaveScene(scene, SCENE_PATH)) throw new IOException("could not save " + SCENE_PATH);
            }
            finally
            {
                if (additive && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }

            EnsureFirstInBuild(SCENE_PATH);
            Debug.Log("[SketchSplash] Built " + SCENE_PATH + " (build index 0), hook: " + AssetDatabase.GetAssetPath(hook));
        }

        // Additive creation fails while an untitled scene is open; saving over an open splash scene needs Single mode.
        static bool CanCreateAdditive()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                string path = SceneManager.GetSceneAt(i).path;
                if (string.IsNullOrEmpty(path) || path == SCENE_PATH) return false;
            }
            return SceneManager.sceneCount > 0;
        }

        static SketchSplashHook FindOrCreateHook()
        {
            SketchSplashHook hook = AssetDatabase.LoadAssetAtPath<SketchSplashHook>(SketchSplashHook.DEFAULT_ASSET_PATH);
            if (hook != null) return hook;
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(SketchSplashHook));
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Resources/")) return AssetDatabase.LoadAssetAtPath<SketchSplashHook>(path);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(SketchSplashHook.DEFAULT_ASSET_PATH));
            hook = ScriptableObject.CreateInstance<SketchSplashHook>();
            AssetDatabase.CreateAsset(hook, SketchSplashHook.DEFAULT_ASSET_PATH);
            AssetDatabase.SaveAssets();
            return hook;
        }

        static void Populate(Scene scene, SketchSplashHook hook)
        {
            var cameraGo = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraGo, scene);
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            camera.orthographic = true;

            var canvasGo = new GameObject("SketchSplashCanvas", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            Image background = CreateImage("Background", canvasGo.transform, null);
            Stretch(background.rectTransform);
            background.color = hook.BackgroundColor;
            // Aspect-fill for a background sprite; the controller turns it off (stretch) for a plain colour.
            AspectRatioFitter backgroundFitter = background.gameObject.AddComponent<AspectRatioFitter>();
            backgroundFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            backgroundFitter.aspectRatio = ReferenceResolution.x / ReferenceResolution.y;
            backgroundFitter.enabled = false;

            Image brand = CreateImage("Brand", canvasGo.transform, hook.BrandSprite);
            Place(brand.rectTransform, new Vector2(0.5f, hook.BrandAnchorY), hook.BrandSize);
            brand.preserveAspect = true;

            Text title = CreateText("Title", canvasGo.transform);
            Place(title.rectTransform, new Vector2(0.5f, 0.36f), new Vector2(900f, 140f));

            var barRoot = new GameObject("LoadingBar", typeof(RectTransform));
            barRoot.transform.SetParent(canvasGo.transform, false);
            Place((RectTransform)barRoot.transform, new Vector2(0.5f, hook.BarAnchorY), hook.BarSize);
            CanvasGroup barGroup = barRoot.AddComponent<CanvasGroup>();
            barGroup.blocksRaycasts = false;
            barGroup.interactable = false;

            Sprite rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Sprite trackSprite = hook.BarBackgroundSprite != null ? hook.BarBackgroundSprite : rounded;
            Image track = CreateImage("Track", barRoot.transform, trackSprite);
            Stretch(track.rectTransform);
            track.type = trackSprite != null && trackSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            track.color = hook.BarBackgroundColor;

            // Filled + 9-slice image: full-width rect trimmed by fillAmount, rounded caps kept at low values.
            Sprite fillSprite = hook.BarFillSprite != null ? hook.BarFillSprite : rounded;
            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(barRoot.transform, false);
            SketchSlicedFillImage fill = fillGo.AddComponent<SketchSlicedFillImage>();
            fill.sprite = fillSprite;
            fill.raycastTarget = false;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            RectTransform fillRect = fill.rectTransform;
            float inset = hook.BarFillInset;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(inset, inset);
            fillRect.offsetMax = new Vector2(-inset, -inset);
            fill.color = hook.BarColor;

            var labelSlot = new GameObject("LabelSlot", typeof(RectTransform));
            labelSlot.transform.SetParent(barRoot.transform, false);
            var slotRect = (RectTransform)labelSlot.transform;
            slotRect.anchorMin = Vector2.zero;
            slotRect.anchorMax = new Vector2(1f, 0f);
            slotRect.pivot = new Vector2(0.5f, 1f);
            slotRect.anchoredPosition = new Vector2(0f, -hook.BarLabelGap);
            slotRect.sizeDelta = new Vector2(0f, hook.BarLabelHeight);

            CreateEventSystem(scene);

            var controllerGo = new GameObject("SketchSplashController");
            SceneManager.MoveGameObjectToScene(controllerGo, scene);
            SketchSplashController controller = controllerGo.AddComponent<SketchSplashController>();
            var so = new SerializedObject(controller);
            so.FindProperty("_hook").objectReferenceValue = hook;
            so.FindProperty("_background").objectReferenceValue = background;
            so.FindProperty("_backgroundFitter").objectReferenceValue = backgroundFitter;
            so.FindProperty("_brand").objectReferenceValue = brand;
            so.FindProperty("_title").objectReferenceValue = title;
            so.FindProperty("_barGroup").objectReferenceValue = barGroup;
            so.FindProperty("_barTrack").objectReferenceValue = track;
            so.FindProperty("_barLabelSlot").objectReferenceValue = slotRect;
            // Older scenes with a stock sliced Image fall back to driving anchorMax; this one uses fillAmount.
            so.FindProperty("_barFill").objectReferenceValue = fill;
            so.FindProperty("_barFillRect").objectReferenceValue = fillRect;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Image CreateImage(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        static Text CreateText(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Text text = go.AddComponent<Text>();
            text.font = LoadBuiltinFont();
            text.fontSize = 72;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 24;
            text.resizeTextMaxSize = 72;
            text.raycastTarget = false;
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.04f, 0.05f, 0.12f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);
            return text;
        }

        static Font LoadBuiltinFont()
        {
            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (ArgumentException) { }
            if (font == null)
                try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch (ArgumentException) { }
            return font;
        }

        static void CreateEventSystem(Scene scene)
        {
            var go = new GameObject("EventSystem");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<EventSystem>();
#if ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<StandaloneInputModule>();
#else
            // New Input System only: add its UI module without a compile-time dependency on the package.
            Type module = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (module != null) go.AddComponent(module);
            else Debug.LogWarning("[SketchSplash] Input System UI module not found; EventSystem has no input module.");
#endif
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        static void EnsureFirstInBuild(string scenePath)
        {
            EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
            if (current.Length > 0 && current[0].path == scenePath && current[0].enabled) return;
            var scenes = new List<EditorBuildSettingsScene>(current.Length + 1) { new EditorBuildSettingsScene(scenePath, true) };
            foreach (EditorBuildSettingsScene s in current)
                if (s.path != scenePath) scenes.Add(s);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
