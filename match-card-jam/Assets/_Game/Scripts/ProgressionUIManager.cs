using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Masadaki nesnelerin temizlenme oranını UI Image (Fill) üzerinden görselleştiren yönetici.
/// LevelManager tarafından doğrudan kontrol edilir (sıfır statik event bağımlılığı).
/// </summary>
public class ProgressionUIManager : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("Doldurulacak UI Image bileşeni (Image Type: Filled olmalı).")]
    [SerializeField] private Image fillImage;

    [Tooltip("Seviye numarasını gösteren TextMeshProUGUI bileşeni.")]
    [SerializeField] private TextMeshProUGUI levelNoText;

    [Header("Text Format")]
    [Tooltip("Seviye metninin öneki (Örn: 'Level ' -> 'Level 1').")]
    [SerializeField] private string levelPrefix = "Level ";

    [Header("Animation Settings")]
    [Tooltip("Barın yeni değere dolma süresi.")]
    [SerializeField] private float fillDuration = 0.25f;

    [Tooltip("Dolum animasyonu eğrisi.")]
    [SerializeField] private Ease fillEase = Ease.OutQuad;

    [Header("Juice / Punch Effect")]
    [Tooltip("Eşleşme olduğunda bar hafifçe esnesin/zıplasın mı?")]
    [SerializeField] private bool usePunchEffect = true;

    [Tooltip("Zıplama büyüklüğü.")]
    [SerializeField] private float punchStrength = 0.08f;

    [Tooltip("Zıplama süresi.")]
    [SerializeField] private float punchDuration = 0.2f;

    private const string KEY_CURRENT_LEVEL = "CurrentLevel";

    private Tween fillTween;
    private Tween punchTween;
    private Vector3 initialScale;
    private bool isInitialized;

    public bool IsInitialized => isInitialized;
    public TextMeshProUGUI LevelNoText => levelNoText;

    private void Awake()
    {
        CacheComponents();
        UpdateLevelText();
    }

    private void OnDisable()
    {
        KillTweens();
    }

    /// <summary>
    /// LevelManager tarafından seviye başlangıcında doğrudan çağrılır.
    /// Başlangıç durumunu ve seviye numarasını anında kurar.
    /// </summary>
    public void Initialize(int remainingItems = 0, int totalItems = 0, int levelNumber = -1)
    {
        CacheComponents();

        if (levelNumber > 0)
        {
            SetLevelNumber(levelNumber);
        }
        else
        {
            UpdateLevelText();
        }

        if (totalItems > 0)
        {
            UpdateProgressImmediate(remainingItems, totalItems);
        }
        else if (fillImage != null)
        {
            fillImage.fillAmount = 0f;
        }

        isInitialized = true;
    }

    /// <summary>
    /// Oyuncunun PlayerPrefs'teki güncel seviyesini okur ve metni günceller.
    /// Prefab'ler başa sarsa bile gerçek oyuncu seviyesini gösterir (Level 1, Level 2, Level 4...).
    /// </summary>
    public void UpdateLevelText()
    {
        SetLevelNumber(GameData.CurrentLevel);
    }

    /// <summary>
    /// Seviye numarasını doğrudan verilen değere göre günceller.
    /// </summary>
    public void SetLevelNumber(int levelNumber)
    {
        if (levelNoText != null)
        {
            levelNoText.text = $"{levelPrefix}{levelNumber}";
        }
    }

    private void CacheComponents()
    {
        if (initialScale == Vector3.zero)
        {
            initialScale = transform.localScale;
        }

        if (fillImage == null)
        {
            fillImage = GetComponentInChildren<Image>();
        }

        if (levelNoText == null)
        {
            levelNoText = GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    /// <summary>
    /// LevelManager tarafından her eşleşme olduğunda doğrudan çağrılır.
    /// Kalan azaldıkça bar dolacak şekilde animasyonu oynatır.
    /// </summary>
    public void UpdateProgress(int remaining, int total)
    {
        if (fillImage == null || total <= 0) return;

        float targetFill = Mathf.Clamp01(1f - ((float)remaining / total));
        AnimateProgress(targetFill);
    }

    private void AnimateProgress(float targetFill)
    {
        fillTween?.Kill();
        fillTween = fillImage.DOFillAmount(targetFill, fillDuration).SetEase(fillEase);

        if (usePunchEffect)
        {
            punchTween?.Kill();
            transform.localScale = initialScale;
            punchTween = transform.DOPunchScale(initialScale * punchStrength, punchDuration, 1, 0.5f);
        }
    }

    public void UpdateProgressImmediate(int remaining, int total)
    {
        if (fillImage == null || total <= 0) return;

        float targetFill = Mathf.Clamp01(1f - ((float)remaining / total));
        fillImage.fillAmount = targetFill;
    }

    public void ResetBar()
    {
        KillTweens();
        if (fillImage != null)
        {
            fillImage.fillAmount = 0f;
        }
    }

    private void KillTweens()
    {
        fillTween?.Kill();
        punchTween?.Kill();
        if (initialScale != Vector3.zero)
        {
            transform.localScale = initialScale;
        }
    }
}
