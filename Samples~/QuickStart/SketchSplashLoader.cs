using System.Collections;
using Sketch.Sdk;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sketch.Sdk.Samples
{
    /// <summary>
    /// Splash scene (build index 0): waits for consent + ads + remote config (with a cap), shows the cold-start
    /// app-open ad when enabled, then loads the game scene. Put it on one object in the splash scene.
    /// </summary>
    public sealed class SketchSplashLoader : MonoBehaviour
    {
        [SerializeField] string _nextScene = "";
        [SerializeField] int _nextSceneIndex = 1;
        [SerializeField] float _minSplashSeconds = 1.5f;
        [SerializeField] float _maxWaitSeconds = 8f;
        [SerializeField] float _appOpenWaitSeconds = 4f;

        IEnumerator Start()
        {
            float start = Time.realtimeSinceStartup;
            yield return new WaitUntil(() =>
                (SketchSdkApp.IsReady && SketchRemoteConfig.IsReady) || Time.realtimeSinceStartup - start > _maxWaitSeconds);

            bool appOpenDone = false;
            SketchAds.ShowColdStartAppOpen(_appOpenWaitSeconds, () => appOpenDone = true);
            yield return new WaitUntil(() => appOpenDone);

            float left = _minSplashSeconds - (Time.realtimeSinceStartup - start);
            if (left > 0) yield return new WaitForSecondsRealtime(left);

            if (!string.IsNullOrEmpty(_nextScene)) SceneManager.LoadSceneAsync(_nextScene);
            else SceneManager.LoadSceneAsync(_nextSceneIndex);
        }
    }
}
