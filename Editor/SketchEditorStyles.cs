using UnityEditor;
using UnityEngine;

namespace Sketch.Sdk.Editor
{
    /// <summary>Shared IMGUI look of the Manager: header, foldout sections, status badges, field help, key table rows.</summary>
    public static class SketchEditorStyles
    {
        static GUIStyle _fold, _title, _pill, _header, _wrapMini, _help, _code, _primary, _badge, _section, _cell, _cellBold;
        static Texture2D _primaryTex, _primaryHoverTex;

        public static Color HeaderColor => EditorGUIUtility.isProSkin ? new Color(0.13f, 0.17f, 0.24f) : new Color(0.80f, 0.86f, 0.95f);
        static Color SectionColor => EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.035f) : new Color(0f, 0f, 0f, 0.035f);
        static Color LineColor => EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.08f) : new Color(0f, 0f, 0f, 0.12f);
        static Color HelpColor => EditorGUIUtility.isProSkin ? new Color(0.68f, 0.70f, 0.74f) : new Color(0.32f, 0.34f, 0.38f);

        public static GUIStyle Title => _title ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 17, fixedHeight = 26, alignment = TextAnchor.MiddleLeft };
        public static GUIStyle VersionPill => _pill ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleLeft, fixedHeight = 26, padding = new RectOffset(4, 4, 0, 0) };
        public static GUIStyle HeaderBar => _header ??= new GUIStyle { padding = new RectOffset(12, 12, 8, 8), margin = new RectOffset(0, 0, 0, 0) };
        public static GUIStyle WrapMini => _wrapMini ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, alignment = TextAnchor.MiddleLeft };
        static GUIStyle HelpStyle => _help ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, normal = { textColor = HelpColor }, padding = new RectOffset(0, 0, 0, 2) };
        static GUIStyle CodeStyle => _code ??= new GUIStyle(EditorStyles.textArea) { wordWrap = false, richText = false, font = EditorStyles.standardFont, padding = new RectOffset(8, 8, 6, 6) };
        static GUIStyle Cell => _cell ??= new GUIStyle(EditorStyles.label) { clipping = TextClipping.Clip };
        static GUIStyle CellBold => _cellBold ??= new GUIStyle(EditorStyles.boldLabel) { clipping = TextClipping.Clip };
        static GUIStyle FoldStyle => _fold ??= new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold, fontSize = 12 };
        static GUIStyle BadgeStyle => _badge ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        static GUIStyle SectionBox => _section ??= new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 6, 8), margin = new RectOffset(8, 8, 0, 8) };

        public static GUIStyle PrimaryButton
        {
            get
            {
                if (_primary == null || _primaryTex == null)
                {
                    _primaryTex = Solid(new Color(0.16f, 0.42f, 0.78f));
                    _primaryHoverTex = Solid(new Color(0.22f, 0.50f, 0.88f));
                    _primary = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fontSize = 12 };
                    _primary.normal.background = _primaryTex;
                    _primary.hover.background = _primaryHoverTex;
                    _primary.active.background = _primaryTex;
                    _primary.normal.textColor = _primary.hover.textColor = _primary.active.textColor = Color.white;
                }
                return _primary;
            }
        }

        static Texture2D Solid(Color color)
        {
            var tex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        public static Color StatusColor(SketchCheckStatus status)
        {
            switch (status)
            {
                case SketchCheckStatus.Ok: return new Color(0.20f, 0.62f, 0.34f);
                case SketchCheckStatus.Info: return new Color(0.35f, 0.47f, 0.62f);
                case SketchCheckStatus.Warning: return new Color(0.86f, 0.58f, 0.10f);
                default: return new Color(0.82f, 0.25f, 0.22f);
            }
        }

        public static string StatusText(SketchCheckStatus status)
        {
            switch (status)
            {
                case SketchCheckStatus.Ok: return "OK";
                case SketchCheckStatus.Info: return "INFO";
                case SketchCheckStatus.Warning: return "WARN";
                default: return "ERROR";
            }
        }

        /// <summary>Coloured pill with white bold text, vertically centred in a single-line row.</summary>
        public static void Badge(SketchCheckStatus status, string text = null, float width = 52)
        {
            Rect rect = GUILayoutUtility.GetRect(width, EditorGUIUtility.singleLineHeight, GUILayout.Width(width), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            rect.y += 1; rect.height -= 2;
            EditorGUI.DrawRect(rect, StatusColor(status));
            GUI.Label(rect, text ?? StatusText(status), BadgeStyle);
        }

        /// <summary>Foldout section with remembered open state. Call EndSection only when it returns true.</summary>
        public static bool Section(string key, string title)
        {
            string pref = "SketchSdk.Manager.Fold." + key;
            bool open = EditorPrefs.GetBool(pref, true);
            Rect box = EditorGUILayout.BeginVertical(SectionBox);
            EditorGUI.DrawRect(box, SectionColor);
            Rect header = GUILayoutUtility.GetRect(10, 22, GUILayout.ExpandWidth(true));
            bool next = EditorGUI.Foldout(header, open, title, true, FoldStyle);
            if (next != open) EditorPrefs.SetBool(pref, next);
            if (!next) { EditorGUILayout.EndVertical(); return false; }
            Rect line = GUILayoutUtility.GetRect(10, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(line, LineColor);
            GUILayout.Space(6);
            return true;
        }

        public static void EndSection() => EditorGUILayout.EndVertical();

        public static void Separator()
        {
            Rect line = GUILayoutUtility.GetRect(10, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(line, LineColor);
            GUILayout.Space(6);
        }

        /// <summary>Small grey help line aligned under the field value (after the label column).</summary>
        public static void FieldHelp(string help, string remoteKey = null)
        {
            if (string.IsNullOrEmpty(help) && string.IsNullOrEmpty(remoteKey)) return;
            string text = string.IsNullOrEmpty(remoteKey) ? help : help + "   Remote key: " + remoteKey;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUIUtility.labelWidth + 2);
            GUILayout.Label(text, HelpStyle);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(3);
        }

        public static void Help(string text)
        {
            GUILayout.Label(text, HelpStyle);
            GUILayout.Space(4);
        }

        public static void Code(string code)
        {
            float height = CodeStyle.CalcHeight(new GUIContent(code), EditorGUIUtility.currentViewWidth - 60);
            EditorGUILayout.SelectableLabel(code, CodeStyle, GUILayout.Height(height));
        }

        public static void InfoRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(EditorGUIUtility.labelWidth));
            EditorGUILayout.SelectableLabel(value ?? string.Empty, EditorStyles.label, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Badge + bold label + wrapped hint. The hint wraps inside the remaining width.</summary>
        public static void CheckRow(SketchCheckItem item)
        {
            EditorGUILayout.BeginHorizontal();
            Badge(item.Status);
            GUILayout.Space(8);
            GUILayout.Label(item.Label, CellBold, GUILayout.Width(Mathf.Min(200f, EditorGUIUtility.labelWidth)));
            GUILayout.Label(item.Hint ?? string.Empty, WrapMini);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
        }

        /// <summary>Read-only path field; clicking it pings the asset in the Project window.</summary>
        public static void PathLink(string label, string assetPath)
        {
            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            using (new EditorGUI.DisabledScope(true))
                EditorGUI.TextField(rect, new GUIContent(label, "Click to show the file in the Project window"), assetPath ?? string.Empty);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/")) return;
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (asset != null) { EditorUtility.FocusProjectWindow(); EditorGUIUtility.PingObject(asset); }
                e.Use();
            }
        }

        /// <summary>Draws scalars without their [Header] decorators (the Manager has its own sections); lists fall back to PropertyField.</summary>
        public static void PlainField(SerializedProperty p, GUIContent label)
        {
            SerializedPropertyType t = p.propertyType;
            bool scalar = !p.isArray || t == SerializedPropertyType.String;
            scalar &= t == SerializedPropertyType.Boolean || t == SerializedPropertyType.String || t == SerializedPropertyType.Float
                      || t == SerializedPropertyType.Integer || t == SerializedPropertyType.Enum;
            if (!scalar) { EditorGUILayout.PropertyField(p, label, true); return; }

            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            label = EditorGUI.BeginProperty(rect, label, p);
            EditorGUI.BeginChangeCheck();
            switch (t)
            {
                case SerializedPropertyType.Boolean: { bool v = EditorGUI.Toggle(rect, label, p.boolValue); if (EditorGUI.EndChangeCheck()) p.boolValue = v; break; }
                case SerializedPropertyType.String: { string v = EditorGUI.TextField(rect, label, p.stringValue); if (EditorGUI.EndChangeCheck()) p.stringValue = v; break; }
                case SerializedPropertyType.Float: { float v = EditorGUI.FloatField(rect, label, p.floatValue); if (EditorGUI.EndChangeCheck()) p.floatValue = v; break; }
                case SerializedPropertyType.Integer: { int v = EditorGUI.IntField(rect, label, p.intValue); if (EditorGUI.EndChangeCheck()) p.intValue = v; break; }
                default: { int v = EditorGUI.Popup(rect, label, p.enumValueIndex, EnumLabels(p)); if (EditorGUI.EndChangeCheck()) p.enumValueIndex = v; break; }
            }
            EditorGUI.EndProperty();
        }

        static GUIContent[] EnumLabels(SerializedProperty p)
        {
            string[] names = p.enumDisplayNames;
            var labels = new GUIContent[names.Length];
            for (int i = 0; i < names.Length; i++) labels[i] = new GUIContent(names[i]);
            return labels;
        }

        /// <summary>Compact read-only table of a catalog's products: id, type, store ids, fallback price, remove-ads.</summary>
        public static void ProductTable(SketchIapCatalog catalog)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Product id", CellBold, GUILayout.ExpandWidth(true));
            GUILayout.Label("Type", CellBold, GUILayout.Width(110));
            GUILayout.Label("Price", CellBold, GUILayout.Width(70));
            GUILayout.Label("No ads", CellBold, GUILayout.Width(52));
            EditorGUILayout.EndHorizontal();
            Separator();
            foreach (SketchIapProduct product in catalog.Products)
            {
                if (product == null) continue;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.SelectableLabel(product.Id ?? string.Empty, Cell, GUILayout.Height(EditorGUIUtility.singleLineHeight), GUILayout.ExpandWidth(true));
                GUILayout.Label(product.Type.ToString(), Cell, GUILayout.Width(110));
                GUILayout.Label(string.IsNullOrEmpty(product.FallbackPrice) ? "-" : product.FallbackPrice, Cell, GUILayout.Width(70));
                GUILayout.Label(product.RemovesAds ? "yes" : "", Cell, GUILayout.Width(52));
                EditorGUILayout.EndHorizontal();
            }
            GUILayout.Space(4);
        }

        // ---------------- remote key table ----------------

        const float TYPE_W = 52f, OWNER_W = 44f, COPY_W = 44f;

        static void Columns(float total, out float keyW, out float defW, out float ovrW)
        {
            float free = Mathf.Max(240f, total - TYPE_W - OWNER_W - COPY_W - 32f);
            keyW = free * 0.48f; defW = free * 0.24f; ovrW = free * 0.28f;
        }

        public static void KeyHeader()
        {
            Columns(EditorGUIUtility.currentViewWidth - 60f, out float keyW, out float defW, out float ovrW);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Key", CellBold, GUILayout.Width(keyW));
            GUILayout.Label("Type", CellBold, GUILayout.Width(TYPE_W));
            GUILayout.Label("Default", CellBold, GUILayout.Width(defW));
            GUILayout.Label("Override", CellBold, GUILayout.Width(ovrW));
            GUILayout.Label("From", CellBold, GUILayout.Width(OWNER_W));
            EditorGUILayout.EndHorizontal();
            Separator();
        }

        /// <summary>One key row (key, type, default, override field, owner, copy) plus its description. Returns the override text.</summary>
        public static string KeyRow(SketchRemoteKeyDef key, string current)
        {
            Columns(EditorGUIUtility.currentViewWidth - 60f, out float keyW, out float defW, out float ovrW);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(key.Key, key.Key), CellBold, GUILayout.Width(keyW));
            GUILayout.Label(key.Type.ToString(), Cell, GUILayout.Width(TYPE_W));
            GUILayout.Label(new GUIContent(key.DefaultValue, key.DefaultValue), Cell, GUILayout.Width(defW));
            string edited = EditorGUILayout.DelayedTextField(current, GUILayout.Width(ovrW));
            GUILayout.Label(key.IsSdkKey ? "SDK" : "Game", Cell, GUILayout.Width(OWNER_W));
            if (GUILayout.Button(new GUIContent("Copy", "Copy the key name"), EditorStyles.miniButton, GUILayout.Width(COPY_W))) EditorGUIUtility.systemCopyBuffer = key.Key;
            EditorGUILayout.EndHorizontal();
            GUILayout.Label(key.Description, HelpStyle);
            GUILayout.Space(3);
            return (edited ?? string.Empty).Trim();
        }
    }
}
