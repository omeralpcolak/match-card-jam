using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level defeat popup dialog (LevelFailPopup).
/// Plays a choreographed fail sequence:
/// 1. Backdrop fades in and FailGlow activates.
/// 2. LevelFailText fades in, then transitions its color to red.
/// 3. After a brief delay, HomeButton and RestartButton scale up with Ease.OutBack.
/// </summary>
public class LevelFailPopup : PopupBase<LevelFailPopup>
{
    [Header("Visual Effects")]
    [Tooltip("Background fail glow effect GameObject.")]
    [SerializeField] private GameObject failGlow;

    [Header("Title")]
    [Tooltip("Level fail title text component.")]
    [SerializeField] private TextMeshProUGUI levelFailText;

    [Tooltip("Initial color of the text before turning red (usually white).")]
    [SerializeField] private Color initialTextColor = Color.white;

    [Tooltip("Target red color applied after fading in.")]
    [SerializeField] private Color failRedColor = new Color(0.95f, 0.22f, 0.22f, 1f);

    [Tooltip("Duration of the text fade-in animation.")]
    [SerializeField] private float textFadeInDuration = 0.4f;

    [Tooltip("Duration of the color transition to red.")]
    [SerializeField] private float colorChangeDuration = 0.35f;

    [Header("Buttons")]
    [Tooltip("Home button returning to the Main Menu.")]
    [SerializeField] private Button homeButton;

    [Tooltip("Restart button to reload the current level.")]
    [SerializeField] private Button restartButton;

    [Tooltip("Delay before buttons begin scaling up.")]
    [SerializeField] private float buttonDelay = 0.2f;

    [Tooltip("Duration of the button scale animation.")]
    [SerializeField] private float buttonScaleDuration = 0.35f;

    [Tooltip("Ease curve for the button scale animation.")]
    [SerializeField] private Ease buttonScaleEase = Ease.OutBack;

    [Header("Scene Navigation")]
    [Tooltip("Target scene name to load when HomeButton is clicked.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    protected override void Awake()
    {
        base.Awake();

        CacheReferences();
        SetupButtonListeners();
        PrepareInitialVisualState();
    }

    private void SetupButtonListeners()
    {
        if (homeButton != null)
        {
            homeButton.onClick.AddListener(OnHomeClicked);
        }

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(OnRestartClicked);
        }
    }

    /// <summary>
    /// Prepares elements in their hidden / zero-scale initial state before animation.
    /// </summary>
    private void PrepareInitialVisualState()
    {
        if (failGlow != null)
        {
            failGlow.SetActive(false);
        }

        if (levelFailText != null)
        {
            // Start fully transparent with initial color
            levelFailText.color = new Color(initialTextColor.r, initialTextColor.g, initialTextColor.b, 0f);
        }

        if (homeButton != null)
        {
            homeButton.transform.localScale = Vector3.zero;
        }

        if (restartButton != null)
        {
            restartButton.transform.localScale = Vector3.zero;
        }
    }

    /// <summary>
    /// Executes the choreographed fail animation sequence.
    /// </summary>
    protected override void AnimateOpen(Action onComplete)
    {
        PrepareInitialVisualState();

        Sequence seq = DOTween.Sequence().SetUpdate(true);

        // --- STEP 1: Backdrop fades in, then FailGlow activates ---
        if (backdropCanvasGroup != null)
        {
            backdropCanvasGroup.alpha = 0f;
            seq.Append(backdropCanvasGroup.DOFade(1f, 0.25f));
        }
        else
        {
            seq.AppendInterval(0.2f);
        }

        seq.AppendCallback(() =>
        {
            if (failGlow != null)
            {
                failGlow.SetActive(true);
            }
        });

        // --- STEP 2: LevelFailText fades in, then turns red ---
        if (levelFailText != null)
        {
            // Fade in from alpha 0 to 1
            seq.Append(levelFailText.DOFade(1f, textFadeInDuration));

            // After fade-in completes, animate color to red
            seq.Append(levelFailText.DOColor(failRedColor, colorChangeDuration));
        }

        // --- STEP 3: Small delay, then HomeButton and RestartButton scale up with Ease.OutBack ---
        seq.AppendInterval(buttonDelay);

        if (homeButton != null && restartButton != null)
        {
            seq.Append(homeButton.transform.DOScale(Vector3.one, buttonScaleDuration).SetEase(buttonScaleEase));
            seq.Join(restartButton.transform.DOScale(Vector3.one, buttonScaleDuration).SetEase(buttonScaleEase));
        }
        else if (homeButton != null)
        {
            seq.Append(homeButton.transform.DOScale(Vector3.one, buttonScaleDuration).SetEase(buttonScaleEase));
        }
        else if (restartButton != null)
        {
            seq.Append(restartButton.transform.DOScale(Vector3.one, buttonScaleDuration).SetEase(buttonScaleEase));
        }

        seq.OnComplete(() => onComplete?.Invoke());
        activeAnimationTween = seq;
    }

    private void OnHomeClicked()
    {
        if (homeButton != null) homeButton.interactable = false;

        if (Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            SceneTransition.LoadScene(mainMenuSceneName);
        }
        else
        {
            Debug.LogWarning($"[LevelFailPopup] Scene '{mainMenuSceneName}' is not in Build Settings! Reloading current scene as fallback.");
            SceneTransition.ReloadCurrentScene();
        }
    }

    private void OnRestartClicked()
    {
        if (restartButton != null)
        {
            restartButton.transform.DOKill();
            restartButton.transform.localScale = Vector3.one;
            restartButton.transform.DOPunchScale(Vector3.one * 0.1f, 0.2f, 1, 0.5f);
        }

        // Can 0 ise OutOfLives popupı çıkmalı, şimdilik restart engellenir
        if (!LifeManager.HasLives)
        {
            Debug.LogWarning("<color=#e74c3c><b>[LevelFailPopup]</b> Can 0! OutOfLives popupı açılacak (restart engellendi).</color>");
            if (restartButton != null)
            {
                restartButton.transform.DOShakePosition(0.35f, 10f, 14, 90f, false, true);
            }
            return;
        }

        if (restartButton != null) restartButton.interactable = false;

        // Seviye yeniden başlatıldığı için 1 can eksilir
        LifeManager.TryConsumeLife();

        SceneTransition.ReloadCurrentScene();
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
            }
        }

        // Fail Glow (supports GlowEffect or FailGlow)
        if (failGlow == null)
        {
            Transform glow = transform.Find("FailGlow") ?? transform.Find("GlowEffect");
            if (glow != null)
            {
                failGlow = glow.gameObject;
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

            if (levelFailText == null)
            {
                Transform title = inside.Find("LevelFailText");
                if (title != null)
                {
                    levelFailText = title.GetComponent<TextMeshProUGUI>();
                }
            }

            if (homeButton == null)
            {
                Transform home = inside.Find("HomeButton");
                if (home != null)
                {
                    homeButton = home.GetComponent<Button>();
                }
            }

            if (restartButton == null)
            {
                Transform restart = inside.Find("RestartButton");
                if (restart != null)
                {
                    restartButton = restart.GetComponent<Button>();
                }
            }
        }
    }

    private void Reset()
    {
        CacheReferences();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (levelFailText != null)
        {
            levelFailText.DOKill();
        }

        if (homeButton != null)
        {
            homeButton.transform.DOKill();
        }

        if (restartButton != null)
        {
            restartButton.transform.DOKill();
        }
    }
}
