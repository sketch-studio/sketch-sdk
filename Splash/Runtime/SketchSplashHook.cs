using System.Collections.Generic;
using UnityEngine;

namespace Sketch.Sdk.Splash
{
    /// <summary>
    /// The only place a game customizes the SDK splash (the scene itself is SDK-owned and rebuilt on update).
    /// Default location: Assets/Resources/SketchSplashHook.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Sketch SDK/Splash Hook", fileName = RESOURCE_NAME)]
    public sealed class SketchSplashHook : ScriptableObject
    {
        public const string RESOURCE_NAME = "SketchSplashHook";
        public const string DEFAULT_ASSET_PATH = "Assets/Resources/SketchSplashHook.asset";

        [Header("Next scene")]
        [Tooltip("Scene name to load after the splash. Empty = use the build index below.")]
        [SerializeField] string _nextSceneName = "";
        [SerializeField] int _nextSceneIndex = 1;

        [Header("Timing (seconds, real time)")]
        [Tooltip("Logo shown alone before the loading bar appears.")]
        [SerializeField] float _brandSeconds = 0.5f;
        [Tooltip("The bar never completes faster than this.")]
        [SerializeField] float _minDuration = 5f; // Bounce-style: gives the cold-start App Open time to load
        [Tooltip("Hard cap: the splash leaves by this time whatever is still pending (an app-open ad already on screen is allowed to close).")]
        [SerializeField] float _maxDuration = 8f;
        [SerializeField] float _remoteConfigTimeout = 3f;
        [SerializeField] float _consentTimeout = 3f;

        [Header("SDK")]
        [Tooltip("Start the SDK here if SketchSdkSettings.AutoInitialize is off.")]
        [SerializeField] bool _initializeSdk = true;
        [Tooltip("Uses SketchAds.ShowColdStartAppOpen (it still respects the App Open On Cold Start setting and pacing).")]
        [SerializeField] bool _coldStartAppOpen = true;

        [Header("Look")]
        [SerializeField] Sprite _brandSprite;
        [SerializeField] Sprite _backgroundSprite;
        [SerializeField] Color _backgroundColor = new Color(0.08f, 0.09f, 0.14f, 1f);
        [SerializeField] string _title = "";
        [SerializeField] Color _titleColor = Color.white;
        [SerializeField] Color _barColor = new Color(0.36f, 0.82f, 0.33f, 1f);

        [Header("Preloads")]
        [Tooltip("ScriptableObjects or prefabs whose component implements ISketchSplashPreload.")]
        [SerializeField] List<Object> _preloads = new List<Object>();

        public string NextSceneName => _nextSceneName;
        public int NextSceneIndex => _nextSceneIndex;
        public float BrandSeconds => Mathf.Max(0f, _brandSeconds);
        public float MinDuration => Mathf.Max(0f, _minDuration);
        public float MaxDuration => Mathf.Max(MinDuration, _maxDuration);
        public float RemoteConfigTimeout => Mathf.Max(0f, _remoteConfigTimeout);
        public float ConsentTimeout => Mathf.Max(0f, _consentTimeout);
        public bool InitializeSdk => _initializeSdk;
        public bool ColdStartAppOpen => _coldStartAppOpen;
        public Sprite BrandSprite => _brandSprite;
        public Sprite BackgroundSprite => _backgroundSprite;
        public Color BackgroundColor => _backgroundColor;
        public string Title => _title;
        public Color TitleColor => _titleColor;
        public Color BarColor => _barColor;

        /// <summary>Appends every configured ISketchSplashPreload (SO directly, or a component on a listed prefab).</summary>
        public void CollectPreloads(List<ISketchSplashPreload> into)
        {
            for (int i = 0; i < _preloads.Count; i++)
            {
                Object entry = _preloads[i];
                if (entry is ISketchSplashPreload direct) into.Add(direct);
                else if (entry is GameObject go && go.TryGetComponent(out ISketchSplashPreload component)) into.Add(component);
                else if (entry != null) Debug.LogWarning("[SketchSplash] " + entry.name + " does not implement ISketchSplashPreload.", this);
            }
        }

        public static SketchSplashHook LoadOrDefault()
        {
            SketchSplashHook hook = Resources.Load<SketchSplashHook>(RESOURCE_NAME);
            return hook != null ? hook : CreateInstance<SketchSplashHook>();
        }
    }
}
