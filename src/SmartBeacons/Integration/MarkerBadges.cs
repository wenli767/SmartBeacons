using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SmartBeacons.Integration;

internal sealed class MarkerBadges
{
    private readonly List<Badge> pool = new();
    private int used;

    public void Begin() => used = 0;

    public void Draw(HudMarker marker, int count, Color color)
    {
        TextMeshProUGUI template = marker.Name;
        if (!template || !template.font || !marker.IconTransform)
            return;

        Badge badge = used < pool.Count ? pool[used] : null;
        TextMeshProUGUI text = badge?.Text;
        if (!text)
        {
            var gameObject = new GameObject("Smart Beacons badge", typeof(RectTransform));
            // Parent before adding TMP so it inherits the game canvas.
            gameObject.transform.SetParent(marker.IconTransform, false);
            gameObject.layer = marker.IconTransform.gameObject.layer;
            text = gameObject.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.Left;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(1f, .5f);
            text.rectTransform.pivot = new Vector2(0f, .5f);
            text.rectTransform.sizeDelta = new Vector2(45f, 24f);
            badge = new Badge(text);
            if (used == pool.Count)
                pool.Add(badge);
            else
                pool[used] = badge;
        }
        used++;
        if (text.rectTransform.parent != marker.IconTransform)
            text.rectTransform.SetParent(marker.IconTransform, false);
        if (text.font != template.font)
        {
            badge.Count = -1;
            text.font = template.font;
            text.fontSharedMaterial = template.fontSharedMaterial;
        }
        text.fontSize = Mathf.Clamp(template.fontSize * .72f, 9f, 20f);
        text.rectTransform.anchoredPosition = new Vector2(3f, count > 0 ? -4f : 5f);
        if (badge.Count != count)
        {
            text.text = count > 0 ? count.ToString(System.Globalization.CultureInfo.InvariantCulture) :
                template.font.HasCharacter('★') ? "★" : "*";
            badge.Count = count;
        }
        color.a = count > 0 ? Mathf.Clamp01(marker.CurrentIconAlpha * 1.5f) : 1f;
        text.color = color;
        if (!text.gameObject.activeSelf)
            text.gameObject.SetActive(true);
    }

    public void End()
    {
        for (int i = used; i < pool.Count; i++)
        {
            TextMeshProUGUI text = pool[i].Text;
            if (text && text.gameObject.activeSelf)
                text.gameObject.SetActive(false);
        }
    }

    public void HideUnder(RectTransform parent)
    {
        foreach (Badge badge in pool)
        {
            if (badge.Text && badge.Text.transform.parent == parent)
                badge.Text.gameObject.SetActive(false);
        }
    }

    public void Clear()
    {
        foreach (Badge badge in pool)
        {
            TextMeshProUGUI text = badge.Text;
            if (text)
            {
                text.gameObject.SetActive(false);
                Object.Destroy(text.gameObject);
            }
        }
        pool.Clear();
        used = 0;
    }

    private sealed class Badge
    {
        public readonly TextMeshProUGUI Text;
        public int Count = -1;

        public Badge(TextMeshProUGUI text) => Text = text;
    }
}
