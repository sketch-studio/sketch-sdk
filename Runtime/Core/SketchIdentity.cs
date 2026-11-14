using System;
using System.Globalization;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>Install / session identity persisted in PlayerPrefs. Begin() runs once per launch from the bootstrap.</summary>
    public static class SketchIdentity
    {
        const string INSTALL_KEY = "sketchsdk.installation_id";
        const string FIRST_OPEN_KEY = "sketchsdk.first_open_unix";
        const string SESSION_KEY = "sketchsdk.session";

        static bool _begun;

        public static string InstallationId { get; private set; } = string.Empty;
        public static string SessionId { get; private set; } = string.Empty;
        /// <summary>1 on the first launch after install.</summary>
        public static int Session { get; private set; }
        public static double FirstOpenUnix { get; private set; }
        public static DateTime SessionStartedUtc { get; private set; }

        public static double NowUnix => (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;

        public static string Platform
        {
            get
            {
                switch (Application.platform)
                {
                    case RuntimePlatform.Android: return "Android";
                    case RuntimePlatform.IPhonePlayer: return "iOS";
                    default: return Application.isEditor ? "Editor" : Application.platform.ToString();
                }
            }
        }

        public static void Begin()
        {
            if (_begun) return;
            _begun = true;
            InstallationId = PlayerPrefs.GetString(INSTALL_KEY, string.Empty);
            if (string.IsNullOrEmpty(InstallationId))
            {
                InstallationId = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(INSTALL_KEY, InstallationId);
            }
            string first = PlayerPrefs.GetString(FIRST_OPEN_KEY, string.Empty);
            if (!double.TryParse(first, NumberStyles.Float, CultureInfo.InvariantCulture, out double firstOpen) || firstOpen <= 0)
            {
                firstOpen = NowUnix;
                PlayerPrefs.SetString(FIRST_OPEN_KEY, firstOpen.ToString("R", CultureInfo.InvariantCulture));
            }
            FirstOpenUnix = firstOpen;
            Session = PlayerPrefs.GetInt(SESSION_KEY, 0) + 1;
            PlayerPrefs.SetInt(SESSION_KEY, Session);
            NewSessionId();
            PlayerPrefs.Save();
        }

        /// <summary>Start a new analytics session id (also used after a long background).</summary>
        public static void NewSessionId()
        {
            SessionId = Guid.NewGuid().ToString("N");
            SessionStartedUtc = DateTime.UtcNow;
        }

        public static string IsoNow() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        public static string Iso(DateTime utc) => utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _begun = false;
            InstallationId = string.Empty;
            SessionId = string.Empty;
            Session = 0;
            FirstOpenUnix = 0;
        }
    }
}
