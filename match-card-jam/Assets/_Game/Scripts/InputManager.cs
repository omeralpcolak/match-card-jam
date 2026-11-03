using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    /// <summary>
    /// Bir MatchItem onaylanıp dock'a gönderilmek üzere toplandığında fırlar.
    /// </summary>
    public static event Action<MatchItem> OnItemPicked;

    /// <summary>
    /// Bir MatchItem'ın üzerine parmakla gelindiğinde/seçildiğinde fırlar.
    /// </summary>
    public static event Action<MatchItem> OnItemHighlighted;

    [Header("Camera & Raycast")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask itemLayer = ~0;
    [SerializeField] private float maxRayDistance = 100f;
    [SerializeField] private bool blockRaycastOverUI = true;

    [Header("Drag & Selection Settings")]
    [Tooltip("Parmağı kartlardan boş bir alana kaydırınca mevcut seçimi iptal etsin mi?")]
    [SerializeField] private bool cancelSelectionOnDragAway = false;

    private MatchItem currentlySelectedItem;
    private bool isPointerActive;

    public MatchItem CurrentlySelectedItem => currentlySelectedItem;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void Update()
    {
        HandlePointerInput();
    }

    private void HandlePointerInput()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        Vector2 screenPos = pointer.position.ReadValue();
        bool wasPressed = pointer.press.wasPressedThisFrame;
        bool isPressed = pointer.press.isPressed;
        bool wasReleased = pointer.press.wasReleasedThisFrame;

        // UI üzerine tıklandığında oyun içi etkileşimi engelle
        if (blockRaycastOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            if (wasReleased)
            {
                ResetCurrentSelection();
                isPointerActive = false;
            }
            return;
        }

        // 1. FAZ: Dokunma/Basış Başlangıcı (Pointer Down)
        if (wasPressed)
        {
            isPointerActive = true;
            TrySelectAt(screenPos);
        }

        // 2. FAZ: Parmak Basılıyken Gezinme (Pointer Drag / Hold)
        if (isPressed)
        {
            isPointerActive = true;
            TrySelectAt(screenPos);
        }

        // 3. FAZ: Dokunma Bitişi (Pointer Up)
        if (wasReleased)
        {
            if (currentlySelectedItem != null)
            {
                ConfirmPick(currentlySelectedItem);
            }
            else if (isPointerActive)
            {
                MatchItem itemUnderPointer = GetItemAt(screenPos);
                if (itemUnderPointer != null && itemUnderPointer.CurrentState == MatchItem.ItemState.InPhysics)
                {
                    ConfirmPick(itemUnderPointer);
                }
            }

            isPointerActive = false;
            currentlySelectedItem = null;
        }
    }

    /// <summary>
    /// Belirtilen ekran pozisyonundaki MatchItem'ı bulur ve seçili hale getirir.
    /// </summary>
    private void TrySelectAt(Vector2 screenPos)
    {
        // Dock doluysa yeni kart seçilmesine izin verme
        if (DockManager.Instance != null && !DockManager.Instance.HasEmptySlot)
        {
            return;
        }

        MatchItem hitItem = GetItemAt(screenPos);

        if (hitItem != null && (hitItem.CurrentState == MatchItem.ItemState.InPhysics || hitItem.CurrentState == MatchItem.ItemState.Selected))
        {
            if (currentlySelectedItem != hitItem)
            {
                if (currentlySelectedItem != null)
                {
                    currentlySelectedItem.Deselect();
                }

                currentlySelectedItem = hitItem;
                currentlySelectedItem.Select();
                OnItemHighlighted?.Invoke(currentlySelectedItem);
            }
        }
        else if (cancelSelectionOnDragAway && currentlySelectedItem != null)
        {
            ResetCurrentSelection();
        }
    }

    /// <summary>
    /// Raycast ile ekrandaki objeyi sorgular.
    /// </summary>
    private MatchItem GetItemAt(Vector2 screenPos)
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return null;

        Ray ray = targetCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, itemLayer))
        {
            return hit.collider.GetComponentInParent<MatchItem>();
        }

        return null;
    }

    /// <summary>
    /// Seçilen objeyi kesinleştirir, geri bildirimi oynatır ve dock event'ini fırlatır.
    /// </summary>
    private void ConfirmPick(MatchItem item)
    {
        if (item == null) return;

        // Dock doluysa toplamayı iptal et
        if (DockManager.Instance != null && !DockManager.Instance.HasEmptySlot)
        {
            item.Deselect();
            return;
        }

        // Hemen Flying state'e alarak ardışık tıklamalarda tekrar seçilmesini önle
        item.SetState(MatchItem.ItemState.Flying);
        item.PlayClickFeedback();

        OnItemPicked?.Invoke(item);
    }

    public void ResetCurrentSelection()
    {
        if (currentlySelectedItem != null)
        {
            currentlySelectedItem.Deselect();
            currentlySelectedItem = null;
        }
    }
}
