using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    private static InputManager _fallbackInstance;
    public static InputManager Instance => LevelManager.Instance != null && LevelManager.Instance.InputManager != null 
        ? LevelManager.Instance.InputManager 
        : _fallbackInstance;

    /// <summary>
    /// Fired when a MatchItem is confirmed and picked to be sent to the dock.
    /// </summary>
    public static event Action<MatchItem> OnItemPicked;

    /// <summary>
    /// Fired when hovering/selecting over a MatchItem.
    /// </summary>
    public static event Action<MatchItem> OnItemHighlighted;

    [Header("Camera & Raycast")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask itemLayer = ~0;
    [SerializeField] private float maxRayDistance = 100f;
    [SerializeField] private bool blockRaycastOverUI = true;

    [Header("Drag & Selection Settings")]
    [Tooltip("Whether moving finger away from cards to an empty area cancels current selection.")]
    [SerializeField] private bool cancelSelectionOnDragAway = false;

    private MatchItem currentlySelectedItem;
    private bool isPointerActive;

    public MatchItem CurrentlySelectedItem => currentlySelectedItem;

    private void Awake()
    {
        _fallbackInstance = this;

        if (LevelManager.Instance == null)
        {
            Initialize();
        }
    }

    /// <summary>
    /// Explicitly initialized by LevelManager in sequence.
    /// </summary>
    public void Initialize()
    {
        _fallbackInstance = this;

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
        // Stop receiving input if level is not actively playing (Won, Lost, or Paused)
        if (LevelManager.Instance != null && LevelManager.Instance.CurrentState != LevelManager.LevelState.Playing)
        {
            if (currentlySelectedItem != null)
            {
                ResetCurrentSelection();
            }
            return;
        }

        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        Vector2 screenPos = pointer.position.ReadValue();
        bool wasPressed = pointer.press.wasPressedThisFrame;
        bool isPressed = pointer.press.isPressed;
        bool wasReleased = pointer.press.wasReleasedThisFrame;

        // Prevent in-game interaction when clicking over UI
        if (blockRaycastOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            if (wasReleased)
            {
                ResetCurrentSelection();
                isPointerActive = false;
            }
            return;
        }

        // PHASE 1: Pointer Down
        if (wasPressed)
        {
            isPointerActive = true;
            TrySelectAt(screenPos);
        }

        // PHASE 2: Pointer Drag / Hold
        if (isPressed)
        {
            isPointerActive = true;
            TrySelectAt(screenPos);
        }

        // PHASE 3: Pointer Up / Release
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
                    if (itemUnderPointer.IsFrozen)
                    {
                        itemUnderPointer.PlayFrozenFeedback();
                    }
                    else
                    {
                        ConfirmPick(itemUnderPointer);
                    }
                }
            }

            isPointerActive = false;
            currentlySelectedItem = null;
        }
    }

    /// <summary>
    /// Queries MatchItem at screen position and selects it.
    /// </summary>
    private void TrySelectAt(Vector2 screenPos)
    {
        // Do not allow selecting new cards if dock is full
        if (DockManager.Instance != null && !DockManager.Instance.HasEmptySlot)
        {
            return;
        }

        MatchItem hitItem = GetItemAt(screenPos);

        if (hitItem != null && hitItem.IsFrozen)
        {
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                hitItem.PlayFrozenFeedback();
            }
            if (currentlySelectedItem != null)
            {
                ResetCurrentSelection();
            }
            return;
        }

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
    /// Raycasts for MatchItem at screen position.
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
    /// Confirms picked item, plays feedback, and triggers dock event.
    /// </summary>
    private void ConfirmPick(MatchItem item)
    {
        if (item == null) return;

        if (item.IsFrozen)
        {
            item.PlayFrozenFeedback();
            return;
        }

        // Cancel pickup if dock is full
        if (DockManager.Instance != null && !DockManager.Instance.HasEmptySlot)
        {
            item.Deselect();
            return;
        }

        // Trigger haptic if not already triggered during Select()
        if (item.CurrentState != MatchItem.ItemState.Selected)
        {
            Taptic.Light();
        }

        // Immediately switch to Flying state to prevent repeated clicks
        item.SetState(MatchItem.ItemState.Flying);
        item.PlayClickFeedback();

        // Play sound as card begins flying towards dock
        SoundManager.PlaySound(SoundType.CardSelect);

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
