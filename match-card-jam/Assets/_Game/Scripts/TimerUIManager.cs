using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Seviye süre barını (TimerBar) ve süre metnini (TimeTxt) yöneten UI sınıfı.
/// Seviyede süre yoksa tüm barı kapatır (SetActive(false)), süre varsa 00:30 formatında günceller.
/// Süre belirlenen eşiğin (varsayılan 10 saniye) altına düştüğünde uyarı rengine geçer ve kalp atışı efekti oynatır.
/// </summary>
public class TimerUIManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Açılıp kapatılacak ana bar objesi (Boşsa bu scriptin takılı olduğu GameObject kullanılır).")]
    [SerializeField] private GameObject timerBarRoot;

    [Tooltip("Sürenin yazılacağı TextMeshProUGUI bileşeni.")]
    [SerializeField] private TextMeshProUGUI timeTxt;

    [Header("Format Settings")]
    [Tooltip("Süre gösterim formatı (Örn: {0:00}:{1:00} -> 00:30).")]
    [SerializeField] private string timeFormat = "{0:00}:{1:00}";

    [Header("Warning Effect (Under Threshold)")]
    [Tooltip("Efektin devreye gireceği kalan saniye eşiği.")]
    [SerializeField] private float warningThreshold = 10f;

    [Tooltip("Uyarı anında metnin alacağı renk (Kırmızı).")]
    [SerializeField] private Color warningColor = new Color(0.96f, 0.26f, 0.21f, 1f);

    [Tooltip("Her saniyede kalp atışı / zıplama efekti olsun mu?")]
    [SerializeField] private bool usePulseEffect = true;

    [Tooltip("Zıplama büyüklüğü.")]
    [SerializeField] private float pulseScale = 0.25f;

    [Tooltip("Zıplama süresi.")]
    [SerializeField] private float pulseDuration = 0.3f;

    private int lastDisplayedSeconds = -1;
    private Color originalColor;
    private Vector3 originalScale;
    private Tween pulseTween;
    private Tween colorTween;
    private bool isWarningActive;

    private void Awake()
    {
        CacheComponents();
    }

    private void OnDisable()
    {
        ResetVisuals();
    }

    /// <summary>
    /// LevelManager tarafından seviye başlarken çağrılır.
    /// Süre yoksa barı gizler, süre varsa açıp başlangıç değerini yazar.
    /// </summary>
    public void Initialize(bool useTimer, float initialTime)
    {
        CacheComponents();
        ResetVisuals();

        if (!useTimer)
        {
            Hide();
            return;
        }

        Show();
        lastDisplayedSeconds = -1;
        UpdateTimer(initialTime);
    }

    /// <summary>
    /// Süre metnini dakika:saniye (00:30) olarak günceller.
    /// Süre 10 saniyenin altına indiğinde uyarı rengine döner ve her saniye kalp atışı gibi atar.
    /// </summary>
    public void UpdateTimer(float remainingSeconds)
    {
        if (timeTxt == null) return;

        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
        if (totalSeconds == lastDisplayedSeconds) return;

        lastDisplayedSeconds = totalSeconds;
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timeTxt.text = string.Format(timeFormat, minutes, seconds);

        // 10 saniyenin altına indiğinde efekt kontrolü
        if (totalSeconds <= warningThreshold && totalSeconds > 0)
        {
            ApplyWarningTick();
        }
        else if (isWarningActive && totalSeconds > warningThreshold)
        {
            ResetVisuals();
        }
    }

    private void ApplyWarningTick()
    {
        // Rengi kırmızıya çevir
        if (!isWarningActive)
        {
            isWarningActive = true;
            colorTween?.Kill();
            colorTween = timeTxt.DOColor(warningColor, 0.2f);
        }

        // Her saniye kalp atışı / zıplama efekti
        if (usePulseEffect)
        {
            pulseTween?.Kill();
            timeTxt.transform.localScale = originalScale;
            pulseTween = timeTxt.transform.DOPunchScale(originalScale * pulseScale, pulseDuration, 1, 0.5f);
        }
    }

    public void Show()
    {
        if (timerBarRoot != null)
        {
            timerBarRoot.SetActive(true);
        }
    }

    public void Hide()
    {
        if (timerBarRoot != null)
        {
            timerBarRoot.SetActive(false);
        }
    }

    private void CacheComponents()
    {
        if (timerBarRoot == null)
        {
            timerBarRoot = gameObject;
        }

        if (timeTxt == null)
        {
            timeTxt = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (timeTxt != null)
        {
            if (originalScale == Vector3.zero)
            {
                originalScale = timeTxt.transform.localScale;
            }

            if (originalColor == default)
            {
                originalColor = timeTxt.color;
            }
        }
    }

    public void ResetVisuals()
    {
        pulseTween?.Kill();
        colorTween?.Kill();

        isWarningActive = false;

        if (timeTxt != null)
        {
            if (originalScale != Vector3.zero)
            {
                timeTxt.transform.localScale = originalScale;
            }

            if (originalColor != default)
            {
                timeTxt.color = originalColor;
            }
        }
    }
}
