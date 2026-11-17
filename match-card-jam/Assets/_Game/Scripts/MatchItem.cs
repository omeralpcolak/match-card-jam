using System;
using DG.Tweening;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

    [Header("Dock Settings")]
    [Tooltip("Multiplier applied to DockSlot's targetScale when this item sits in the dock (1 = default).")]
    [Min(0.1f)]
    [SerializeField] private float dockScaleMultiplier = 1f;

    [Header("Mystery Settings")]
    [Tooltip("If true, this item shows a '?' mystery material on the board and reveals its true identity upon entering the dock.")]
    [SerializeField] private bool isMystery = false;
    [Tooltip("Mystery material applied while hidden on the board. Auto-assigned in Editor based on Card/Dice if left empty.")]
    [SerializeField] private Material mysteryMaterial;

    private ItemState currentState = ItemState.InPhysics;
    private Vector3 initialScale = Vector3.one;
    private bool isInitialized = false;
    private bool isRevealed = false;
    private MeshRenderer mainRenderer;
    private Material[] cachedMaterials;

    public int Id => id;
    public string ItemKey => string.IsNullOrEmpty(itemKey) ? id.ToString() : itemKey;
    public ItemState CurrentState => currentState;
    public Vector3 InitialScale => initialScale;
    public float DockScaleMultiplier => dockScaleMultiplier;
    public bool IsMystery => isMystery;
    public bool IsRevealed => isRevealed;
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

        // 3. Apply mystery visual if configured
        if (isMystery && !isRevealed)
        {
            ApplyMysteryVisual();
        }

        isInitialized = true;
    }

    private void InitOutline()
    {
        if (outline == null)
        {
            outline = GetComponent<MatchItemOutline>() ?? GetComponentInChildren<MeshRenderer>()?.GetComponent<MatchItemOutline>();
            if (outline == null)
            {
                outline = GetComponentInChildren<MatchItemOutline>() ?? gameObject.AddComponent<MatchItemOutline>();
            }
        }

        if (mainRenderer == null)
        {
            mainRenderer = outline.MainMeshRenderer != null ? outline.MainMeshRenderer : GetComponentInChildren<MeshRenderer>();
        }
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

            if (newState == ItemState.InDock && isMystery && !isRevealed)
            {
                RevealMystery();
            }
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

    /// <summary>
    /// Configures whether this item is a Mystery item at runtime or in Editor scripts.
    /// </summary>
    public void SetMystery(bool mystery, Material overrideMaterial = null)
    {
        isMystery = mystery;
        if (overrideMaterial != null)
        {
            mysteryMaterial = overrideMaterial;
        }

        if (!isInitialized) return;

        if (isMystery && !isRevealed)
        {
            ApplyMysteryVisual();
        }
        else if (!isMystery && isRevealed == false && cachedMaterials != null)
        {
            RestoreOriginalMaterials();
        }
    }

    /// <summary>
    /// Reveals the true identity of a Mystery item with a scale pop, outline flash, and haptic when it lands in the dock.
    /// </summary>
    public void RevealMystery()
    {
        if (!isMystery || isRevealed) return;

        isRevealed = true;

        Vector3 baseScale = transform.localScale;
        Vector3 peakScale = baseScale * 1.25f;
        const float expandDuration = 0.1f;
        const float settleDuration = 0.16f;
        Color goldFlash = new Color(1f, 0.85f, 0.2f, 1f);

        transform.DOKill();
        RestoreOriginalMaterials();
        SoundManager.PlaySound(SoundType.MysteryReveal);
        Taptic.Light();

        if (outline != null)
        {
            outline.SetOutlineColor(goldFlash);
        }

        Sequence revealSeq = DOTween.Sequence();
        revealSeq.Append(transform.DOScale(peakScale, expandDuration).SetEase(Ease.OutQuad));
        revealSeq.Append(transform.DOScale(baseScale, settleDuration).SetEase(Ease.OutBack, 1.4f));

        revealSeq.OnComplete(() =>
        {
            transform.localScale = baseScale;
            if (outline != null)
            {
                outline.SetHighlighted(false);
            }
        });

        revealSeq.OnKill(() =>
        {
            RestoreOriginalMaterials();
            if (outline != null)
            {
                outline.SetHighlighted(false);
            }
        });
    }

    private void ApplyMysteryVisual()
    {
        if (mainRenderer == null)
        {
            mainRenderer = outline != null && outline.MainMeshRenderer != null
                ? outline.MainMeshRenderer
                : GetComponentInChildren<MeshRenderer>();
        }

        if (mainRenderer == null || mysteryMaterial == null) return;

        if (cachedMaterials == null || cachedMaterials.Length == 0)
        {
            cachedMaterials = mainRenderer.sharedMaterials;
        }

        Material[] mysteryMats = new Material[cachedMaterials.Length];
        for (int i = 0; i < mysteryMats.Length; i++)
        {
            mysteryMats[i] = mysteryMaterial;
        }
        mainRenderer.sharedMaterials = mysteryMats;
    }

    private void RestoreOriginalMaterials()
    {
        if (mainRenderer != null && cachedMaterials != null && cachedMaterials.Length > 0)
        {
            mainRenderer.sharedMaterials = cachedMaterials;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!isMystery) return;

        if (mysteryMaterial == null)
        {
            bool isDice = id >= 300 ||
                          (!string.IsNullOrEmpty(itemKey) && itemKey.IndexOf("Dice", StringComparison.OrdinalIgnoreCase) >= 0) ||
                          gameObject.name.IndexOf("Dice", StringComparison.OrdinalIgnoreCase) >= 0;

            string matPath = isDice
                ? "Assets/_Game/Materials/Dice_mystery_mat.mat"
                : "Assets/_Game/Materials/Card_mystery_mat.mat";

            mysteryMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        }
    }
#endif
}
