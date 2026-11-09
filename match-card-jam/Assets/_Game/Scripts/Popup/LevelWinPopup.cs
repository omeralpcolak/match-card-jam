using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level victory popup dialog (LevelWinPopup).
/// Plays a choreographed 5-step victory sequence (Backdrop, Glow, Title bounce, Sparkles, Coin count-up, Buttons).
/// </summary>
public class LevelWinPopup : PopupBase<LevelWinPopup>
{
    [Header("Visual Effects")]
    [Tooltip("Background glow particle or image effect.")]
    [SerializeField] private GameObject glowEffect;

    [Tooltip("Sparkle particle effect playing on the victory text.")]
    [SerializeField] private GameObject completeSparkleEffect;

    [Header("Title & Panels")]
    [Tooltip("Level complete title text.")]
    [SerializeField] private RectTransform levelCompleteText;

    [Tooltip("Reward panel container.")]
    [SerializeField] private RectTransform rewardPanel;

    [Tooltip("Reward coin icon to shake/punch scale during count-up.")]
    [SerializeField] private RectTransform coinImg;

    [Tooltip("Coin reward text component.")]
    [SerializeField] private TextMeshProUGUI rewardCoinText;

    [Header("Buttons")]
    [Tooltip("Claim 2x reward button (e.g. for rewarded ads).")]
    [SerializeField] private Button reward2xButton;

    [Tooltip("Continue button to return to the Main Menu.")]
    [SerializeField] private Button continueButton;

    [Header("Settings")]
    [Tooltip("Base coin reward amount.")]
    [SerializeField] private int baseRewardCoin = 100;

    [Tooltip("Target scene name to load on continue.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private int earnedCoins = 100;
    private int displayedCoins = 0;
    private Tween coinCountTween;
    private Tween coinPunchTween;

    protected override void Awake()
    {
        base.Awake();

        CacheReferences();

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        if (reward2xButton != null)
        {
            reward2xButton.onClick.AddListener(OnReward2xClicked);
        }

        PrepareInitialVisualState();
    }

    /// <summary>
    /// Prepares elements in their hidden / zero-scale states before the sequence begins.
    /// </summary>
    private void PrepareInitialVisualState()
    {
        if (glowEffect != null) glowEffect.SetActive(false);
        if (completeSparkleEffect != null) completeSparkleEffect.SetActive(false);

        if (levelCompleteText != null) levelCompleteText.localScale = Vector3.zero;
        if (rewardPanel != null) rewardPanel.localScale = Vector3.zero;
        if (reward2xButton != null) reward2xButton.transform.localScale = Vector3.zero;
        if (continueButton != null) continueButton.transform.localScale = Vector3.zero;

        if (rewardCoinText != null) rewardCoinText.text = "+0";
    }

    /// <summary>
    /// Executes the custom 5-step victory animation sequence.
    /// </summary>
    protected override void AnimateOpen(Action onComplete)
    {
        PrepareInitialVisualState();
        SoundManager.PlaySound(SoundType.LevelComplete);
        SoundManager.DuckMusic(0.2f, 0.4f);

        Sequence seq = DOTween.Sequence().SetUpdate(true);

        // --- STEP 1: Backdrop fades in, then glowEffect activates ---
        if (backdropCanvasGroup != null)
        {
            backdropCanvasGroup.alpha = 0f;
            seq.Append(backdropCanvasGroup.DOFade(1f, 0.25f));
        }
        else
        {
            seq.AppendInterval(0.25f);
        }

        seq.AppendCallback(() =>
        {
            if (glowEffect != null) glowEffect.SetActive(true);
        });

        // --- STEP 2: LevelCompleteText scales up with Ease.OutBack, sparkles activate on finish ---
        if (levelCompleteText != null)
        {
            seq.Append(levelCompleteText.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack));
        }

        seq.AppendCallback(() =>
        {
            if (completeSparkleEffect != null) completeSparkleEffect.SetActive(true);
        });

        // --- STEP 3: Reward panel pops in, coin counts up to 100 with coin icon punch scale ---
        if (rewardPanel != null)
        {
            seq.Append(rewardPanel.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack));
        }

        seq.AppendCallback(() =>
        {
            // Count up coins from 0 to baseRewardCoin
            displayedCoins = 0;
            coinCountTween?.Kill();
            coinCountTween = DOTween.To(() => displayedCoins, x =>
            {
                displayedCoins = x;
                if (rewardCoinText != null) rewardCoinText.text = $"+{x}";
            }, baseRewardCoin, 0.6f).SetUpdate(true);

            // Coin icon punch/shake scale
            if (coinImg != null)
            {
                coinPunchTween?.Kill();
                coinImg.localScale = Vector3.one;
                coinPunchTween = coinImg.DOPunchScale(Vector3.one * 0.25f, 0.6f, 4, 0.5f).SetUpdate(true);
            }
        });

        seq.AppendInterval(0.65f); // Wait for coin count-up

        // --- STEP 4: Reward2xButton scales up with Ease.OutBack ---
        if (reward2xButton != null)
        {
            seq.Append(reward2xButton.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack));
        }

