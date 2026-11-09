using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup dialog shown when the player attempts to play or restart with 0 lives.
/// - 'inside' content scales up with Ease.OutBack.
/// - CloseButton and backdrop clicks dismiss the popup.
/// - Displays real-time countdown to the next regenerating life.
/// - ShopButton is reserved for opening the shop in future updates.
/// </summary>
public class NoLivesLeftPopup : PopupBase<NoLivesLeftPopup>
{
    [Header("Buttons")]
    [Tooltip("Button used to close the popup.")]
    [SerializeField] private Button closeButton;

    [Tooltip("Optional backdrop button to dismiss popup on background click.")]
    [SerializeField] private Button backdropButton;

    [Tooltip("Shop button to purchase lives/coins (hooked to shop in future updates).")]
    [SerializeField] private Button shopButton;

    [Tooltip("Whether clicking outside the popup on the dark backdrop closes it.")]
    [SerializeField] private bool closeOnBackdropClick = true;

    protected override void Awake()
    {
        base.Awake();

        CacheReferences();
        SetupButtonListeners();
    }

    private void SetupButtonListeners()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        if (backdropButton != null && closeOnBackdropClick)
        {
            backdropButton.onClick.AddListener(Close);
        }

        if (shopButton != null)
        {
            shopButton.onClick.AddListener(OnShopClicked);
        }
    }

    protected override void OnOpening()
    {
        base.OnOpening();

        // Pause gameplay if opened inside Game scene
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.SetPaused(true);
        }
    }

    protected override void OnClosed()
    {
        base.OnClosed();

        // Resume gameplay if opened inside Game scene
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.SetPaused(false);
        }
    }

    private void OnShopClicked()
    {
        SoundManager.PlayButtonSound();
        Debug.Log("<color=#f39c12><b>[NoLivesLeftPopup]</b> ShopButton tıklandı (Shop sistemi açıldığında buraya bağlanacak).</color>");
    }

    /// <summary>
    /// Automatically resolves and caches child references if not assigned in inspector.
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

        // Inside container (animated by PopupBase AnimateOpen)
        Transform inside = transform.Find("inside");
        if (inside != null)
        {
            if (popupContent == null)
            {
                popupContent = inside as RectTransform;
            }

            if (closeButton == null)
            {
                Transform close = inside.Find("CloseButton");
                if (close != null) closeButton = close.GetComponent<Button>();
            }

            if (shopButton == null)
            {
                Transform shop = inside.Find("YesButton") ?? inside.Find("ShopButton");
                if (shop != null) shopButton = shop.GetComponent<Button>();
            }
        }
    }

    private void Reset()
    {
        CacheReferences();
    }
}
