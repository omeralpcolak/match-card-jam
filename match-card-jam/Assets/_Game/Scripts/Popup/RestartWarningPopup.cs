using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Confirmation popup shown when the player clicks the HUD Restart button during gameplay.
/// - Pauses gameplay (timer and card input) while open.
/// - 'No' closes the popup and resumes gameplay.
/// - 'Yes' consumes 1 life and reloads the current scene via SceneTransition.
/// NOTE: This popup is NOT shown when restarting from LevelFailPopup.
/// </summary>
public class RestartWarningPopup : PopupBase<RestartWarningPopup>
{
    public static int CurrentLives => LifeManager.CurrentLives;

    [Header("Buttons")]
    [Tooltip("Yes button confirming restart (consumes 1 life).")]
    [SerializeField] private Button yesButton;

    [Tooltip("No button cancelling restart and returning to gameplay.")]
    [SerializeField] private Button noButton;

    [Tooltip("Optional backdrop button to dismiss popup on outside click.")]
    [SerializeField] private Button backdropButton;

    [Tooltip("Whether tapping the dark backdrop closes the popup.")]
    [SerializeField] private bool closeOnBackdropClick = true;

    private bool isRestarting;

    protected override void Awake()
    {
        base.Awake();

        CacheReferences();
        SetupButtonListeners();
    }

    private void SetupButtonListeners()
    {
        if (yesButton != null)
        {
            yesButton.onClick.AddListener(OnYesClicked);
        }

        if (noButton != null)
        {
            noButton.onClick.AddListener(OnNoClicked);
        }

        if (backdropButton != null && closeOnBackdropClick)
        {
            backdropButton.onClick.AddListener(OnNoClicked);
        }
    }

    protected override void OnOpening()
    {
        base.OnOpening();
        isRestarting = false;

        // Pause gameplay (timer and input) while the warning popup is open
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.SetPaused(true);
        }
    }

    protected override void OnClosed()
    {
        base.OnClosed();

        // Resume gameplay if player chose 'No' and the level is continuing
        if (!isRestarting && LevelManager.Instance != null)
        {
            LevelManager.Instance.SetPaused(false);
        }
    }

    private void OnYesClicked()
    {
        if (isRestarting) return;

        // Can 0 kontrolü:
        if (!LifeManager.HasLives)
        {
            Debug.LogWarning("<color=#e74c3c><b>[RestartWarningPopup]</b> Can 0! OutOfLives popupı açılacak (restart engellendi).</color>");
            if (yesButton != null)
            {
                yesButton.transform.DOShakePosition(0.35f, 10f, 14, 90f, false, true);
            }
            return;
        }

        isRestarting = true;

        // Disable buttons to prevent spam
        if (yesButton != null) yesButton.interactable = false;
        if (noButton != null) noButton.interactable = false;
        if (backdropButton != null) backdropButton.interactable = false;

        // 1. 1 Can eksilt
        LifeManager.TryConsumeLife();

        // 2. Reload scene with smooth fade to black (destruction left to OnDestroy during scene unload)
        SceneTransition.ReloadCurrentScene();
    }

    private void OnNoClicked()
    {
        if (isRestarting) return;
        Close();
    }

    /// <summary>
    /// Automatically resolves and caches child references if they are not wired in the inspector.
    /// </summary>
    private void CacheReferences()
    {
        // Backdrop
        if (backdropCanvasGroup == null)
        {
            Transform bg = transform.Find("Bg");
            if (bg != null)
            {
                backdropCanvasGroup = bg.GetComponent<CanvasGroup>();
                if (backdropCanvasGroup == null)
                {
                    backdropCanvasGroup = bg.gameObject.AddComponent<CanvasGroup>();
                }

                if (backdropButton == null)
                {
                    backdropButton = bg.GetComponent<Button>();
                }
            }
        }

        // Inside elements
        Transform inside = transform.Find("inside");
        if (inside != null)
        {
            if (popupContent == null)
            {
                popupContent = inside as RectTransform;
            }

            if (yesButton == null)
            {
                Transform yes = inside.Find("YesButton");
                if (yes != null) yesButton = yes.GetComponent<Button>();
            }

            if (noButton == null)
            {
                Transform no = inside.Find("NoButton");
                if (no != null) noButton = no.GetComponent<Button>();
            }
        }
    }

    private void Reset()
    {
        CacheReferences();
    }
}
