using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Alt tepsideki (Dock) slotları, toplanan MatchItem'ların sıralamasını ve
/// 3'lü eşleşme (Match-3) çözümlemesini yöneten ana bileşen.
/// </summary>
public class DockManager : MonoBehaviour
{
    private static DockManager _fallbackInstance;
    public static DockManager Instance => LevelManager.Instance != null && LevelManager.Instance.DockManager != null 
        ? LevelManager.Instance.DockManager 
        : _fallbackInstance;

    /// <summary>
    /// 3 aynı tipteki MatchItem eşleşip yok olduğunda fırlar. (ItemId, Dünya Pozisyonu)
    /// </summary>
    public static event Action<int, Vector3> OnItemsMatched;

    [Header("Dock & Slot Settings")]
    [Tooltip("Dock'un alabileceği maksimum obje sayısı.")]
    [SerializeField] private int capacity = 7;

    [Tooltip("Slotlar arası yatay mesafe.")]
    [SerializeField] private float slotSpacing = 1.1f;

    [Tooltip("Slot prefab'ı (DockSlot bileşenine sahip).")]
    [SerializeField] private DockSlot slotPrefab;

    [Tooltip("Slotların child yapılacağı container. Boşsa bu transform kullanılır.")]
    [SerializeField] private Transform slotContainer;

    [Header("Slots List")]
    [SerializeField] private List<DockSlot> slots = new();

    [Header("Animation Settings")]
    [SerializeField] private float jumpPower = 1.2f;
    [SerializeField] private float moveDuration = 0.35f;
    [SerializeField] private float slideDuration = 0.22f;
    [SerializeField] private Ease moveEase = Ease.OutQuad;

    [Header("Match Animation Settings")]
    [SerializeField] private float matchConvergeDuration = 0.16f;
    [SerializeField] private float matchShrinkDuration = 0.18f;

    // Aktif hareket takibi ve kilit kontrolü
    private int activeMovingCount = 0;
    private bool isResolvingMatch = false;

    public int Capacity => capacity;
    public IReadOnlyList<DockSlot> Slots => slots;
    public bool IsResolvingMatch => isResolvingMatch;
    public int ActiveMovingCount => activeMovingCount;

