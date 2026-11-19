using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Represents a single slot inside the Dock.
/// MatchItem is parented to the MatchItemRoot transform and smoothly tweens to its local position.
/// </summary>
public class DockSlot : MonoBehaviour
{
    [Header("Slot Identity & References")]
    [SerializeField] private int slotIndex;

    [Tooltip("Child transform to parent the MatchItem to. If empty, uses this transform.")]
    [SerializeField] private Transform matchItemRoot;

    [Header("Flight Animation Details")]
    [Tooltip("Target scale for the card when seated in the dock.")]
    [SerializeField] private Vector3 targetScale = new Vector3(0.75f, 0.75f, 0.75f);

    [Tooltip("Scale multiplier reached at the peak of the jump arc for a juicy pop.")]
    [SerializeField] private float flightPeakScaleMultiplier = 1.22f;

    [Header("Visuals")]
    [Tooltip("Slot background Image used for last-slot danger warning pulse. Auto-located if empty.")]
    [SerializeField] private Image slotImage;

    private MatchItem currentItem;
    private Color defaultImageColor = Color.white;
    private Vector3 defaultImageScale = Vector3.one;
    private Material defaultImageMaterial;
    private Material warningMaterialInstance;
    private bool hasCachedVisuals;
    private bool isWarningActive;
    private Tween warningTween;

    private static readonly int WarningIntensityProp = Shader.PropertyToID("_WarningIntensity");
    private static readonly int WarningColorProp = Shader.PropertyToID("_WarningColor");

    public int SlotIndex
    {
        get => slotIndex;
        set => slotIndex = value;
    }

    public Transform MatchItemRoot => matchItemRoot != null ? matchItemRoot : transform;
    public MatchItem CurrentItem => currentItem;
    public bool IsEmpty => currentItem == null;

    /// <summary>
    /// Parents the incoming item to MatchItemRoot and animates placement with a juicy arc,
    /// mid-air scale pop, and landing bounce.
    /// </summary>
    public void PlaceItemWithJump(MatchItem item, float duration, float jumpPower, Ease ease, Action onComplete = null)
    {
        currentItem = item;

        // Parent card to the slot's MatchItemRoot reference
        Transform targetParent = MatchItemRoot;
        item.transform.SetParent(targetParent, true);
        item.transform.DOKill();

        float riseDuration = duration * 0.42f;
        float fallDuration = duration - riseDuration;
        Vector3 finalScale = GetTargetScale(item);
        Vector3 peakScale = finalScale * flightPeakScaleMultiplier;

        Sequence seq = DOTween.Sequence();

        // 1. Jump to local center along arc
        seq.Join(item.transform.DOLocalJump(Vector3.zero, jumpPower, 1, duration).SetEase(ease));

        // 2. Align rotation with subtle snappy overshoot
        seq.Join(item.transform.DOLocalRotate(Vector3.zero, duration).SetEase(Ease.OutBack, 1.15f));

        // 3. Mid-air scale pop (swell up on lift, snap to finalScale on landing)
        Sequence scaleSeq = DOTween.Sequence();
        scaleSeq.Append(item.transform.DOScale(peakScale, riseDuration).SetEase(Ease.OutQuad));
        scaleSeq.Append(item.transform.DOScale(finalScale, fallDuration).SetEase(Ease.InQuad));
        seq.Join(scaleSeq);

        bool callbackInvoked = false;

        seq.OnComplete(() =>
        {
            if (item != null)
            {
                bool willRevealMystery = item.IsMystery && !item.IsRevealed;
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.identity;
                item.transform.localScale = finalScale;
                item.SetState(MatchItem.ItemState.InDock);

                if (!willRevealMystery)
                {
                    Vector3 squashScale = new Vector3(finalScale.x * 1.12f, finalScale.y * 0.86f, finalScale.z * 1.12f);
                    Sequence settleSeq = DOTween.Sequence();
                    settleSeq.Append(item.transform.DOScale(squashScale, 0.06f).SetEase(Ease.OutQuad));
                    settleSeq.Append(item.transform.DOScale(finalScale, 0.1f).SetEase(Ease.OutBack));
                }
            }

            if (!callbackInvoked)
            {
                callbackInvoked = true;
                onComplete?.Invoke();
            }
        });

        seq.OnKill(() =>
        {
            if (!callbackInvoked)
            {
                callbackInvoked = true;
                onComplete?.Invoke();
            }
        });
    }

