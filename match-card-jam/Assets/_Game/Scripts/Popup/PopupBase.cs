using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Generic abstract base class for all UI popups.
/// Manages popup lifecycle, autonomous instantiation from Resources,
/// and provides customizable virtual animation hooks (AnimateOpen, AnimateClose).
/// </summary>
public abstract class PopupBase<T> : MonoBehaviour where T : PopupBase<T>
{
    protected static T currentInstance;
    public static T Current => currentInstance;
    public static bool IsOpen => currentInstance != null && currentInstance.isPopupOpen;

    [Header("Default Visual References (Optional)")]
    [Tooltip("Optional CanvasGroup used for backdrop dimming.")]
    [SerializeField] protected CanvasGroup backdropCanvasGroup;

    [Tooltip("Optional main content RectTransform to scale/animate.")]
    [SerializeField] protected RectTransform popupContent;

    [Header("Default Animation Settings")]
    [Tooltip("Duration of the default opening animation.")]
    [SerializeField] protected float defaultOpenDuration = 0.3f;

    [Tooltip("Duration of the default closing animation.")]
    [SerializeField] protected float defaultCloseDuration = 0.2f;

    [Tooltip("Easing curve for the default opening animation.")]
    [SerializeField] protected Ease defaultOpenEase = Ease.OutBack;

    [Tooltip("Easing curve for the default closing animation.")]
    [SerializeField] protected Ease defaultCloseEase = Ease.InBack;

    protected Action onClosedCallback;
    protected Tween activeAnimationTween;
    protected bool isPopupOpen;
    protected bool isClosing;

    /// <summary>
    /// Loads the popup prefab from 'Resources/Popups/{T}', instantiates it, and triggers its open sequence.
    /// E.g.: SettingsPopup.Show();
    /// </summary>
    public static T Show(Action onClosed = null)
    {
        if (currentInstance != null)
        {
            currentInstance.transform.SetAsLastSibling();
            return currentInstance;
        }

        string popupName = typeof(T).Name;
        string resourcePath = $"Popups/{popupName}";
        GameObject prefab = Resources.Load<GameObject>(resourcePath);

        if (prefab == null)
        {
            Debug.LogError($"[PopupBase] Popup prefab not found at '{resourcePath}'! Ensure it exists in 'Assets/_Game/Resources/Popups/{popupName}'.");
            return null;
        }

        GameObject instanceObj = Instantiate(prefab);
        currentInstance = instanceObj.GetComponent<T>();

        if (currentInstance == null)
        {
            Debug.LogError($"[PopupBase] Component '{popupName}' missing on prefab '{popupName}'!");
            Destroy(instanceObj);
            return null;
        }

        // Configure Canvas sorting if present to ensure it displays on top
        if (instanceObj.TryGetComponent(out Canvas canvas))
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
        }

        currentInstance.onClosedCallback = onClosed;
        currentInstance.Open();
        return currentInstance;
    }

    public static void Hide()
    {
        if (currentInstance != null)
        {
            currentInstance.Close();
        }
    }

    protected virtual void Awake()
    {
        // Subclasses can setup their specific buttons and bindings here
    }

    /// <summary>
    /// Opens the popup and plays the open animation.
    /// </summary>
    public virtual void Open()
    {
        if (isPopupOpen) return;

        isPopupOpen = true;
        isClosing = false;

        KillActiveTweens();
        OnOpening();

        AnimateOpen(() =>
        {
            OnOpened();
        });
    }

    /// <summary>
    /// Closes the popup with its close animation, then destroys the GameObject.
    /// </summary>
    public virtual void Close()
    {
        if (isClosing || !isPopupOpen) return;

        isClosing = true;
        KillActiveTweens();
        OnClosing();

        AnimateClose(() =>
        {
            CompleteClose();
        });
    }

    /// <summary>
    /// Instantly closes the popup, cancels all active tweens, and destroys the GameObject immediately.
    /// Use this for scene changes, restarts, or when no closing animation should play.
    /// </summary>
    public virtual void CloseImmediate()
    {
        KillActiveTweens();
        CompleteClose();
    }

    private void CompleteClose()
    {
        isPopupOpen = false;
        isClosing = false;

        OnClosed();
        onClosedCallback?.Invoke();

        if (currentInstance == this)
        {
            currentInstance = null;
        }

        Destroy(gameObject);
    }

    #region Virtual Animation Hooks (Subclasses can override these)

    /// <summary>
    /// Executes the open animation sequence. Override this in child classes (e.g. WinPopup)
    /// to provide unique animations (banners, stars, particles, etc.).
    /// Always invoke onComplete when the animation finishes.
    /// </summary>
    protected virtual void AnimateOpen(Action onComplete)
    {
        Sequence seq = DOTween.Sequence().SetUpdate(true);

        if (backdropCanvasGroup != null)
        {
            backdropCanvasGroup.alpha = 0f;
            seq.Join(backdropCanvasGroup.DOFade(1f, defaultOpenDuration));
        }

        if (popupContent != null)
        {
            popupContent.localScale = Vector3.zero;
            seq.Join(popupContent.DOScale(Vector3.one, defaultOpenDuration).SetEase(defaultOpenEase));
        }

        seq.OnComplete(() => onComplete?.Invoke());
        activeAnimationTween = seq;
    }

    /// <summary>
    /// Executes the close animation sequence. Override this in child classes for custom exit animations.
    /// Always invoke onComplete when the animation finishes.
    /// </summary>
    protected virtual void AnimateClose(Action onComplete)
    {
        Sequence seq = DOTween.Sequence().SetUpdate(true);

        if (backdropCanvasGroup != null)
        {
            seq.Join(backdropCanvasGroup.DOFade(0f, defaultCloseDuration));
        }

        if (popupContent != null)
        {
            seq.Join(popupContent.DOScale(Vector3.zero, defaultCloseDuration).SetEase(defaultCloseEase));
        }

        seq.OnComplete(() => onComplete?.Invoke());
        activeAnimationTween = seq;
    }

    #endregion

    #region Lifecycle Callbacks (Optional hooks)

    protected virtual void OnOpening() { }
    protected virtual void OnOpened() { }
    protected virtual void OnClosing() { }
    protected virtual void OnClosed() { }

    #endregion

    protected virtual void OnDestroy()
    {
        KillActiveTweens();

        if (currentInstance == this)
        {
            currentInstance = null;
        }
    }

    protected void KillActiveTweens()
    {
        activeAnimationTween?.Kill();

        if (backdropCanvasGroup != null)
        {
            backdropCanvasGroup.DOKill();
        }

        if (popupContent != null)
        {
            popupContent.DOKill();
        }

        transform.DOKill();
        DOTween.Kill(this);
    }
}
