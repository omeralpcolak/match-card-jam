using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Core component managing dock slots, ordering collected MatchItems,
/// and resolving 3-item matches (Match-3).
/// </summary>
public class DockManager : MonoBehaviour
{
    private static DockManager _fallbackInstance;
    public static DockManager Instance => LevelManager.Instance != null && LevelManager.Instance.DockManager != null 
        ? LevelManager.Instance.DockManager 
        : _fallbackInstance;

    /// <summary>
    /// Fired when 3 MatchItems of the same type match and disappear (ItemId, World Position).
    /// </summary>
    public static event Action<int, Vector3> OnItemsMatched;

    [Header("Dock & Slot Settings")]
    [Tooltip("Maximum number of items the dock can hold.")]
    [SerializeField] private int capacity = 7;

    [Tooltip("Horizontal spacing between slots.")]
    [SerializeField] private float slotSpacing = 1.1f;

    [Tooltip("Slot prefab containing a DockSlot component.")]
    [SerializeField] private DockSlot slotPrefab;

    [Tooltip("Container Transform under which slots are instantiated. If null, this transform is used.")]
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

    // Active movement tracking and lock controls
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
    public event Action OnDockSettled;

    private void Awake()
    {
        _fallbackInstance = this;

        if (LevelManager.Instance == null)
        {
            Initialize();
        }
    }

    /// <summary>
    /// Explicitly called by LevelManager in initialization sequence.
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
    /// Places a chosen MatchItem into the appropriate dock slot.
    /// Intelligently clusters items of the same type side-by-side.
    /// </summary>
    public bool TryAddItem(MatchItem item)
    {
        if (item == null || isResolvingMatch || !HasEmptySlot) return false;

        // 1. Calculate intelligent insert index
        int targetIndex = CalculateInsertIndex(item);

        if (targetIndex < 0 || targetIndex >= slots.Count) return false;

        // 2. If target slot is occupied, shift cards to the right by one slot
        if (!slots[targetIndex].IsEmpty)
        {
            ShiftItemsRightFrom(targetIndex);
        }

        // 3. Place incoming item into target slot and launch with jump animation
        activeMovingCount++;
        slots[targetIndex].PlaceItemWithJump(item, moveDuration, jumpPower, moveEase, () =>
        {
            OnItemAddedToDock?.Invoke(item, targetIndex);
            OnMovementFinished();
        });

        return true;
    }

    /// <summary>
    /// Called when flight or slide animation finishes. Triggers match resolution when all movements settle.
    /// </summary>
    private void OnMovementFinished()
    {
        activeMovingCount--;
        if (activeMovingCount <= 0)
        {
            activeMovingCount = 0;
            CheckAndResolveMatches();
            OnDockSettled?.Invoke();
        }
    }

    /// <summary>
    /// Finds the last element with matching ID in the dock and returns the slot right after it.
    /// If no match exists, returns the first empty slot.
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
    /// <summary>
    /// Shifts occupied cards starting from targetIndex to the right by one unit.
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
    /// Checks if there are 3 adjacent MatchItems with the same ID in the dock.
    /// </summary>
    private void CheckAndResolveMatches()
    {
        if (isResolvingMatch || activeMovingCount > 0) return;

        int matchStartIndex = -1;

        // Due to intelligent grouping, identical types stay adjacent; check consecutive 3 slots
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
            // No 3-item match found; check if dock is deadlocked
            CheckDockDeadlock();

            // If no remaining items on board, trigger win check
            LevelManager.Instance?.CheckWinCondition();
        }
    }

    /// <summary>
    /// Triggers level loss if dock is completely full, no items in flight, and no matches remain.
    /// </summary>
    private void CheckDockDeadlock()
    {
        if (activeMovingCount == 0 && CurrentItemCount >= capacity)
        {
            LevelManager.Instance?.LoseLevel(LevelManager.LoseReason.DockFull);
        }
    }

    /// <summary>
    /// Plays 3-item match sequence: Converge at center -> Pop & Shrink -> SetActive(false) -> Compact slots.
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

        // Immediately clear slots logically
        leftSlot.Clear();
        centerSlot.Clear();
        rightSlot.Clear();

        // Stage 1: Outline flash feedback
        leftItem.PlayClickFeedback();
        centerItem.PlayClickFeedback();
        rightItem.PlayClickFeedback();

        SoundManager.PlaySound(SoundType.CardMatch);

        Sequence matchSeq = DOTween.Sequence();

        // Stage 2: Left and right cards converge towards center card
        matchSeq.Append(leftItem.transform.DOMove(matchCenterWorldPos, matchConvergeDuration).SetEase(Ease.InQuad));
        matchSeq.Join(rightItem.transform.DOMove(matchCenterWorldPos, matchConvergeDuration).SetEase(Ease.InQuad));
        matchSeq.Join(centerItem.transform.DOPunchScale(Vector3.one * 0.15f, matchConvergeDuration, 1, 0.5f));

        // Stage 3: 3 converged cards scale down and disappear
        matchSeq.Append(leftItem.transform.DOScale(Vector3.zero, matchShrinkDuration).SetEase(Ease.InBack));
        matchSeq.Join(centerItem.transform.DOScale(Vector3.zero, matchShrinkDuration).SetEase(Ease.InBack));
        matchSeq.Join(rightItem.transform.DOScale(Vector3.zero, matchShrinkDuration).SetEase(Ease.InBack));

        matchSeq.OnComplete(() =>
        {
            // Deactivate matched cards
            if (leftItem != null) leftItem.gameObject.SetActive(false);
            if (centerItem != null) centerItem.gameObject.SetActive(false);
            if (rightItem != null) rightItem.gameObject.SetActive(false);

            // Fire matched event
            OnItemsMatched?.Invoke(matchedId, matchCenterWorldPos);

            // Slide remaining cards left to close the 3-slot gap
            CompactSlotsLeft();
        });
    }

    /// <summary>
    /// Slides all remaining cards to the left to close gaps after a match.
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
                            // Check again for chained matches
                            CheckAndResolveMatches();
                        }
                    });
                }
                nextTargetSlot++;
            }
        }

        // If no cards need shifting (rightmost 3 matched), unlock and check immediately
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
