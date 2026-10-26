using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PotLeakage.UI
{
    /// <summary>World-space "Collected Tools" panel (VR port of the desktop panel). Same event API: AddCollected(key), RemoveCollected(key),
    /// ArmRow(key) / DisarmRow(key) and per-row click events (rowEvents), so task flows are wired in the Inspector. Rows are UGUI buttons
    /// pressed with the controller ray (the canvas has a tracked-device raycaster). The panel appears in front of the player when shown.</summary>
    public class CollectedToolsPanelController : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject panelRoot;
        public Transform listContainer;
        public TextMeshProUGUI headingText;
        public TextMeshProUGUI countText;
        public Sprite defaultIcon;

        [Serializable]
        public class ToolDefinition
        {
            [Tooltip("Key used by AddCollected / row lookups (usually the tool GameObject name).")]
            public string key;
            public string displayName;
            public Sprite icon;
        }

        [Header("Event-driven setup (wire AddCollected(key) from Events To Follow)")]
        public List<ToolDefinition> toolDefinitions = new List<ToolDefinition>();
        [Tooltip("Total shown in the counter badge. 0 = 3.")]
        public int totalTools = 0;

        [Header("Placement (world space, in front of the player when shown)")]
        public bool placeInFrontOnShow = true;
        public float distance = 1.5f, sideOffset = 0.6f, heightOffset = -0.05f, worldScale = 0.0012f;

        readonly List<string> collectedToolKeys = new List<string>();
        readonly Dictionary<string, GameObject> toolRows = new Dictionary<string, GameObject>();

        public event Action<string> OnToolRowClicked;

        static readonly Color RowNormalColor = new Color(0.11f, 0.19f, 0.34f, 1f);
        static readonly Color RowHighlightColor = new Color(0.20f, 0.50f, 0.45f, 1f);
        static readonly Color StatusGreen = new Color(0.20f, 0.90f, 0.35f, 1f);
        const float RowHeight = 70f;

        public int CollectedCount { get { return collectedToolKeys.Count; } }

        [Serializable]
        public class RowEvent
        {
            [Tooltip("Matches a clicked row whose key contains this text (case-insensitive).")]
            public string keyContains;
            [Tooltip("Armed by ArmRow(keyContains); disarmed again after the first click.")]
            public bool armed;
            public UnityEngine.Events.UnityEvent onClicked = new UnityEngine.Events.UnityEvent();
        }

        [Header("Row click events (arm with ArmRow from Events To Follow)")]
        public List<RowEvent> rowEvents = new List<RowEvent>();

        void Awake()
        {
            if (panelRoot == null) panelRoot = gameObject;
            UpdateCounterDisplay();
        }

        // ---------------------------------------------------------------- visibility
        public void ShowPanel()
        {
            gameObject.SetActive(true);
            SetVisible(true);
            if (placeInFrontOnShow) { PlaceInFront(); StopAllCoroutines(); StartCoroutine(PlaceAgainAfterFade()); }
        }

        // the comfort fade moves the player a moment after the event that showed the panel, so place it once more afterwards
        System.Collections.IEnumerator PlaceAgainAfterFade()
        {
            yield return new WaitForSeconds(0.4f);
            PlaceInFront();
        }

        public void HidePanel() { SetVisible(false); }

        public void SetVisible(bool visible)
        {
            if (visible && !gameObject.activeSelf) gameObject.SetActive(true);
            if (panelRoot != null && panelRoot != gameObject) panelRoot.SetActive(visible);
            else if (!visible) gameObject.SetActive(false);
        }

        void PlaceInFront()
        {
            var cam = UnityEngine.Camera.main ? UnityEngine.Camera.main.transform : null;
            if (!cam) return;
            var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.001f) fwd = cam.forward;
            fwd.Normalize();
            var right = Vector3.Cross(Vector3.up, fwd);
            transform.position = cam.position + fwd * distance + right * sideOffset + Vector3.up * heightOffset;
            transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            transform.localScale = Vector3.one * worldScale;
            var canvas = GetComponent<Canvas>();
            if (canvas) canvas.worldCamera = UnityEngine.Camera.main;
        }

        // ---------------------------------------------------------------- collection
        public bool HasCollected(string toolKey) { return collectedToolKeys.Contains(toolKey); }

        public void UpdateCounterDisplay()
        {
            int total = totalTools > 0 ? totalTools : 3;
            if (headingText != null) headingText.text = LanguageService.Localize("COLLECTED TOOLS");
            if (countText != null) countText.text = CollectedCount + "/" + total;
        }

        public void ClearPanel()
        {
            collectedToolKeys.Clear();
            toolRows.Clear();
            if (listContainer != null)
                for (int i = listContainer.childCount - 1; i >= 0; i--)
                {
                    var child = listContainer.GetChild(i).gameObject;
                    if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
                }
            UpdateCounterDisplay();
        }

        /// <summary>Adds the tool registered under this key in toolDefinitions (single-argument, event-friendly).</summary>
        public void AddCollected(string toolKey)
        {
            var def = toolDefinitions.Find(d => d != null && string.Equals(d.key, toolKey, StringComparison.OrdinalIgnoreCase));
            if (def == null) { Debug.LogWarning("[CollectedToolsPanel] AddCollected: no tool definition for '" + toolKey + "'."); return; }
            AddCollectedTool(def.key, string.IsNullOrEmpty(def.displayName) ? def.key : def.displayName, def.icon);
        }

        public void AddCollectedTool(string toolKey, string displayName, Sprite thumbnail = null)
        {
            if (HasCollected(toolKey)) return;
            collectedToolKeys.Add(toolKey);
            UpdateCounterDisplay();
            if (listContainer == null) return;

            var row = CreateRow(displayName, thumbnail != null ? thumbnail : defaultIcon);
            row.name = "Row_" + toolKey;
            toolRows[toolKey] = row;
            var btn = row.GetComponent<Button>();
            string captured = toolKey;
            btn.onClick.AddListener(() => OnRowClicked(captured));
        }

        /// <summary>Event-friendly (void) removal.</summary>
        public void RemoveCollected(string toolKey) { RemoveCollectedTool(toolKey); }

        public bool RemoveCollectedTool(string toolKey)
        {
            string key = FindKey(toolKey);
            if (key == null) return false;
            collectedToolKeys.Remove(key);
            GameObject row;
            if (toolRows.TryGetValue(key, out row))
            {
                toolRows.Remove(key);
                if (row != null) { if (Application.isPlaying) Destroy(row); else DestroyImmediate(row); }
            }
            UpdateCounterDisplay();
            return true;
        }

        string FindKey(string needle)
        {
            foreach (var k in collectedToolKeys) if (string.Equals(k, needle, StringComparison.OrdinalIgnoreCase)) return k;
            foreach (var k in collectedToolKeys) if (KeyMatches(needle, k)) return k;
            return null;
        }

        static bool KeyMatches(string needle, string key)
        {
            if (string.IsNullOrEmpty(needle) || string.IsNullOrEmpty(key)) return false;
            return key.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 || needle.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ---------------------------------------------------------------- rows
        public GameObject GetRow(string toolKey)
        {
            GameObject row;
            if (toolRows.TryGetValue(toolKey, out row) && row != null) return row;
            foreach (var kv in toolRows) if (kv.Value != null && KeyMatches(toolKey, kv.Key)) return kv.Value;
            return null;
        }

        public void OnRowClicked(string toolKey)
        {
            if (OnToolRowClicked != null) OnToolRowClicked(toolKey);
            for (int i = 0; i < rowEvents.Count; i++)
            {
                var re = rowEvents[i];
                if (re == null || !re.armed || !KeyMatches(re.keyContains, toolKey)) continue;
                re.armed = false;
                re.onClicked.Invoke();
            }
        }

        /// <summary>Highlights the row, makes it clickable and arms every rowEvents entry registered for this key.</summary>
        public void ArmRow(string keyContains)
        {
            HighlightRow(keyContains, true);
            SetRowInteractable(keyContains, true);
            foreach (var re in rowEvents) if (re != null && string.Equals(re.keyContains, keyContains, StringComparison.OrdinalIgnoreCase)) re.armed = true;
        }

        public void DisarmRow(string keyContains)
        {
            HighlightRow(keyContains, false);
            SetRowInteractable(keyContains, false);
            foreach (var re in rowEvents) if (re != null && string.Equals(re.keyContains, keyContains, StringComparison.OrdinalIgnoreCase)) re.armed = false;
        }

        public void HighlightRow(string toolKey, bool highlight)
        {
            var row = GetRow(toolKey);
            if (row == null) return;
            var img = row.GetComponent<Image>();
            if (img != null) img.color = highlight ? RowHighlightColor : RowNormalColor;
        }

        public void SetRowInteractable(string toolKey, bool interactable)
        {
            var row = GetRow(toolKey);
            if (row == null) return;
            var btn = row.GetComponent<Button>();
            if (btn != null) btn.interactable = interactable;
        }

        GameObject CreateRow(string displayName, Sprite icon)
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(listContainer, false);
            row.GetComponent<LayoutElement>().preferredHeight = RowHeight;
            var rowImg = row.GetComponent<Image>(); rowImg.color = RowNormalColor; rowImg.raycastTarget = true;
            var btn = row.GetComponent<Button>(); btn.targetGraphic = rowImg; btn.interactable = false;

            var box = new GameObject("IconBox", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(row.transform, false);
            var br = (RectTransform)box.transform;
            br.anchorMin = br.anchorMax = new Vector2(0f, 0.5f); br.pivot = new Vector2(0f, 0.5f);
            br.anchoredPosition = new Vector2(10f, 0f); br.sizeDelta = new Vector2(52f, 52f);
            var bi = box.GetComponent<Image>(); bi.color = Color.white; bi.raycastTarget = false;
            if (icon != null)
            {
                var ig = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                ig.transform.SetParent(box.transform, false);
                var ir = (RectTransform)ig.transform; ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one; ir.offsetMin = new Vector2(3, 3); ir.offsetMax = new Vector2(-3, -3);
                var ii = ig.GetComponent<Image>(); ii.sprite = icon; ii.preserveAspect = true; ii.raycastTarget = false;
            }

            AddText(row.transform, "ToolName", LanguageService.Localize(displayName), 20f, FontStyles.Bold, Color.white, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(74, 0), new Vector2(-10, -6), TextAlignmentOptions.BottomLeft);
            AddText(row.transform, "StatusLabel", LanguageService.Localize("[COLLECTED]"), 15f, FontStyles.Bold, StatusGreen, new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(74, 6), new Vector2(-10, 0), TextAlignmentOptions.TopLeft);
            return row;
        }

        static void AddText(Transform parent, string name, string text, float size, FontStyles style, Color color,
            Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, TextAlignmentOptions align)
        {
            var g = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            g.transform.SetParent(parent, false);
            var r = (RectTransform)g.transform; r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = oMin; r.offsetMax = oMax;
            var t = g.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color; t.alignment = align;
            t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}
