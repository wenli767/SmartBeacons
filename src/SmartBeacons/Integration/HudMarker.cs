using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartBeacons.Integration;

using Core;

internal sealed class HudMarker
{
    private const float Epsilon = .0001f;
    public readonly uGUI_Ping View;
    public readonly RectTransform Root, IconTransform;
    public readonly uGUI_Icon Icon;
    public readonly TextMeshProUGUI Name, Distance, Suffix;
    private readonly Graphic arrow;
    private readonly TransformOffset rootOffset, nameOffset, distanceOffset, suffixOffset;
    private readonly Vector3[] boundsCorners = new Vector3[4];
    private float nativeIcon, appliedIcon, nativeText, appliedText;
    private Vector3 nativeScale, appliedScale;
    private float nativeArrow, appliedArrow;
    private bool changedIcon, changedText, changedScale, changedArrow;
    private float iconTransition = 1f, scaleTransition = 1f, labelTransition = -1f, emphasis;

    public readonly string Id;
    public PingInstance Ping;
    public int LastSeen;
    public ViewPoint Point;
    public Vector2 Screen;
    public float Alignment, Meters;
    public Visual Visual;
    public Category Category;
    public bool Priority, Expanded, Collapsed;
    public int GroupCount;
    public ScreenPoint Spread, LabelOffset;
    public float CurrentIconAlpha { get; private set; }

    public HudMarker(string id, uGUI_Ping view, PingInstance ping)
    {
        Id = id;
        View = view;
        Ping = ping;
        Root = view.rectTransform;
        Icon = view.icon;
        IconTransform = Icon ? Icon.rectTransform : null;
        arrow = view.arrow;
        Name = view.infoText;
        Distance = view.distanceText;
        Suffix = view.suffixText;
        rootOffset = new TransformOffset(Root);
        nameOffset = new TransformOffset(Name ? Name.rectTransform : null);
        distanceOffset = new TransformOffset(Distance ? Distance.rectTransform : null);
        suffixOffset = new TransformOffset(Suffix ? Suffix.rectTransform : null);
    }

    public bool Active(HudBindings bindings)
    {
        if (!View || !Root || !Icon || !IconTransform || !View.isActiveAndEnabled ||
            !Ping || !Ping.isActiveAndEnabled || !Ping.Initialized || !Ping.visible || !Ping.origin)
            return false;
        return HasVisibleIcon(bindings) || HasVisibleGraphic(arrow, 1f);
    }

    public bool HasVisibleIcon(HudBindings bindings) => HasVisibleGraphic(Icon, bindings.IconColor(View).a);

    private static bool HasVisibleGraphic(Graphic graphic, float sourceAlpha) =>
        graphic && graphic.isActiveAndEnabled && !graphic.canvasRenderer.cull &&
        sourceAlpha > Epsilon && graphic.color.a > Epsilon &&
        graphic.canvasRenderer.GetAlpha() > Epsilon && graphic.canvasRenderer.GetInheritedAlpha() > Epsilon;

    public bool CanShowLabels => Point.OnScreen && Name && Name.isActiveAndEnabled &&
        Name.canvasRenderer.GetInheritedAlpha() > Epsilon;

    public void Restore(HudBindings bindings)
    {
        bool restoreArrow = changedArrow && arrow && Mathf.Abs(arrow.canvasRenderer.GetAlpha() - appliedArrow) < Epsilon;
        rootOffset.Restore();
        nameOffset.Restore();
        distanceOffset.Restore();
        suffixOffset.Restore();
        if (changedScale && IconTransform && IconTransform.localScale == appliedScale)
            IconTransform.localScale = nativeScale;
        if (View && changedIcon && Mathf.Abs(bindings.IconColor(View).a - appliedIcon) < Epsilon)
            View.SetIconAlpha(nativeIcon);
        if (View && changedText && Mathf.Abs(View.GetTextAlpha() - appliedText) < Epsilon)
            View.SetTextAlpha(nativeText);
        if (restoreArrow)
            arrow.canvasRenderer.SetAlpha(nativeArrow);
        changedScale = changedIcon = changedText = changedArrow = false;
    }

    public void ResetMotion()
    {
        iconTransition = scaleTransition = 1f;
        labelTransition = -1f;
        emphasis = 0f;
        Spread = LabelOffset = default;
    }

