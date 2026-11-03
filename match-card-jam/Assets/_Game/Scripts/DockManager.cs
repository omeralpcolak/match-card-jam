using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Alt tepsideki (Dock) slotları ve toplanan MatchItem'ların sıralamasını yöneten bileşen.
/// Her bir slot DockSlot bileşeni ile temsil edilir.
/// </summary>
public class DockManager : MonoBehaviour
{
    public static DockManager Instance { get; private set; }

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

    public int Capacity => capacity;
    public IReadOnlyList<DockSlot> Slots => slots;

    public bool HasEmptySlot
    {
        get
        {
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
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Slot listesi boşsa child'lardaki DockSlot'ları otomatik topla
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
        if (item == null || !HasEmptySlot) return false;

        // 1. Akıllı Yerleşim İndeksini Bul
        int targetIndex = CalculateInsertIndex(item);

        if (targetIndex < 0 || targetIndex >= slots.Count) return false;

        // 2. Eğer hedef slot doluysa, sağındaki kartları birer slot sağa kaydır
        if (!slots[targetIndex].IsEmpty)
        {
            ShiftItemsRightFrom(targetIndex);
        }

        // 3. Yeni gelen objeyi hedef slota yerleştir ve zıplatarak uçur
        slots[targetIndex].PlaceItemWithJump(item, moveDuration, jumpPower, moveEase, () =>
        {
            OnItemAddedToDock?.Invoke(item, targetIndex);
        });

        return true;
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

        // Aynı tür yoksa ilk boş slotu bul
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

        // Sağdan sola doğru kaydırarak üzerine yazmayı önle
        for (int i = lastOccupied; i >= targetIndex; i--)
        {
            if (i + 1 < slots.Count && slots[i] != null && !slots[i].IsEmpty)
            {
                MatchItem shiftingItem = slots[i].CurrentItem;
                slots[i].Clear();
                slots[i + 1].SlideItemToHere(shiftingItem, slideDuration);
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
    /// <summary>
    /// Play mode'ta değilken editörde capacity değerine göre slotları oluşturur.
    /// </summary>
    [ContextMenu("Generate Slots (In Editor)")]
    public void GenerateSlotsInEditor()
    {
        Transform container = slotContainer != null ? slotContainer : transform;

        // Eski slotları temizle
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
