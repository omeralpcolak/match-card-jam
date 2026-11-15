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
    /// Parents the incoming item to MatchItemRoot and animates placement with a jump tween.
    /// </summary>
    public void PlaceItemWithJump(MatchItem item, float duration, float jumpPower, Ease ease, Action onComplete = null)
    {
        currentItem = item;

        // Parent card to the slot's MatchItemRoot reference
        Transform targetParent = MatchItemRoot;
        item.transform.SetParent(targetParent, true);
        item.transform.DOKill();

        Sequence seq = DOTween.Sequence();

        // 1. Jump to local center
        seq.Join(item.transform.DOLocalJump(Vector3.zero, jumpPower, 1, duration).SetEase(ease));

        // 2. Smoothly align rotation to zero
        seq.Join(item.transform.DOLocalRotate(Vector3.zero, duration).SetEase(ease));

        // 3. Scale to target size
        seq.Join(item.transform.DOScale(targetScale, duration).SetEase(ease));

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
    /// Smoothly slides an existing card into this new slot when items shift or gaps close.
    /// </summary>
    public void SlideItemToHere(MatchItem item, float duration, Ease ease = Ease.OutQuad, Action onComplete = null)
    {
        currentItem = item;

        Transform targetParent = MatchItemRoot;
        item.transform.SetParent(targetParent, true);
        item.transform.DOKill();

        Sequence seq = DOTween.Sequence();
        seq.Join(item.transform.DOLocalMove(Vector3.zero, duration).SetEase(ease));
        seq.Join(item.transform.DOLocalRotate(Vector3.zero, duration).SetEase(ease));
        seq.Join(item.transform.DOScale(targetScale, duration).SetEase(ease));

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