    /// <summary>
    /// Smoothly slides an existing card into this new slot with a snappy spring ease and subtle tilt when items shift or gaps close.
    /// </summary>
    public void SlideItemToHere(MatchItem item, float duration, Ease ease = Ease.OutBack, Action onComplete = null)
    {
        currentItem = item;

        Transform targetParent = MatchItemRoot;
        float moveDir = Mathf.Sign(targetParent.position.x - item.transform.position.x);
        float tiltAngle = Mathf.Abs(targetParent.position.x - item.transform.position.x) > 0.05f ? -6f * moveDir : 0f;

        item.transform.SetParent(targetParent, true);
        item.transform.DOKill();

        Vector3 finalScale = GetTargetScale(item);

        Sequence seq = DOTween.Sequence();
        seq.Join(item.transform.DOLocalMove(Vector3.zero, duration).SetEase(Ease.OutBack, 1.1f));

        Sequence rotSeq = DOTween.Sequence();
        rotSeq.Append(item.transform.DOLocalRotate(new Vector3(0f, 0f, tiltAngle), duration * 0.4f).SetEase(Ease.OutQuad));
        rotSeq.Append(item.transform.DOLocalRotate(Vector3.zero, duration * 0.6f).SetEase(Ease.OutBack));
        seq.Join(rotSeq);

        seq.Join(item.transform.DOScale(finalScale, duration).SetEase(Ease.OutQuad));

        bool callbackInvoked = false;

        seq.OnComplete(() =>
        {
            if (item != null)
            {
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.identity;
                item.transform.localScale = finalScale;
                if (item.CurrentState != MatchItem.ItemState.InDock)
                {
                    item.SetState(MatchItem.ItemState.InDock);
                }
            }

            if (!callbackInvoked)
            {
                callbackInvoked = true;
                onComplete?.Invoke();
            }
        });

        seq.OnKill(() =>
        {
            if (!callbackInvoked)
            {
                callbackInvoked = true;
                onComplete?.Invoke();
            }
        });
    }

    private Vector3 GetTargetScale(MatchItem item)
    {
        return item != null ? targetScale * item.DockScaleMultiplier : targetScale;
    }

    /// <summary>
    /// Clears and releases the matched or removed card from this slot.
    /// </summary>
    public void Clear()
    {
        currentItem = null;
    }

    public void SetWarningPulse(bool active)
    {
        if (isWarningActive == active) return;

        EnsureVisualCached();
        if (slotImage == null) return;

        isWarningActive = active;
        warningTween?.Kill();

        if (active)
        {
            slotImage.color = defaultImageColor;
            slotImage.rectTransform.localScale = defaultImageScale;

            if (EnsureWarningMaterial())
            {
                slotImage.material = warningMaterialInstance;
                warningMaterialInstance.SetColor(WarningColorProp, new Color(0.88f, 0.04f, 0.08f, 1f));
                warningMaterialInstance.SetFloat(WarningIntensityProp, 0f);

                Sequence s = DOTween.Sequence();
                s.Join(DOTween.To(
                    () => warningMaterialInstance != null ? warningMaterialInstance.GetFloat(WarningIntensityProp) : 0f,
                    v => { if (warningMaterialInstance != null) warningMaterialInstance.SetFloat(WarningIntensityProp, v); },
                    0.95f,
                    0.45f
                ).SetEase(Ease.InOutSine));
                s.Join(slotImage.rectTransform.DOScale(defaultImageScale * 1.07f, 0.45f).SetEase(Ease.InOutSine));
                s.SetLoops(-1, LoopType.Yoyo);
                warningTween = s;
            }
            else
            {
                Color dangerColor = new Color(1f, 0.28f, 0.28f, Mathf.Max(defaultImageColor.a, 0.85f));
                Sequence s = DOTween.Sequence();
                s.Join(slotImage.DOColor(dangerColor, 0.45f).SetEase(Ease.InOutSine));
                s.Join(slotImage.rectTransform.DOScale(defaultImageScale * 1.07f, 0.45f).SetEase(Ease.InOutSine));
                s.SetLoops(-1, LoopType.Yoyo);
                warningTween = s;
            }
        }
        else
        {
            if (warningMaterialInstance != null)
            {
                warningMaterialInstance.SetFloat(WarningIntensityProp, 0f);
            }
            slotImage.material = defaultImageMaterial;
            slotImage.DOColor(defaultImageColor, 0.14f).SetEase(Ease.OutQuad);
            slotImage.rectTransform.DOScale(defaultImageScale, 0.14f).SetEase(Ease.OutQuad);
        }
    }

    private bool EnsureWarningMaterial()
    {
        if (warningMaterialInstance != null) return true;

        Shader shader = Shader.Find("Custom/UI_WarningFlash");
        if (shader == null) return false;

        warningMaterialInstance = new Material(shader);
        return true;
    }

    private void EnsureVisualCached()
    {
        if (hasCachedVisuals) return;

        if (slotImage == null)
        {
            slotImage = GetComponentInChildren<Image>(true);
        }

        if (slotImage != null)
        {
            defaultImageColor = slotImage.color;
            defaultImageScale = slotImage.rectTransform.localScale;
            defaultImageMaterial = slotImage.material;
        }

        hasCachedVisuals = true;
    }

    private void OnDestroy()
    {
        warningTween?.Kill();
        if (warningMaterialInstance != null)
        {
            Destroy(warningMaterialInstance);
            warningMaterialInstance = null;
        }
    }
}
