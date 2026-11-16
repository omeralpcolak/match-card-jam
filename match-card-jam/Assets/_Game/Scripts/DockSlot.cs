using System;
using DG.Tweening;
using UnityEngine;

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

    private MatchItem currentItem;

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
        Vector3 peakScale = targetScale * flightPeakScaleMultiplier;

        Sequence seq = DOTween.Sequence();

        // 1. Jump to local center along arc
        seq.Join(item.transform.DOLocalJump(Vector3.zero, jumpPower, 1, duration).SetEase(ease));

        // 2. Align rotation with subtle snappy overshoot
        seq.Join(item.transform.DOLocalRotate(Vector3.zero, duration).SetEase(Ease.OutBack, 1.15f));

        // 3. Mid-air scale pop (swell up on lift, snap to targetScale on landing)
        Sequence scaleSeq = DOTween.Sequence();
        scaleSeq.Append(item.transform.DOScale(peakScale, riseDuration).SetEase(Ease.OutQuad));
        scaleSeq.Append(item.transform.DOScale(targetScale, fallDuration).SetEase(Ease.InQuad));
        seq.Join(scaleSeq);

        seq.OnComplete(() =>
        {
            if (item != null)
            {
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.identity;
                item.transform.localScale = targetScale;
                item.SetState(MatchItem.ItemState.InDock);
            }
            onComplete?.Invoke();
        });
    }

    /// <summary>
    /// Smoothly slides an existing card into this new slot with a snappy spring ease when items shift or gaps close.
    /// </summary>
    public void SlideItemToHere(MatchItem item, float duration, Ease ease = Ease.OutBack, Action onComplete = null)
    {
        currentItem = item;

        Transform targetParent = MatchItemRoot;
        item.transform.SetParent(targetParent, true);
        item.transform.DOKill();

        Sequence seq = DOTween.Sequence();
        seq.Join(item.transform.DOLocalMove(Vector3.zero, duration).SetEase(Ease.OutBack, 1.1f));
        seq.Join(item.transform.DOLocalRotate(Vector3.zero, duration).SetEase(Ease.OutQuad));
        seq.Join(item.transform.DOScale(targetScale, duration).SetEase(Ease.OutQuad));

        seq.OnComplete(() =>
        {
            onComplete?.Invoke();
        });
    }

    /// <summary>
    /// Clears and releases the matched or removed card from this slot.
    /// </summary>
    public void Clear()
    {
        currentItem = null;
    }
}