    public bool HasEmptySlot
    {
        get
        {
            if (isResolvingMatch) return false;

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].IsEmpty) return true;
            }
            return false;
        }
    }

    public int CurrentItemCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && !slots[i].IsEmpty) count++;
            }
            return count;
        }
    }

    public event Action<MatchItem, int> OnItemAddedToDock;

    private void Awake()
    {
        _fallbackInstance = this;

        if (LevelManager.Instance == null)
        {
            Initialize();
        }
    }

    /// <summary>
    /// LevelManager tarafından açık sırayla çağrılır.
    /// </summary>
    public void Initialize()
    {
        _fallbackInstance = this;

        if (slots == null || slots.Count == 0)
        {
            RefreshSlotsFromChildren();
        }
    }

    private void OnEnable()
    {
        InputManager.OnItemPicked += HandleItemPicked;
    }

    private void OnDisable()
    {
        InputManager.OnItemPicked -= HandleItemPicked;
    }

    private void HandleItemPicked(MatchItem item)
    {
        TryAddItem(item);
    }

    /// <summary>
    /// Seçilen bir MatchItem'ı dock'a uygun slota yerleştirir.
    /// Aynı tipteki objeleri akıllıca yan yana gruplar.
    /// </summary>
    public bool TryAddItem(MatchItem item)
    {
        if (item == null || isResolvingMatch || !HasEmptySlot) return false;

        // 1. Akıllı Yerleşim İndeksini Bul
        int targetIndex = CalculateInsertIndex(item);

        if (targetIndex < 0 || targetIndex >= slots.Count) return false;

        // 2. Eğer hedef slot doluysa, sağındaki kartları birer slot sağa kaydır
        if (!slots[targetIndex].IsEmpty)
        {
            ShiftItemsRightFrom(targetIndex);
        }

        // 3. Yeni gelen objeyi hedef slota yerleştir ve zıplatarak uçur
        activeMovingCount++;
        slots[targetIndex].PlaceItemWithJump(item, moveDuration, jumpPower, moveEase, () =>
        {
            OnItemAddedToDock?.Invoke(item, targetIndex);
            OnMovementFinished();
        });

        return true;
    }

    /// <summary>
    /// Uçuş veya kayma hareketi bittiğinde çağrılır. Tüm hareketler bittiyse eşleşmeyi tetikler.
    /// </summary>
    private void OnMovementFinished()
    {
        activeMovingCount--;
        if (activeMovingCount <= 0)
        {
            activeMovingCount = 0;
            CheckAndResolveMatches();
        }
    }

    /// <summary>
    /// Dock içinde aynı Id'ye sahip son elemanı bulur ve hemen sonrasını döner.
    /// Yoksa ilk boş slotu döner.
    /// </summary>
    private int CalculateInsertIndex(MatchItem newItem)
    {
        int lastMatchingIndex = -1;

        for (int i = 0; i < slots.Count; i++)
        {
            DockSlot slot = slots[i];
            if (slot != null && !slot.IsEmpty && slot.CurrentItem.Id == newItem.Id)
            {
                lastMatchingIndex = i;
            }
        }

        if (lastMatchingIndex != -1 && lastMatchingIndex + 1 < slots.Count)
        {
            return lastMatchingIndex + 1;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && slots[i].IsEmpty)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// targetIndex'ten itibaren dolu olan slotlardaki kartları sağa doğru 1 birim kaydırır.
    /// </summary>
    private void ShiftItemsRightFrom(int targetIndex)
    {
        int lastOccupied = FindLastOccupiedIndex();

        for (int i = lastOccupied; i >= targetIndex; i--)
        {
            if (i + 1 < slots.Count && slots[i] != null && !slots[i].IsEmpty)
            {
                MatchItem shiftingItem = slots[i].CurrentItem;
                slots[i].Clear();

                activeMovingCount++;
                slots[i + 1].SlideItemToHere(shiftingItem, slideDuration, Ease.OutQuad, () =>
                {
                    OnMovementFinished();
                });
            }
        }
    }

    private int FindLastOccupiedIndex()
    {
        for (int i = slots.Count - 1; i >= 0; i--)
        {
            if (slots[i] != null && !slots[i].IsEmpty)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Dock içinde yan yana 3 tane aynı ID'li MatchItem olup olmadığını kontrol eder.
    /// </summary>
    private void CheckAndResolveMatches()
    {
        if (isResolvingMatch || activeMovingCount > 0) return;

        int matchStartIndex = -1;

        // Akıllı gruplama sayesinde aynı türler yan yana durur; 3 ardışık slotu tara
        for (int i = 0; i <= slots.Count - 3; i++)
        {
            if (slots[i] != null && !slots[i].IsEmpty &&
                slots[i + 1] != null && !slots[i + 1].IsEmpty &&
                slots[i + 2] != null && !slots[i + 2].IsEmpty)
            {
                int id1 = slots[i].CurrentItem.Id;
                int id2 = slots[i + 1].CurrentItem.Id;
                int id3 = slots[i + 2].CurrentItem.Id;

                if (id1 == id2 && id2 == id3)
                {
                    matchStartIndex = i;
                    break;
                }
            }
        }

        if (matchStartIndex != -1)
        {
            ResolveMatch(matchStartIndex);
        }
        else
        {
            // 3'lü eşleşme yok; dock tıkandı mı kontrol et
            CheckDockDeadlock();

            // Masada toplanacak nesne kalmadıysa zafer kontrolünü tetikle
            LevelManager.Instance?.CheckWinCondition();
        }
    }

    /// <summary>
    /// Dock tamamen doluysa, uçuşta başka nesne yoksa ve 3'lü eşleşme kalmadıysa oyunu kaybettirir.
    /// </summary>
    private void CheckDockDeadlock()
    {
        if (activeMovingCount == 0 && CurrentItemCount >= capacity)
        {
            LevelManager.Instance?.LoseLevel(LevelManager.LoseReason.DockFull);
        }
    }

    /// <summary>
    /// Bulunan 3'lü eşleşmeyi oynatır: Ortada birleşme -> Pop & Küçülme -> SetActive(false) -> Boşlukları kapatma.
    /// </summary>
    private void ResolveMatch(int startIndex)
    {
        isResolvingMatch = true;

        DockSlot leftSlot = slots[startIndex];
        DockSlot centerSlot = slots[startIndex + 1];
        DockSlot rightSlot = slots[startIndex + 2];

        MatchItem leftItem = leftSlot.CurrentItem;
        MatchItem centerItem = centerSlot.CurrentItem;
        MatchItem rightItem = rightSlot.CurrentItem;

        int matchedId = centerItem.Id;
        Vector3 matchCenterWorldPos = centerSlot.MatchItemRoot.position;

        // Slotları mantıksal olarak hemen boşalt
        leftSlot.Clear();
        centerSlot.Clear();
        rightSlot.Clear();

        // 1. Aşama: Outline beyaz parlaması
        leftItem.PlayClickFeedback();
        centerItem.PlayClickFeedback();
        rightItem.PlayClickFeedback();

        Sequence matchSeq = DOTween.Sequence();

        // 2. Aşama: Sol ve sağ kartların ortadaki kartın üzerine doğru hızla kayması
        matchSeq.Append(leftItem.transform.DOMove(matchCenterWorldPos, matchConvergeDuration).SetEase(Ease.InQuad));
        matchSeq.Join(rightItem.transform.DOMove(matchCenterWorldPos, matchConvergeDuration).SetEase(Ease.InQuad));
        matchSeq.Join(centerItem.transform.DOPunchScale(Vector3.one * 0.15f, matchConvergeDuration, 1, 0.5f));

        // 3. Aşama: Merkezde birleşen 3 kartın zıplayıp küçülerek yok olması
        matchSeq.Append(leftItem.transform.DOScale(Vector3.zero, matchShrinkDuration).SetEase(Ease.InBack));
        matchSeq.Join(centerItem.transform.DOScale(Vector3.zero, matchShrinkDuration).SetEase(Ease.InBack));
        matchSeq.Join(rightItem.transform.DOScale(Vector3.zero, matchShrinkDuration).SetEase(Ease.InBack));

        matchSeq.OnComplete(() =>
        {
            // Kartları devre dışı bırak
            if (leftItem != null) leftItem.gameObject.SetActive(false);
            if (centerItem != null) centerItem.gameObject.SetActive(false);
            if (rightItem != null) rightItem.gameObject.SetActive(false);

            // Eşleşme event'ini ateşle
            OnItemsMatched?.Invoke(matchedId, matchCenterWorldPos);

            // Sağda kalan kartları sola kaydırıp aradaki 3 slotluk boşluğu kapat
            CompactSlotsLeft();
        });
    }

    /// <summary>
    /// Eşleşme sonrası aradaki boşlukları kapatmak için sağda kalan tüm kartları sola kaydırır.
    /// </summary>
    private void CompactSlotsLeft()
    {
        int nextTargetSlot = 0;
        int shiftCount = 0;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && !slots[i].IsEmpty)
            {
                if (i != nextTargetSlot)
                {
                    MatchItem itemToShift = slots[i].CurrentItem;
                    slots[i].Clear();

                    shiftCount++;
                    slots[nextTargetSlot].SlideItemToHere(itemToShift, slideDuration, Ease.OutQuad, () =>
                    {
                        shiftCount--;
                        if (shiftCount <= 0)
                        {
                            isResolvingMatch = false;
                            // Zincirleme eşleşme var mı diye tekrar kontrol et
                            CheckAndResolveMatches();
                        }
                    });
                }
                nextTargetSlot++;
            }
        }

        // Eğer kayacak kart yoksa (en sağdaki 3'lü eşleştiyse) doğrudan kilidi aç ve kontrol et
        if (shiftCount == 0)
        {
            isResolvingMatch = false;
            CheckAndResolveMatches();
        }
    }

    public void RefreshSlotsFromChildren()
    {
        Transform container = slotContainer != null ? slotContainer : transform;
        slots = new List<DockSlot>(container.GetComponentsInChildren<DockSlot>());

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
            {
                slots[i].SlotIndex = i;
            }
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Generate Slots (In Editor)")]
    public void GenerateSlotsInEditor()
    {
        Transform container = slotContainer != null ? slotContainer : transform;

        ClearSlotsInEditor();

        slots = new List<DockSlot>();
        float startX = -((capacity - 1) * slotSpacing) * 0.5f;

        for (int i = 0; i < capacity; i++)
        {
            float xOffset = startX + (i * slotSpacing);
            Vector3 localPos = new Vector3(xOffset, 0f, 0f);

            GameObject slotGo;

            if (slotPrefab != null)
            {
                slotGo = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab.gameObject, container);
                slotGo.transform.localPosition = localPos;
                slotGo.transform.localRotation = Quaternion.identity;
                slotGo.transform.localScale = Vector3.one;
            }
            else
            {
                slotGo = new GameObject($"Slot_{i}");
                slotGo.transform.SetParent(container, false);
                slotGo.transform.localPosition = localPos;
                slotGo.AddComponent<DockSlot>();
            }

            slotGo.name = $"Slot_{i}";
            Undo.RegisterCreatedObjectUndo(slotGo, "Generate Dock Slot");

            DockSlot dockSlot = slotGo.GetComponent<DockSlot>();
            dockSlot.SlotIndex = i;
            slots.Add(dockSlot);
        }

        EditorUtility.SetDirty(this);
    }

    [ContextMenu("Clear Slots (In Editor)")]
    public void ClearSlotsInEditor()
    {
        Transform container = slotContainer != null ? slotContainer : transform;

        var existingSlots = container.GetComponentsInChildren<DockSlot>(true);
        for (int i = existingSlots.Length - 1; i >= 0; i--)
        {
            if (existingSlots[i] != null)
            {
                Undo.DestroyObjectImmediate(existingSlots[i].gameObject);
            }
        }

        slots.Clear();
        EditorUtility.SetDirty(this);
    }
#endif

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);

        if (slots != null && slots.Count > 0)
        {
            foreach (var slot in slots)
            {
                if (slot != null)
                {
                    Gizmos.DrawWireCube(slot.transform.position, new Vector3(0.8f, 0.1f, 1.1f));
                }
            }
        }
        else
        {
            Transform container = slotContainer != null ? slotContainer : transform;
            float startX = -((capacity - 1) * slotSpacing) * 0.5f;

            for (int i = 0; i < capacity; i++)
            {
                float xOffset = startX + (i * slotSpacing);
                Vector3 worldPos = container.TransformPoint(new Vector3(xOffset, 0f, 0f));
                Gizmos.DrawWireCube(worldPos, new Vector3(0.8f, 0.1f, 1.1f));
            }
        }
    }
}
