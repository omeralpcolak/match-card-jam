using System;
using UnityEngine;

[SelectionBase]
public class MatchItem : MonoBehaviour
{
    public enum ItemState
    {
        InPhysics,
        Selected,
        Flying,
        InDock,
        Matched
    }

    [Header("Item Identity")]
    [SerializeField] private int id;
    [SerializeField] private string itemKey;

    [Header("Components")]
    [SerializeField] private MatchItemOutline outline;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Collider itemCollider;

    [Header("Physics Settings")]
    [Tooltip("Radius around the card to wake up sleeping rigidbodies when picked from the table.")]
    [SerializeField] private float physicsWakeRadius = 2.5f;

    private ItemState currentState = ItemState.InPhysics;
    private Vector3 initialScale = Vector3.one;
    private bool isInitialized = false;

    public int Id => id;
    public string ItemKey => string.IsNullOrEmpty(itemKey) ? id.ToString() : itemKey;
    public ItemState CurrentState => currentState;
    public Vector3 InitialScale => initialScale;
    public MatchItemOutline Outline => outline;
    public Collider ItemCollider => itemCollider;
    public Rigidbody Rb => rb;
    public bool IsInitialized => isInitialized;

    private void Awake()
    {
        // Fallback for isolated testing if not explicitly initialized by LevelManager
        if (!isInitialized)
        {
            Initialize();
        }
    }

    /// <summary>
    /// Explicitly called by LevelManager; sets up physics, collider, and outline.
    /// </summary>
    public void Initialize()
    {
        if (isInitialized) return;

        initialScale = transform.localScale;

        // 1. Bind physics and collider components
        if (rb == null) rb = GetComponent<Rigidbody>() ?? GetComponentInChildren<Rigidbody>();

        if (itemCollider == null)
        {
            itemCollider = GetComponent<Collider>() ?? GetComponentInChildren<Collider>();
            if (itemCollider == null)
            {
                MeshRenderer mr = GetComponentInChildren<MeshRenderer>();
                if (mr != null)
                {
                    itemCollider = mr.gameObject.AddComponent<BoxCollider>();
                }
            }
        }

        // 2. Locate and initialize outline component
        InitOutline();

        isInitialized = true;
    }

    private void InitOutline()
    {
        if (outline == null)
        {
            outline = GetComponent<MatchItemOutline>() ?? GetComponentInChildren<MatchItemOutline>();
            if (outline == null)
            {
                outline = gameObject.AddComponent<MatchItemOutline>();
            }
        }

        MeshRenderer mainRenderer = GetComponentInChildren<MeshRenderer>();
        outline.Initialize(mainRenderer);
    }

    public void Select()
    {
        if (currentState != ItemState.InPhysics) return;

        currentState = ItemState.Selected;
        if (outline != null)
        {
            outline.SetHighlighted(true);
        }

        Taptic.Light();
    }

    public void Deselect()
    {
        if (currentState != ItemState.Selected) return;

        currentState = ItemState.InPhysics;
        if (outline != null)
        {
            outline.SetHighlighted(false);
        }
    }

    public void PlayClickFeedback(Action onComplete = null)
    {
        if (outline != null)
        {
            outline.PlayClickFeedback(onComplete);
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    public void SetState(ItemState newState)
    {
        currentState = newState;

        if (newState == ItemState.Flying || newState == ItemState.InDock || newState == ItemState.Matched)
        {
            Vector3 pickPosition = transform.position;

            // Disable raycast and physics collision during flight or dock phase
            if (itemCollider != null)
            {
                itemCollider.enabled = false;
            }

            if (rb != null)
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
            }

            // Wake up resting rigidbodies lying on or touching this card
            WakeNearbyRigidbodies(pickPosition, physicsWakeRadius);
        }
        else if (newState == ItemState.InPhysics)
        {
            if (itemCollider != null)
            {
                itemCollider.enabled = true;
            }
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.WakeUp();
            }
            if (outline != null)
            {
                outline.SetHighlighted(false);
            }
        }
    }

    /// <summary>
    /// When a card is picked from the table, wakes up any sleeping rigidbodies resting
    /// on top of or in contact with it so they fall naturally with gravity.
    /// </summary>
    private void WakeNearbyRigidbodies(Vector3 center, float radius)
    {
        Collider[] colliders = Physics.OverlapSphere(center, radius);
        for (int i = 0; i < colliders.Length; i++)
        {
            Rigidbody otherRb = colliders[i].attachedRigidbody;
            if (otherRb != null && otherRb != rb && !otherRb.isKinematic)
            {
                otherRb.WakeUp();
            }
        }
    }
}
