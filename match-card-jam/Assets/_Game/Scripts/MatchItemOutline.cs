using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Manages outline visuals and click feedback animations for MatchItem.
/// The Outline GameObject exists directly in the prefab hierarchy.
/// Lifecycle is controlled by MatchItem.
/// </summary>
public class MatchItemOutline : MonoBehaviour
{
    [Header("Mesh & Renderers")]
    [Tooltip("Main MeshRenderer of the card. If unassigned, searched in this object or children.")]
    [SerializeField] private MeshRenderer mainMeshRenderer;

    [Tooltip("MeshRenderer on Outline GameObject in hierarchy. If null, searched as child named 'OutlineMesh'.")]
    [SerializeField] private MeshRenderer outlineRenderer;

    [Header("Colors & Animation")]
    [SerializeField] private Color normalColor = Color.black;
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private float feedbackDuration = 0.18f;

    private MaterialPropertyBlock propertyBlock;
    private static readonly int OutlineColorProp = Shader.PropertyToID("_OutlineColor");
    private Coroutine feedbackCoroutine;
    private bool isInitialized;

    public MeshRenderer MainMeshRenderer => mainMeshRenderer;
    public MeshRenderer OutlineRenderer => outlineRenderer;

    /// <summary>
    /// Called by MatchItem.
    /// </summary>
    public void Initialize(MeshRenderer mainRenderer = null)
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        if (mainRenderer != null)
        {
            mainMeshRenderer = mainRenderer;
        }
        else if (mainMeshRenderer == null)
        {
            mainMeshRenderer = GetComponentInChildren<MeshRenderer>();
        }

        BindOutlineRenderer();
        SetOutlineColor(normalColor);
        isInitialized = true;
    }

    /// <summary>
    /// Locates and binds the existing OutlineMesh renderer in the hierarchy.
    /// </summary>
    public void BindOutlineRenderer()
    {
        if (outlineRenderer != null) return;

        // 1. Search directly in child objects for 'OutlineMesh'
        Transform outlineTransform = transform.Find("OutlineMesh");
        if (outlineTransform == null && mainMeshRenderer != null)
        {
            outlineTransform = mainMeshRenderer.transform.Find("OutlineMesh");
        }

        if (outlineTransform != null)
        {
            outlineRenderer = outlineTransform.GetComponent<MeshRenderer>();
            return;
        }

        // 2. If not found by name, pick the secondary MeshRenderer among children as outline
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        foreach (var r in renderers)
        {
            if (r != mainMeshRenderer)
            {
                outlineRenderer = r;
                break;
            }
        }
    }

    private void EnsurePropertyBlock()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    public void SetHighlighted(bool highlighted)
    {
        if (!isInitialized) Initialize();

        if (feedbackCoroutine != null)
        {
            StopCoroutine(feedbackCoroutine);
            feedbackCoroutine = null;
        }

        SetOutlineColor(highlighted ? selectedColor : normalColor);
    }

    public void PlayClickFeedback(Action onComplete = null)
    {
        if (!isInitialized) Initialize();

        if (feedbackCoroutine != null)
        {
            StopCoroutine(feedbackCoroutine);
        }
        feedbackCoroutine = StartCoroutine(ClickFeedbackRoutine(onComplete));
    }

    private IEnumerator ClickFeedbackRoutine(Action onComplete)
    {
        SetOutlineColor(selectedColor);

        float elapsed = 0f;
        while (elapsed < feedbackDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / feedbackDuration);
            Color c = Color.Lerp(selectedColor, normalColor, t);
            SetOutlineColor(c);
            yield return null;
        }

        SetOutlineColor(normalColor);
        feedbackCoroutine = null;
        onComplete?.Invoke();
    }

    public void SetOutlineColor(Color color)
    {
        EnsurePropertyBlock();

        MeshRenderer target = outlineRenderer != null ? outlineRenderer : mainMeshRenderer;
        if (target == null) return;

        target.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(OutlineColorProp, color);
        target.SetPropertyBlock(propertyBlock);
    }

    public void SetOutlineVisible(bool visible)
    {
        if (outlineRenderer != null)
        {
            outlineRenderer.enabled = visible;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            BindOutlineRenderer();
            if (outlineRenderer != null)
            {
                SetOutlineColor(normalColor);
            }
        }
    }
#endif
}
