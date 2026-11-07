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
    [Tooltip("Kart masadan alındığında etrafındaki uyuyan diğer kartları uyandırma yarıçapı.")]
    [SerializeField] private float physicsWakeRadius = 2.5f;

    private ItemState currentState = ItemState.InPhysics;
    private bool isInitialized = false;

    public int Id => id;
    public string ItemKey => string.IsNullOrEmpty(itemKey) ? id.ToString() : itemKey;
    public ItemState CurrentState => currentState;
    public MatchItemOutline Outline => outline;
    public Collider ItemCollider => itemCollider;
    public Rigidbody Rb => rb;
    public bool IsInitialized => isInitialized;

    private void Awake()
    {
        // LevelManager tarafından önceden çağrılmadıysa tekil testler için fallback
        if (!isInitialized)
        {
            Initialize();
        }
    }

    /// <summary>
    /// LevelManager tarafından açıkça çağrılır; fizik, collider ve outline kurulumunu tamamlar.
    /// </summary>
    public void Initialize()
    {
        if (isInitialized) return;

        // 1. Fizik ve Collider bileşenlerini bağla
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

        // 2. Outline bileşenini bul ve initialize et
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

            // Dock veya uçuş aşamasında raycast ve fizik etkileşimini kapat
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

            // Kart altından çekildiğinde üzerinde duran 'uyuyan' (Sleeping) diğer kartları uyandır
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
    /// Bir kart masadan çekildiğinde, onun üstünde duran veya temas halindeki uyuyan
    /// diğer Rigidbody'leri uyandırarak yerçekimiyle masaya düşmelerini sağlar.
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