        // --- STEP 5: Small delay, then ContinueButton scales up with Ease.OutBack ---
        seq.AppendInterval(0.12f);

        if (continueButton != null)
        {
            seq.Append(continueButton.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack));
        }

        seq.OnComplete(() => onComplete?.Invoke());
        activeAnimationTween = seq;
    }

    private void OnContinueClicked()
    {
        SoundManager.PlayButtonSound();

        if (continueButton != null) continueButton.interactable = false;

        // Earned coins are awarded to the player wallet
        GameData.AddCoins(earnedCoins);

        if (Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            SceneTransition.LoadScene(mainMenuSceneName);
        }
        else
        {
            Debug.LogWarning($"[LevelWinPopup] Scene '{mainMenuSceneName}' is not in Build Settings! Reloading current scene as fallback.");
            SceneTransition.ReloadCurrentScene();
        }
    }

    private void OnReward2xClicked()
    {
        SoundManager.PlayButtonSound();

        if (reward2xButton != null)
        {
            reward2xButton.interactable = false;
            reward2xButton.transform.DOScale(Vector3.one * 0.95f, 0.2f).SetUpdate(true);
        }

        int targetReward = baseRewardCoin * 2;
        earnedCoins = targetReward;
        int startValue = displayedCoins;

        // Animate coin reward text counting up from +100 to +200
        coinCountTween?.Kill();
        coinCountTween = DOTween.To(() => startValue, x =>
        {
            startValue = x;
            displayedCoins = x;
            if (rewardCoinText != null)
            {
                rewardCoinText.text = $"+{x}";
            }
        }, targetReward, 0.45f).SetUpdate(true);

        // Punch scale coin icon to celebrate the doubled reward
        if (coinImg != null)
        {
            coinPunchTween?.Kill();
            coinImg.localScale = Vector3.one;
            coinPunchTween = coinImg.DOPunchScale(Vector3.one * 0.35f, 0.45f, 3, 0.5f).SetUpdate(true);
        }

        Debug.Log($"<color=#f1c40f><b>[LevelWinPopup]</b> 2x Reward claimed! Total: {targetReward}</color>");
    }

    protected override void OnClosed()
    {
        base.OnClosed();
        SoundManager.UnduckMusic(0.8f);
    }

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

        // Glow Effect
        if (glowEffect == null)
        {
            Transform glow = transform.Find("GlowEffect");
            if (glow != null) glowEffect = glow.gameObject;
        }

        // Inside elements
        Transform inside = transform.Find("inside");
        if (inside != null)
        {
            if (popupContent == null) popupContent = inside as RectTransform;

            if (levelCompleteText == null)
            {
                Transform title = inside.Find("LevelCompleteText");
                if (title != null) levelCompleteText = title as RectTransform;
            }

            if (completeSparkleEffect == null && levelCompleteText != null)
            {
                Transform sparkle = levelCompleteText.Find("CompleteSparkleEffect");
                if (sparkle != null) completeSparkleEffect = sparkle.gameObject;
            }

            if (rewardPanel == null)
            {
                Transform rp = inside.Find("RewardPanel");
                if (rp != null)
                {
                    rewardPanel = rp as RectTransform;
                    if (coinImg == null)
                    {
                        Transform ci = rp.Find("CoinImg");
                        if (ci != null) coinImg = ci as RectTransform;
                    }
                    if (rewardCoinText == null)
                    {
                        rewardCoinText = rp.GetComponentInChildren<TextMeshProUGUI>(true);
                    }
                }
            }

            if (reward2xButton == null)
            {
                Transform r2x = inside.Find("Reward2xButton");
                if (r2x != null)
                {
                    reward2xButton = r2x.GetComponent<Button>();
                }
            }

            if (continueButton == null)
            {
                Transform cont = inside.Find("ContinueButton");
                if (cont != null) continueButton = cont.GetComponent<Button>();
            }
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        coinCountTween?.Kill();
        coinPunchTween?.Kill();
    }
}
