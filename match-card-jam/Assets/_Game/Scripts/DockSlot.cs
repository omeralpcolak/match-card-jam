using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Dock içerisindeki tek bir yuvayı (kutucuğu) temsil eder.
/// MatchItem, MatchItemRoot transform referansına parent'lanır ve local pozisyonuna tween ile oturur.
/// </summary>
public class DockSlot : MonoBehaviour
{
    [Header("Slot Identity & References")]
    [SerializeField] private int slotIndex;

    [Tooltip("MatchItem'ın parent yapılacağı child transform. Boş bırakılırsa bu transform kullanılır.")]
    [SerializeField] private Transform matchItemRoot;

    [Header("Flight Animation Details")]
    [Tooltip("Dock'a yerleşirken kartın alacağı hedef scale değeri.")]
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
    /// Yeni gelen kartı MatchItemRoot'a parent yapar ve zıplayarak yerleşmesini sağlar.
    /// </summary>
    public void PlaceItemWithJump(MatchItem item, float duration, float jumpPower, Ease ease, Action onComplete = null)
    {
        currentItem = item;

        // Kartı slotun MatchItemRoot referansına parent yap
        Transform targetParent = MatchItemRoot;
        item.transform.SetParent(targetParent, true);
        item.transform.DOKill();

        Sequence seq = DOTween.Sequence();

        // 1. Local merkeze zıplama
        seq.Join(item.transform.DOLocalJump(Vector3.zero, jumpPower, 1, duration).SetEase(ease));

        // 2. Rotasyonu sıfıra yumuşakça hizalama
        seq.Join(item.transform.DOLocalRotate(Vector3.zero, duration).SetEase(ease));

        // 3. Boyut ölçekleme (0.75f)
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
    /// Araya yeni bir kart girdiğinde mevcut kartın bir sonraki slota yumuşakça kayması.
    /// </summary>
    public void SlideItemToHere(MatchItem item, float duration, Ease ease = Ease.OutQuad)
    {
        currentItem = item;

        Transform targetParent = MatchItemRoot;
        item.transform.SetParent(targetParent, true);
        item.transform.DOKill();

        Sequence seq = DOTween.Sequence();
        seq.Join(item.transform.DOLocalMove(Vector3.zero, duration).SetEase(ease));
        seq.Join(item.transform.DOLocalRotate(Vector3.zero, duration).SetEase(ease));
        seq.Join(item.transform.DOScale(targetScale, duration).SetEase(ease));
    }

    /// <summary>
    /// Slottaki eşleşmiş veya kaldırılmış kartı serbest bırakır.
    /// </summary>
    public void Clear()
    {
        currentItem = null;
    }
}