    public void Apply(HudBindings bindings, Camera uiCamera, Settings settings, float dt)
    {
        if (Visual.Mode is Presentation.Vanilla or Presentation.Unavailable)
        {
            ResetMotion();
            return;
        }

        bool reveal = Visual.Mode == Presentation.Reveal;
        bool full = reveal || Visual.Mode == Presentation.Emphasized;
        bool suppressed = Collapsed || Visual.Mode == Presentation.Hidden;
        nativeIcon = bindings.IconColor(View).a;
        nativeText = View.GetTextAlpha();
        nativeScale = IconTransform.localScale;
        bool hasArrow = arrow && arrow.isActiveAndEnabled;
        nativeArrow = hasArrow ? arrow.canvasRenderer.GetAlpha() : 0f;
        float speed = reveal || settings.TransitionSeconds <= 0f ? 2f : dt / settings.TransitionSeconds;
        float targetAlpha = suppressed ? 0f : Visual.Opacity;
        iconTransition = Visual.Mode == Presentation.Hidden ? 0f : Number.MoveTowards(iconTransition, targetAlpha, speed);
        float targetScale = reveal ? 1f : Visual.Scale;
        scaleTransition = Number.MoveTowards(scaleTransition, targetScale, speed);
        emphasis = Number.MoveTowards(emphasis, full && Point.OnScreen ? 1f : 0f, speed);
        appliedIcon = nativeIcon > Epsilon ? Mathf.Lerp(nativeIcon, 1f, emphasis) * iconTransition : 0f;
        changedIcon = Mathf.Abs(nativeIcon - appliedIcon) > Epsilon;
        CurrentIconAlpha = appliedIcon;
        if (changedIcon)
            View.SetIconAlpha(appliedIcon);

        if (hasArrow)
        {
            appliedArrow = nativeArrow * iconTransition;
            changedArrow = Mathf.Abs(nativeArrow - appliedArrow) > Epsilon;
            if (Mathf.Abs(arrow.canvasRenderer.GetAlpha() - appliedArrow) > Epsilon)
                arrow.canvasRenderer.SetAlpha(appliedArrow);
        }

        appliedScale = nativeScale * scaleTransition;
        changedScale = (nativeScale - appliedScale).sqrMagnitude > Epsilon * Epsilon;
        if (changedScale)
            IconTransform.localScale = appliedScale;

        float labelTarget = !CanShowLabels ? nativeText * targetAlpha : Visual.LabelOpacity;
        if (suppressed || LabelOffset.X == float.PositiveInfinity)
            labelTarget = 0f;
        if (labelTransition < 0f)
            labelTransition = nativeText;
        labelTransition = suppressed ? 0f : Number.MoveTowards(labelTransition, labelTarget, speed);
        appliedText = labelTransition;
        changedText = Mathf.Abs(nativeText - appliedText) > Epsilon;
        if (changedText)
            View.SetTextAlpha(appliedText);

        if (Spread.Valid)
            rootOffset.Apply(uiCamera, Spread);
        if (LabelOffset.Valid)
        {
            nameOffset.Apply(uiCamera, LabelOffset);
            distanceOffset.Apply(uiCamera, LabelOffset);
            suffixOffset.Apply(uiCamera, LabelOffset);
        }
    }

    public bool TryLabelRect(Camera uiCamera, out ScreenRect rect)
    {
        rect = default;
        bool hasRect = AddTextBounds(Name, uiCamera, ref rect, false);
        hasRect |= AddTextBounds(Distance, uiCamera, ref rect, hasRect);
        hasRect |= AddTextBounds(Suffix, uiCamera, ref rect, hasRect);
        return hasRect;
    }

    private bool AddTextBounds(TextMeshProUGUI text, Camera uiCamera, ref ScreenRect bounds, bool hasBounds)
    {
        if (!text || !text.isActiveAndEnabled || string.IsNullOrEmpty(text.text))
            return false;

        RectTransform transform = text.rectTransform;
        Rect rect = transform.rect;
        float width = Mathf.Min(rect.width, text.preferredWidth);
        float height = Mathf.Min(rect.height, text.preferredHeight);
        if (width <= 0f || height <= 0f)
            return false;

        int alignment = (int)text.alignment;
        float left = (alignment & 1) != 0 ? rect.xMin : (alignment & 4) != 0 ? rect.xMax - width : rect.center.x - width / 2f;
        float bottom = (alignment & 0x100) != 0 ? rect.yMax - height :
            (alignment & 0x400) != 0 ? rect.yMin : rect.center.y - height / 2f;
        boundsCorners[0] = transform.TransformPoint(new Vector3(left, bottom));
        boundsCorners[1] = transform.TransformPoint(new Vector3(left + width, bottom));
        boundsCorners[2] = transform.TransformPoint(new Vector3(left, bottom + height));
        boundsCorners[3] = transform.TransformPoint(new Vector3(left + width, bottom + height));
        Vector2 min = RectTransformUtility.WorldToScreenPoint(uiCamera, boundsCorners[0]);
        Vector2 max = min;
        for (int i = 1; i < boundsCorners.Length; i++)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(uiCamera, boundsCorners[i]);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        if (!Number.Finite(min.x) || !Number.Finite(min.y) || !Number.Finite(max.x) || !Number.Finite(max.y))
            return false;
        var next = new ScreenRect(min.x, min.y, max.x - min.x, max.y - min.y);
        bounds = hasBounds ? bounds.Union(next) : next;
        return true;
    }

    private sealed class TransformOffset
    {
        private readonly RectTransform target;
        private Vector2 original, applied;
        private bool changed;

        public TransformOffset(RectTransform target) => this.target = target;

        public void Apply(Camera camera, ScreenPoint offset)
        {
            if (!target || target.parent is not RectTransform parent || (offset.X == 0 && offset.Y == 0))
                return;
            Vector2 from = RectTransformUtility.WorldToScreenPoint(camera, target.position);
            Vector2 to = from + new Vector2(offset.X, offset.Y);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, from, camera, out Vector2 localFrom) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, to, camera, out Vector2 localTo))
                return;
            original = target.anchoredPosition;
            applied = original + localTo - localFrom;
            changed = true;
            target.anchoredPosition = applied;
        }

        public void Restore()
        {
            if (changed && target && target.anchoredPosition == applied)
                target.anchoredPosition = original;
            changed = false;
        }
    }
}
