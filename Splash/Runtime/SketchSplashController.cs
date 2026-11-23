using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sketch.Sdk.Splash
{
    /// <summary>
    /// SDK splash (build index 0): brand -> SDK start -> loading bar -> next scene.
    /// Leaves when consent, remote config, preloads, ready checks and the next scene are ready (each with a timeout)
    /// and the minimum duration passed, or at the hard cap. Any exception jumps straight to the next scene.
    /// Customize through SketchSplashHook only.
    /// </summary>
    public sealed class SketchSplashController : MonoBehaviour
    {
        enum Phase { Waiting, AppOpen, Activating, Done }

        const float BAR_FADE_SECONDS = 0.25f;
        const float BAR_FILL_PER_SECOND = 1.6f;
        // An app-open ad already on screen may outlive the cap; this only guards a provider that never reports "closed".
        const float APP_OPEN_SAFETY_SECONDS = 45f;
        const int GATE_COUNT = 5;

        [SerializeField] SketchSplashHook _hook;
        [SerializeField] Image _background;
        [SerializeField] AspectRatioFitter _backgroundFitter;
        [SerializeField] Image _brand;
        [SerializeField] Text _title;
        [SerializeField] CanvasGroup _barGroup;
        [SerializeField] Image _barTrack;
        [SerializeField] RectTransform _barLabelSlot;
        [SerializeField] Image _barFill;
        [SerializeField] RectTransform _barFillRect;

        readonly List<ISketchSplashPreload> _preloads = new List<ISketchSplashPreload>();
        Action _onAppOpenDone;
        AsyncOperation _sceneLoad;
        Phase _phase;
        float _startTime;
        float _appOpenStartTime;
        float _shownFill;
        int _nextPreload;
        int _preloadsRunning;
        bool _appOpenDone;
        bool _started;

        float Elapsed => Time.realtimeSinceStartup - _startTime;

        void Awake()
        {
            _startTime = Time.realtimeSinceStartup;
            _onAppOpenDone = OnAppOpenDone;
            if (_hook == null) _hook = SketchSplashHook.LoadOrDefault();
            try { ApplyLook(); }
            catch (Exception e) { Debug.LogException(e, this); }
        }

        void Start()
        {
            try
            {
                if (_hook.InitializeSdk && !SketchSdkApp.IsStarted) SketchSdkApp.Initialize();
                _hook.CollectPreloads(_preloads);
                IReadOnlyList<ISketchSplashPreload> registered = SketchSplashRegistry.RegisteredPreloads;
                for (int i = 0; i < registered.Count; i++) _preloads.Add(registered[i]);
                _started = true;
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                Activate();
            }
        }

        void Update()
        {
            if (!_started || _phase == Phase.Done) return;
            try { Tick(); }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                Activate();
            }
        }

        void Tick()
        {
            float elapsed = Elapsed;
            // A transparent bar colour means the game hides the bar (e.g. its own loading screen follows): hide the track too.
            if (_barGroup != null) _barGroup.alpha = _hook.BarColor.a <= 0f ? 0f : Mathf.Clamp01((elapsed - _hook.BrandSeconds) / BAR_FADE_SECONDS);
            StartNextPreload();

            float target = 1f;
            switch (_phase)
            {
                case Phase.Waiting:
                    // Async ops queue behind a scene load held at 90%, so the next scene starts loading after the preloads.
                    if (_sceneLoad == null && AllPreloadsDone()) StartSceneLoad();
                    float readiness = Readiness(elapsed);
                    bool capped = elapsed >= _hook.MaxDuration;
                    target = capped ? 1f : Mathf.Min(readiness, _hook.MinDuration > 0f ? elapsed / _hook.MinDuration : 1f);
                    if (capped || (readiness >= 1f && elapsed >= _hook.MinDuration)) BeginAppOpen(elapsed);
                    break;
                case Phase.AppOpen:
                    if (_appOpenDone || Time.realtimeSinceStartup - _appOpenStartTime > APP_OPEN_SAFETY_SECONDS) Activate();
                    break;
                case Phase.Activating:
                    if (_sceneLoad == null || _sceneLoad.isDone) _phase = Phase.Done;
                    break;
            }
            _shownFill = Mathf.MoveTowards(_shownFill, target, BAR_FILL_PER_SECOND * Time.unscaledDeltaTime);
            SetFill(_shownFill);
        }

        float Readiness(float elapsed)
        {
            float ready = 0f;
            if (SketchConsent.IsGathered || elapsed >= _hook.ConsentTimeout) ready += 1f;
            if (SketchRemoteConfig.IsReady || elapsed >= _hook.RemoteConfigTimeout) ready += 1f;
            if (AllPreloadsDone()) ready += 1f;
            if (ReadyChecksPass()) ready += 1f;
            if (_sceneLoad != null) ready += Mathf.Clamp01(_sceneLoad.progress / 0.9f);
            return ready / GATE_COUNT;
        }

        bool AllPreloadsDone() => _nextPreload >= _preloads.Count && _preloadsRunning == 0;

        static bool ReadyChecksPass()
        {
            IReadOnlyList<Func<bool>> checks = SketchSplashRegistry.RegisteredReadyChecks;
            for (int i = 0; i < checks.Count; i++)
            {
                bool ready;
                try { ready = checks[i](); }
                catch (Exception e) { Debug.LogException(e); ready = true; }
                if (!ready) return false;
            }
            return true;
        }

        // One preload starts per frame so their first steps never stack into one long frame.
        void StartNextPreload()
        {
            if (_nextPreload >= _preloads.Count) return;
            ISketchSplashPreload preload = _preloads[_nextPreload++];
            _preloadsRunning++;
            StartCoroutine(RunPreload(preload));
        }

        IEnumerator RunPreload(ISketchSplashPreload preload)
        {
            IEnumerator routine = null;
            try { routine = preload.Preload(); }
            catch (Exception e) { Debug.LogException(e, this); }
            while (routine != null)
            {
                object current;
                try
                {
                    if (!routine.MoveNext()) break;
                    current = routine.Current;
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                    break;
                }
                yield return current;
            }
            _preloadsRunning--;
        }

        void StartSceneLoad()
        {
            if (_sceneLoad != null) return;
            string sceneName = _hook.NextSceneName;
            if (!string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName))
                _sceneLoad = SceneManager.LoadSceneAsync(sceneName);
            else
            {
                if (!string.IsNullOrEmpty(sceneName)) Debug.LogError("[SketchSplash] Scene '" + sceneName + "' is not in Build Settings; using index " + _hook.NextSceneIndex + ".", this);
                int index = _hook.NextSceneIndex;
                if (index > 0 && index < SceneManager.sceneCountInBuildSettings) _sceneLoad = SceneManager.LoadSceneAsync(index);
                else Debug.LogError("[SketchSplash] Next scene index " + index + " is not in Build Settings (SketchSplashHook).", this);
            }
            if (_sceneLoad != null) _sceneLoad.allowSceneActivation = false;
        }

        void BeginAppOpen(float elapsed)
        {
            _phase = Phase.AppOpen;
            _appOpenStartTime = Time.realtimeSinceStartup;
            if (!_hook.ColdStartAppOpen) { _appOpenDone = true; return; }
            try { SketchAds.ShowColdStartAppOpen(Mathf.Max(0f, _hook.MaxDuration - elapsed), _onAppOpenDone); }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                _appOpenDone = true;
            }
        }

        void OnAppOpenDone() => _appOpenDone = true;

        void Activate()
        {
            if (_phase == Phase.Activating || _phase == Phase.Done) return;
            _phase = Phase.Activating;
            SetFill(1f);
            try
            {
                if (_sceneLoad == null) StartSceneLoad();
                if (_sceneLoad != null) _sceneLoad.allowSceneActivation = true;
                else _phase = Phase.Done;
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                _phase = Phase.Done;
            }
        }

        void SetFill(float value)
        {
            if (_barFill != null && _barFill.type == Image.Type.Filled) _barFill.fillAmount = value;
            else if (_barFillRect != null)
            {
                Vector2 max = _barFillRect.anchorMax;
                if (!Mathf.Approximately(max.x, value)) _barFillRect.anchorMax = new Vector2(value, max.y);
            }
        }

        void ApplyLook()
        {
            if (_background != null)
            {
                Sprite bg = _hook.BackgroundSprite;
                _background.sprite = bg;
                _background.color = bg != null ? Color.white : _hook.BackgroundColor;
                bool envelope = _hook.BackgroundEnvelope && bg != null && bg.rect.height > 0f && _backgroundFitter != null;
                if (_backgroundFitter != null) _backgroundFitter.enabled = envelope;
                if (envelope)
                {
                    _backgroundFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                    _backgroundFitter.aspectRatio = bg.rect.width / bg.rect.height;
                }
                else Stretch(_background.rectTransform, 0f);
            }
            if (_brand != null)
            {
                _brand.sprite = _hook.BrandSprite;
                _brand.preserveAspect = true;
                _brand.enabled = _hook.BrandSprite != null;
                Place(_brand.rectTransform, _hook.BrandAnchorY, _hook.BrandSize);
            }
            ApplyBarLook();
            if (_title != null)
            {
                _title.text = _hook.Title;
                _title.color = _hook.TitleColor;
                _title.gameObject.SetActive(!string.IsNullOrEmpty(_hook.Title));
            }
            if (_barFill != null) _barFill.color = _hook.BarColor;
            if (_barGroup != null) _barGroup.alpha = 0f;
            SetFill(0f);
        }

        void ApplyBarLook()
        {
            if (_barGroup == null) return;
            var barRect = (RectTransform)_barGroup.transform;
            Place(barRect, _hook.BarAnchorY, _hook.BarSize);
            if (_barTrack != null)
            {
                if (_hook.BarBackgroundSprite != null) _barTrack.sprite = _hook.BarBackgroundSprite;
                _barTrack.type = HasBorder(_barTrack.sprite) ? Image.Type.Sliced : Image.Type.Simple;
                _barTrack.color = _hook.BarBackgroundColor;
            }
            if (_barFill != null)
            {
                if (_hook.BarFillSprite != null) _barFill.sprite = _hook.BarFillSprite;
                if (_barFill is SketchSlicedFillImage)
                {
                    // Full-width rect, fillAmount trims it: the 9-slice caps stay round at low values.
                    _barFill.type = Image.Type.Filled;
                    _barFill.fillMethod = Image.FillMethod.Horizontal;
                    _barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                    Stretch(_barFill.rectTransform, _hook.BarFillInset);
                }
                else if (_barFillRect != null)
                {
                    float inset = _hook.BarFillInset;
                    _barFillRect.offsetMin = new Vector2(inset, inset);
                    _barFillRect.offsetMax = new Vector2(-inset, -inset);
                }
            }
            GameObject labelPrefab = _hook.BarLabelPrefab;
            if (labelPrefab != null)
            {
                // The slot hangs under the bar (inside the bar group, so it fades with it) and owns the label's padding.
                RectTransform slot = _barLabelSlot != null ? _barLabelSlot : barRect;
                if (_barLabelSlot != null)
                {
                    slot.anchorMin = Vector2.zero;
                    slot.anchorMax = new Vector2(1f, 0f);
                    slot.pivot = new Vector2(0.5f, 1f);
                    slot.anchoredPosition = new Vector2(0f, -_hook.BarLabelGap);
                    slot.sizeDelta = new Vector2(0f, _hook.BarLabelHeight);
                }
                // One-off at splash start (SDK-owned scene); the game authors the label look in its own prefab.
                GameObject label = Instantiate(labelPrefab, slot, false);
                label.name = labelPrefab.name;
                if (label.transform is RectTransform labelRect)
                {
                    labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
                    labelRect.anchoredPosition = Vector2.zero;
                }
            }
        }

        static bool HasBorder(Sprite sprite) => sprite != null && sprite.border != Vector4.zero;

        static void Place(RectTransform rect, float anchorY, Vector2 size)
        {
            var anchor = new Vector2(0.5f, anchorY);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
