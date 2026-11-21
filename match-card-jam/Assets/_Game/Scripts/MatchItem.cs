using System;
using System.Collections.Generic;
using DG.Tweening;
using MegaFiers;
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

    [Header("Frozen Settings")]
    [Tooltip("If true, this item starts encased in ice and cannot be picked until enough dock matches melt it.")]
    [SerializeField] private bool isFrozen = false;
    [Tooltip("Number of dock matches required to completely melt the ice.")]
    [Min(1)]
    [SerializeField] private int freezeLayers = 3;
    [Tooltip("Translucent ice material used for the frozen overlay cube. Auto-assigned in Editor if left empty.")]
    [SerializeField] private Material iceMaterial;

    [Header("Hazard Settings")]
    [Tooltip("If true, this item is a Bomb hazard: matching 3 in the dock fails the level, and it is excluded from win-condition counts.")]
    [SerializeField] private bool isBomb = false;

    private ItemState currentState = ItemState.InPhysics;
    private Vector3 initialScale = Vector3.one;
    private Vector3 initialMeshScale = Vector3.one;
    private Vector3 initialOutlineScale = Vector3.one;
    private bool isInitialized = false;
    private bool isRevealed = false;
    private int remainingFreezeLayers = 0;
    private MeshRenderer mainRenderer;
    private Material[] cachedMaterials;
    private GameObject iceVisualObject;
    private MeshRenderer iceRenderer;
    private MaterialPropertyBlock icePropertyBlock;
    private Vector3 iceBaseLocalScale = Vector3.one;
    private int iceMeltAxis = 1;
    private float iceBoundsMin = -0.5f;
    private float iceBoundsMax = 0.5f;
    private float currentMeltProgress = 0f;
    private float currentFlashAmount = 0f;
    private Tween iceMeltTween;
    private Tween iceFlashTween;
    private Tween flightStretchTween;
    private Tween dockIdleBobTween;
    private Tween dockIdleSwayTween;
    private Tween dockPairPulseTween;
    private Vector3 initialVisualLocalPos;
    private Quaternion initialVisualLocalRot = Quaternion.identity;
    private Vector3 initialVisualLocalScale = Vector3.one;
    private bool hasCachedVisualPose;
    private bool isPairPulseActive;
    private MegaModifyObject mainModObj;
    private MegaStretch mainStretch;
    private MegaModifyObject outlineModObj;
    private MegaStretch outlineStretch;
    private float currentStretchAmount;
    private Vector3 flightTargetWorldPos;
    private bool stretchInitialized;
    private static readonly Dictionary<Mesh, Mesh> subdividedMeshCache = new();
    private bool subscribedToMatchEvent = false;
    private static float lastIceMeltSoundTime = -1f;
    private static float lastSelectSoundTime = -1f;
    private static int selectPitchCombo = 0;

    private static readonly int MeltProgressProp = Shader.PropertyToID("_MeltProgress");
    private static readonly int MeltAxisProp = Shader.PropertyToID("_MeltAxis");
    private static readonly int BoundsMinProp = Shader.PropertyToID("_BoundsMin");
    private static readonly int BoundsMaxProp = Shader.PropertyToID("_BoundsMax");
    private static readonly int FlashAmountProp = Shader.PropertyToID("_FlashAmount");
    private static readonly int AlphaMultiplierProp = Shader.PropertyToID("_AlphaMultiplier");

    public int Id => id;
    public string ItemKey => string.IsNullOrEmpty(itemKey) ? id.ToString() : itemKey;
    public ItemState CurrentState => currentState;
    public Vector3 InitialScale => initialScale;
    public float DockScaleMultiplier => dockScaleMultiplier;
    public bool IsMystery => isMystery;
    public bool IsRevealed => isRevealed;
    public bool IsFrozen => remainingFreezeLayers > 0;
    public bool IsBomb => isBomb;
    public int RemainingFreezeLayers => remainingFreezeLayers;
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
        initialMeshScale = mainRenderer != null ? mainRenderer.transform.localScale : initialScale;
        if (outline != null && outline.OutlineRenderer != null)
        {
            initialOutlineScale = outline.OutlineRenderer.transform.localScale;
        }
        CacheVisualIdlePose();

        // 3. Apply mystery visual at runtime only (never bake into prefab in Edit Mode; bombs are never mystery)
        if (Application.isPlaying && isMystery && !isBomb && !isRevealed)
        {
            ApplyMysteryVisual();
        }

        // 4. Apply frozen ice overlay at runtime only
        if (Application.isPlaying && isFrozen && freezeLayers > 0)
        {
            InitializeFrozenState();
        }

        isInitialized = true;
    }

    private void OnDisable()
    {
        flightStretchTween?.Kill();
        StopDockIdle();
    }

    private void OnDestroy()
    {
        UnsubscribeFromMatchEvent();
        iceMeltTween?.Kill();
        iceFlashTween?.Kill();
        flightStretchTween?.Kill();
        dockIdleBobTween?.Kill();
        dockIdleSwayTween?.Kill();
        dockPairPulseTween?.Kill();
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

        // Disable any redundant Collider on the Outline visual child so scaling it never pushes neighbors
        if (Application.isPlaying && outline.OutlineRenderer != null)
        {
            Collider outlineCol = outline.OutlineRenderer.GetComponent<Collider>();
            if (outlineCol != null && outlineCol != itemCollider)
            {
                outlineCol.enabled = false;
            }
        }
    }

    public void Select()
    {
        if (currentState != ItemState.InPhysics || IsFrozen) return;

        currentState = ItemState.Selected;
        if (outline != null)
        {
            outline.SetHighlighted(true);
        }

        ScaleVisualRenderers(1.12f, 0.08f, Ease.OutBack);
        PlaySelectSquish();

        Taptic.Light();
    }

    private void ScaleVisualRenderers(float multiplier, float duration, Ease ease)
    {
        Transform meshT = mainRenderer != null ? mainRenderer.transform : transform;
        meshT.DOKill();
        meshT.DOScale((mainRenderer != null ? initialMeshScale : initialScale) * multiplier, duration).SetEase(ease);

        if (outline != null && outline.OutlineRenderer != null)
        {
            Transform outlineT = outline.OutlineRenderer.transform;
            if (outlineT != meshT && !outlineT.IsChildOf(meshT))
            {
                outlineT.DOKill();
                outlineT.DOScale(initialOutlineScale * multiplier, duration).SetEase(ease);
            }
        }
    }

    private void ResetVisualRenderersScale()
    {
        if (mainRenderer != null && mainRenderer.transform != transform)
        {
            mainRenderer.transform.DOKill();
            mainRenderer.transform.localScale = initialMeshScale;
        }

        if (outline != null && outline.OutlineRenderer != null)
        {
            Transform outlineT = outline.OutlineRenderer.transform;
            if (outlineT != transform && (mainRenderer == null || (!outlineT.IsChildOf(mainRenderer.transform) && outlineT != mainRenderer.transform)))
            {
                outlineT.DOKill();
                outlineT.localScale = initialOutlineScale;
            }
        }
    }

    public static void PlaySelectSoundWithCombo()
    {
        if (Time.unscaledTime - lastSelectSoundTime < 0.75f)
        {
            selectPitchCombo = Mathf.Min(selectPitchCombo + 1, 4);
        }
        else
        {
            selectPitchCombo = 0;
        }
        lastSelectSoundTime = Time.unscaledTime;

        float pitch = 1f + selectPitchCombo * 0.05f;
        SoundManager.PlaySound(SoundType.MatchItemSelect, 1f, pitch);
    }

    public void Deselect()
    {
        if (currentState != ItemState.Selected) return;

        currentState = ItemState.InPhysics;
        if (outline != null)
        {
            outline.SetHighlighted(false);
        }

        ScaleVisualRenderers(1f, 0.08f, Ease.OutQuad);
        flightStretchTween?.Kill();
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
            ResetVisualRenderersScale();

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

            if (newState == ItemState.InDock)
            {
                if (isMystery && !isRevealed)
                {
                    RevealMystery();
                }
                if (Application.isPlaying)
                {
                    StartDockIdle();
                }
            }
            else
            {
                StopDockIdle();
            }
        }
        else if (newState == ItemState.InPhysics)
        {
            StopDockIdle();
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

        if (!Application.isPlaying || !isInitialized) return;

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

    #region Frozen Logic

    private void InitializeFrozenState()
    {
        remainingFreezeLayers = Mathf.Max(1, freezeLayers);
        CreateIceVisual();
        UpdateIceVisual(animate: false);
        SubscribeToMatchEvent();
    }

    /// <summary>
    /// Configures whether this item is frozen at runtime.
    /// </summary>
    public void SetFrozen(bool frozen, int layers = 3, Material overrideIceMat = null)
    {
        isFrozen = frozen;
        freezeLayers = Mathf.Max(1, layers);
        if (overrideIceMat != null)
        {
            iceMaterial = overrideIceMat;
        }

        if (!Application.isPlaying || !isInitialized) return;

        if (isFrozen)
        {
            InitializeFrozenState();
        }
        else
        {
            remainingFreezeLayers = 0;
            UnsubscribeFromMatchEvent();
            if (iceVisualObject != null)
            {
                Destroy(iceVisualObject);
                iceVisualObject = null;
            }
        }
    }

    /// <summary>
    /// Plays tactile + visual shake feedback when the player taps a frozen item.
    /// </summary>
    public void PlayFrozenFeedback()
    {
        if (!IsFrozen) return;

        SoundManager.PlaySound(SoundType.FrozenClick);
        Taptic.Light();
        transform.DOKill();
        transform.DOShakeRotation(0.22f, new Vector3(0f, 0f, 10f), 14, 90f, true);
        FlashIceShader(0.75f, 0.22f);
    }

    private void SubscribeToMatchEvent()
    {
        if (subscribedToMatchEvent) return;
        DockManager.OnItemsMatched += HandleDockItemsMatched;
        subscribedToMatchEvent = true;
    }

    private void UnsubscribeFromMatchEvent()
    {
        if (!subscribedToMatchEvent) return;
        DockManager.OnItemsMatched -= HandleDockItemsMatched;
        subscribedToMatchEvent = false;
    }

    private void HandleDockItemsMatched(int matchedId, Vector3 matchPos)
    {
        if (!IsFrozen || !gameObject.activeInHierarchy) return;
        MeltOneLayer();
    }

    private void MeltOneLayer()
    {
        if (remainingFreezeLayers <= 0) return;

        remainingFreezeLayers--;
        PlayIceMeltSound();

        if (remainingFreezeLayers > 0)
        {
            UpdateIceVisual(animate: true);
            FlashIceShader(0.85f, 0.25f);
        }
        else
        {
            UnsubscribeFromMatchEvent();
            iceMeltTween?.Kill();
            FlashIceShader(1f, 0.22f);

            if (iceVisualObject != null)
            {
                GameObject iceObj = iceVisualObject;
                iceVisualObject = null;

                iceMeltTween = DOTween.To(
                    () => currentMeltProgress,
                    val =>
                    {
                        currentMeltProgress = val;
                        ApplyIceShaderProperties();
                    },
                    1f,
                    0.26f
                )
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    if (iceObj != null) Destroy(iceObj);
                });
            }

            transform.DOKill();
            transform.DOPunchScale(Vector3.one * 0.08f, 0.18f, 4, 0.5f);
        }
    }

    private void CreateIceVisual()
    {
        if (iceVisualObject != null) return;

        if (mainRenderer == null)
        {
            mainRenderer = outline != null && outline.MainMeshRenderer != null
                ? outline.MainMeshRenderer
                : GetComponentInChildren<MeshRenderer>();
        }

        Transform parentTransform = mainRenderer != null ? mainRenderer.transform : transform;
        MeshFilter meshFilter = parentTransform.GetComponent<MeshFilter>();

        const float iceScaleMultiplier = 1.08f;

        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Bounds meshBounds = meshFilter.sharedMesh.bounds;
            iceVisualObject = new GameObject("IceOverlay");
            MeshFilter iceMeshFilter = iceVisualObject.AddComponent<MeshFilter>();
            iceMeshFilter.sharedMesh = meshFilter.sharedMesh;
            iceRenderer = iceVisualObject.AddComponent<MeshRenderer>();

            Vector3 centerOffset = meshBounds.center * (1f - iceScaleMultiplier);
            iceVisualObject.transform.SetParent(parentTransform, false);
            iceVisualObject.transform.localPosition = centerOffset;
            iceVisualObject.transform.localRotation = Quaternion.identity;
            iceBaseLocalScale = Vector3.one * iceScaleMultiplier;
            iceVisualObject.transform.localScale = iceBaseLocalScale;

            // Choose the longest axis on cards (length) or Y axis on cubes/dice for top-to-bottom melting
            Vector3 size = meshBounds.size;
            if (Mathf.Abs(size.x - size.y) < 0.05f && Mathf.Abs(size.y - size.z) < 0.05f)
            {
                iceMeltAxis = 1;
            }
            else if (size.z >= size.x && size.z >= size.y)
            {
                iceMeltAxis = 2;
            }
            else if (size.y >= size.x && size.y >= size.z)
            {
                iceMeltAxis = 1;
            }
            else
            {
                iceMeltAxis = 0;
            }

            iceBoundsMin = meshBounds.min[iceMeltAxis];
            iceBoundsMax = meshBounds.max[iceMeltAxis];
        }
        else
        {
            iceVisualObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            iceVisualObject.name = "IceOverlay";
            Collider iceCol = iceVisualObject.GetComponent<Collider>();
            if (iceCol != null) Destroy(iceCol);

            iceVisualObject.transform.SetParent(parentTransform, false);
            iceVisualObject.transform.localPosition = Vector3.zero;
            iceVisualObject.transform.localRotation = Quaternion.identity;
            iceBaseLocalScale = Vector3.one * iceScaleMultiplier;
            iceMeltAxis = 1;
            iceBoundsMin = -0.5f;
            iceBoundsMax = 0.5f;
            iceVisualObject.transform.localScale = iceBaseLocalScale;
            iceRenderer = iceVisualObject.GetComponent<MeshRenderer>();
        }

        if (iceRenderer != null)
        {
            if (iceMaterial != null)
            {
                iceRenderer.sharedMaterial = iceMaterial;
            }
            iceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            iceRenderer.receiveShadows = false;
        }

        currentMeltProgress = 0f;
        currentFlashAmount = 0f;
        ApplyIceShaderProperties();
    }

    private void UpdateIceVisual(bool animate)
    {
        if (iceVisualObject == null || iceRenderer == null) return;

        float ratio = Mathf.Clamp01((float)remainingFreezeLayers / Mathf.Max(1, freezeLayers));
        float targetMelt = 1f - ratio;

        iceMeltTween?.Kill();
        if (animate)
        {
            iceMeltTween = DOTween.To(
                () => currentMeltProgress,
                val =>
                {
                    currentMeltProgress = val;
                    ApplyIceShaderProperties();
                },
                targetMelt,
                0.32f
            ).SetEase(Ease.OutQuad);
        }
        else
        {
            currentMeltProgress = targetMelt;
            ApplyIceShaderProperties();
        }
    }

    private void PlayIceMeltSound()
    {
        if (Time.unscaledTime - lastIceMeltSoundTime < 0.08f) return;
        lastIceMeltSoundTime = Time.unscaledTime;
        SoundManager.PlaySound(SoundType.IceMelt);
    }

    private void FlashIceShader(float peakAmount, float duration)
    {
        if (iceRenderer == null) return;

        iceFlashTween?.Kill();
        currentFlashAmount = peakAmount;
        ApplyIceShaderProperties();

        iceFlashTween = DOTween.To(
            () => currentFlashAmount,
            val =>
            {
                currentFlashAmount = val;
                ApplyIceShaderProperties();
            },
            0f,
            duration
        ).SetEase(Ease.OutQuad);
    }

    private void ApplyIceShaderProperties()
    {
        if (iceRenderer == null) return;

        if (icePropertyBlock == null)
        {
            icePropertyBlock = new MaterialPropertyBlock();
        }

        iceRenderer.GetPropertyBlock(icePropertyBlock);
        icePropertyBlock.SetFloat(MeltAxisProp, iceMeltAxis);
        icePropertyBlock.SetFloat(BoundsMinProp, iceBoundsMin);
        icePropertyBlock.SetFloat(BoundsMaxProp, iceBoundsMax);
        icePropertyBlock.SetFloat(MeltProgressProp, currentMeltProgress);
        icePropertyBlock.SetFloat(FlashAmountProp, currentFlashAmount);
        icePropertyBlock.SetFloat(AlphaMultiplierProp, 1f);
        iceRenderer.SetPropertyBlock(icePropertyBlock);
    }

    #endregion

    #region MegaFiers Velocity Stretch

    /// <summary>
    /// Plays a subtle, tactile jelly squish-pop when the player's finger presses or hovers over this item.
    /// </summary>
    private void PlaySelectSquish()
    {
        if (!Application.isPlaying || isBomb) return;

        EnsureStretchModifiersInitialized();
        if (mainStretch == null) return;

        flightStretchTween?.Kill();
        SetStretchModifiersEnabled(true);
        if (mainStretch != null) mainStretch.gizmoRot = Vector3.zero;
        if (outlineStretch != null) outlineStretch.gizmoRot = Vector3.zero;
        SetStretchAmount(0f);

        Sequence seq = DOTween.Sequence();
        seq.Append(DOTween.To(() => currentStretchAmount, SetStretchAmount, -0.10f, 0.06f).SetEase(Ease.OutQuad));
        seq.Append(DOTween.To(() => currentStretchAmount, SetStretchAmount, 0.05f, 0.07f).SetEase(Ease.OutQuad));
        seq.Append(DOTween.To(() => currentStretchAmount, SetStretchAmount, 0f, 0.09f).SetEase(Ease.OutBack));
        seq.OnComplete(StopAndResetStretch);
        seq.OnKill(StopAndResetStretch);
        flightStretchTween = seq;
    }

    /// <summary>
    /// Plays a subtle horizontal stretch and mini settle squash while sliding between dock slots.
    /// </summary>
    public void PlaySlideStretch(float duration, float delay = 0f)
    {
        if (!Application.isPlaying || isBomb) return;

        EnsureStretchModifiersInitialized();
        if (mainStretch == null) return;

        flightStretchTween?.Kill();
        SetStretchModifiersEnabled(true);
        ApplyStretchDirection(mainStretch, Vector3.right);
        ApplyStretchDirection(outlineStretch, Vector3.right);
        SetStretchAmount(0f);

        float halfDur = Mathf.Max(0.04f, duration * 0.55f);
        float settleDur = Mathf.Max(0.04f, duration - halfDur);

        Sequence seq = DOTween.Sequence();
        if (delay > 0f) seq.AppendInterval(delay);
        seq.Append(DOTween.To(() => currentStretchAmount, SetStretchAmount, 0.12f, halfDur).SetEase(Ease.OutQuad));
        seq.Append(DOTween.To(() => currentStretchAmount, SetStretchAmount, -0.06f, settleDur * 0.6f).SetEase(Ease.InQuad));
        seq.Append(DOTween.To(() => currentStretchAmount, SetStretchAmount, 0f, settleDur * 0.4f).SetEase(Ease.OutBack));
        seq.OnComplete(StopAndResetStretch);
        seq.OnKill(StopAndResetStretch);
        flightStretchTween = seq;
    }

    /// <summary>
    /// Stretches the item horizontally along the dock axis as it is pulled toward the center match slot.
    /// </summary>
    public void PlayMergeStretch(float duration)
    {
        if (!Application.isPlaying || isBomb) return;

        EnsureStretchModifiersInitialized();
        if (mainStretch == null) return;

        flightStretchTween?.Kill();
        SetStretchModifiersEnabled(true);
        ApplyStretchDirection(mainStretch, Vector3.right);
        ApplyStretchDirection(outlineStretch, Vector3.right);
        SetStretchAmount(0f);

        float pullDur = Mathf.Max(0.05f, duration * 0.75f);
        float impactDur = Mathf.Max(0.04f, duration - pullDur + 0.05f);

        Sequence seq = DOTween.Sequence();
        seq.Append(DOTween.To(() => currentStretchAmount, SetStretchAmount, 0.38f, pullDur).SetEase(Ease.OutQuad));
        seq.Append(DOTween.To(() => currentStretchAmount, SetStretchAmount, -0.15f, impactDur).SetEase(Ease.InQuad));
        seq.Append(DOTween.To(() => currentStretchAmount, SetStretchAmount, 0f, 0.06f).SetEase(Ease.OutQuad));
        seq.OnComplete(StopAndResetStretch);
        seq.OnKill(StopAndResetStretch);
        flightStretchTween = seq;
    }

    /// <summary>
    /// Plays a subtle velocity-aligned stretch while flying to the dock and a gentle squash upon landing.
    /// Lazily attaches MegaModifyObject + MegaStretch on first use and disables them once settled for zero idle CPU cost.
    /// </summary>
    public void PlayFlightStretch(float flightDuration, Vector3 targetWorldPos)
    {
        if (!Application.isPlaying || isBomb) return;

        EnsureStretchModifiersInitialized();
        if (mainStretch == null) return;

        flightTargetWorldPos = targetWorldPos;
        flightStretchTween?.Kill();
        SetStretchModifiersEnabled(true);
        SetStretchAmount(0f);
        UpdateStretchDirection();

        float riseDuration = Mathf.Max(0.06f, flightDuration * 0.42f);
        float fallDuration = Mathf.Max(0.06f, flightDuration - riseDuration);

        Sequence seq = DOTween.Sequence();
        seq.Append(DOTween.To(() => currentStretchAmount, SetStretchAmount, 0.13f, riseDuration).SetEase(Ease.OutQuad));
        seq.Append(DOTween.To(() => currentStretchAmount, SetStretchAmount, -0.08f, fallDuration).SetEase(Ease.InQuad));
        seq.Append(DOTween.To(() => currentStretchAmount, SetStretchAmount, 0f, 0.12f).SetEase(Ease.OutBack));

        seq.OnUpdate(UpdateStretchDirection);
        seq.OnComplete(StopAndResetStretch);
        seq.OnKill(StopAndResetStretch);
        flightStretchTween = seq;
    }

    private void EnsureStretchModifiersInitialized()
    {
        if (stretchInitialized) return;
        stretchInitialized = true;

        if (mainRenderer == null)
        {
            mainRenderer = outline != null && outline.MainMeshRenderer != null
                ? outline.MainMeshRenderer
                : GetComponentInChildren<MeshRenderer>();
        }

        if (mainRenderer != null)
        {
            AttachStretchToMeshObject(mainRenderer.gameObject, out mainModObj, out mainStretch);
        }

        if (outline != null && outline.OutlineRenderer != null && outline.OutlineRenderer != mainRenderer)
        {
            AttachStretchToMeshObject(outline.OutlineRenderer.gameObject, out outlineModObj, out outlineStretch);
        }
    }

    private static void AttachStretchToMeshObject(GameObject targetGo, out MegaModifyObject modObj, out MegaStretch stretch)
    {
        modObj = null;
        stretch = null;
        if (targetGo == null) return;

        MeshFilter mf = targetGo.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) return;

        mf.sharedMesh = GetOrCreateSubdividedMesh(mf.sharedMesh);

        stretch = targetGo.GetComponent<MegaStretch>();
        if (stretch == null)
        {
            stretch = targetGo.AddComponent<MegaStretch>();
        }
        stretch.axis = MegaAxis.X;
        stretch.amplify = 0.15f;
        stretch.amount = 0f;

        modObj = targetGo.GetComponent<MegaModifyObject>();
        if (modObj == null)
        {
            modObj = targetGo.AddComponent<MegaModifyObject>();
        }
        modObj.NormalMethod = MegaNormalMethod.None;
        modObj.TangentMethod = MegaTangentMethod.None;
        modObj.InvisibleUpdate = true;
        modObj.recalcbounds = false;
        modObj.recalcCollider = false;
        modObj.MeshChanged();
    }

    private void UpdateStretchDirection()
    {
        Vector3 worldDir = flightTargetWorldPos - transform.position;
        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 0.001f) return;

        worldDir.Normalize();
        ApplyStretchDirection(mainStretch, worldDir);
        ApplyStretchDirection(outlineStretch, worldDir);
    }

    private static void ApplyStretchDirection(MegaStretch stretch, Vector3 worldDir)
    {
        if (stretch == null) return;

        Vector3 localDir = stretch.transform.InverseTransformDirection(worldDir);
        if (localDir.sqrMagnitude < 0.0001f) return;

        stretch.gizmoRot = Quaternion.FromToRotation(Vector3.right, localDir.normalized).eulerAngles;
    }

    private static Mesh GetOrCreateSubdividedMesh(Mesh sourceMesh)
    {
        if (sourceMesh == null) return null;
        if (subdividedMeshCache.TryGetValue(sourceMesh, out Mesh cached) && cached != null)
        {
            return cached;
        }

        Mesh dup = MegaUtils.DupMesh(sourceMesh, "_Sub");
        MeshHelper.Subdivide(dup, 4);
        subdividedMeshCache[sourceMesh] = dup;
        return dup;
    }

    private void SetStretchAmount(float amount)
    {
        currentStretchAmount = amount;
        if (mainStretch != null) mainStretch.amount = amount;
        if (outlineStretch != null) outlineStretch.amount = amount;
    }

    private void SetStretchModifiersEnabled(bool active)
    {
        if (mainModObj != null)
        {
            mainModObj.enabled = active;
            mainModObj.Enabled = active;
        }
        if (outlineModObj != null)
        {
            outlineModObj.enabled = active;
            outlineModObj.Enabled = active;
        }
    }

    private void StopAndResetStretch()
    {
        SetStretchAmount(0f);
        if (mainStretch != null) mainStretch.gizmoRot = Vector3.zero;
        if (outlineStretch != null) outlineStretch.gizmoRot = Vector3.zero;
        if (mainModObj != null) mainModObj.Reset();
        if (outlineModObj != null) outlineModObj.Reset();
        SetStretchModifiersEnabled(false);
    }

    #endregion

    #region Dock Idle Animation

    private Transform GetIdleVisualTransform()
    {
        if (mainRenderer == null) return null;
        Transform t = mainRenderer.transform;
        if (t.parent != null && t.parent != transform)
        {
            return t.parent;
        }
        return t;
    }

    private void CacheVisualIdlePose()
    {
        if (hasCachedVisualPose) return;
        Transform idleTarget = GetIdleVisualTransform();
        if (idleTarget != null)
        {
            initialVisualLocalPos = idleTarget.localPosition;
            initialVisualLocalRot = idleTarget.localRotation;
            initialVisualLocalScale = idleTarget.localScale;
            hasCachedVisualPose = true;
        }
    }

    private void StartDockIdle()
    {
        Transform idleTarget = GetIdleVisualTransform();
        if (idleTarget == null) return;

        CacheVisualIdlePose();
        StopDockIdle();

        float bobDelay = UnityEngine.Random.Range(0f, 0.3f);
        float bobDuration = UnityEngine.Random.Range(1.15f, 1.4f);
        float swayDelay = UnityEngine.Random.Range(0f, 0.3f);
        float swayDuration = UnityEngine.Random.Range(1.3f, 1.6f);
        float swaySign = UnityEngine.Random.value > 0.5f ? 1f : -1f;

        Vector3 bobTarget = initialVisualLocalPos + new Vector3(0f, 0.03f, 0.025f);
        dockIdleBobTween = idleTarget.DOLocalMove(bobTarget, bobDuration)
            .SetDelay(bobDelay)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);

        Vector3 baseEuler = initialVisualLocalRot.eulerAngles;
        dockIdleSwayTween = idleTarget.DOLocalRotate(baseEuler + new Vector3(0f, 3.5f * swaySign, 0f), swayDuration)
            .SetDelay(swayDelay)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    /// <summary>
    /// Gently pulses the item's visual mesh when 2 of the same type are waiting in the dock (2/3 ready).
    /// </summary>
    public void SetDockPairPulse(bool active)
    {
        if (isBomb || currentState != ItemState.InDock)
        {
            active = false;
        }

        if (isPairPulseActive == active) return;
        isPairPulseActive = active;

        dockPairPulseTween?.Kill();
        dockPairPulseTween = null;

        Transform idleTarget = GetIdleVisualTransform();
        if (idleTarget == null) return;

        CacheVisualIdlePose();

        if (active)
        {
            idleTarget.localScale = initialVisualLocalScale;
            dockPairPulseTween = idleTarget.DOScale(initialVisualLocalScale * 1.055f, 0.55f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }
        else
        {
            dockPairPulseTween = idleTarget.DOScale(initialVisualLocalScale, 0.14f).SetEase(Ease.OutQuad);
        }
    }

    private void StopDockIdle()
    {
        dockIdleBobTween?.Kill();
        dockIdleSwayTween?.Kill();
        dockPairPulseTween?.Kill();
        dockIdleBobTween = null;
        dockIdleSwayTween = null;
        dockPairPulseTween = null;
        isPairPulseActive = false;

        if (!hasCachedVisualPose) return;
        Transform idleTarget = GetIdleVisualTransform();
        if (idleTarget != null)
        {
            idleTarget.localPosition = initialVisualLocalPos;
            idleTarget.localRotation = initialVisualLocalRot;
            idleTarget.localScale = initialVisualLocalScale;
        }
    }

    #endregion

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (isMystery && mysteryMaterial == null)
        {
            bool isDice = id >= 300 ||
                          (!string.IsNullOrEmpty(itemKey) && itemKey.IndexOf("Dice", StringComparison.OrdinalIgnoreCase) >= 0) ||
                          gameObject.name.IndexOf("Dice", StringComparison.OrdinalIgnoreCase) >= 0;

            string matPath = isDice
                ? "Assets/_Game/Materials/Dice_mystery_mat.mat"
                : "Assets/_Game/Materials/Card_mystery_mat.mat";

            mysteryMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        }

        if (isFrozen && iceMaterial == null)
        {
            iceMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/M_IceOverlay.mat");
        }
    }
#endif
}
