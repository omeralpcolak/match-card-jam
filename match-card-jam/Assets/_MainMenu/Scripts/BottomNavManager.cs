using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the bottom navigation dock in the Main Menu (Shop, Home, Locked).
/// Handles tab switching, selection scale/punch animations, and locked-tab shake feedback.
/// Compatible with HorizontalLayoutGroup (does not overwrite child anchoredPositions).
/// </summary>
public class BottomNavManager : MonoBehaviour
{
    public enum TabType
    {
        Shop,
        Home,
        Locked
    }

    [Header("Tab Buttons")]
    [Tooltip("Button used to open the Shop tab.")]
    [SerializeField] private Button shopButton;

    [Tooltip("Button used to open the Home tab.")]
    [SerializeField] private Button homeButton;

    [Tooltip("Button for the locked feature tab.")]
    [SerializeField] private Button lockedButton;

    [Header("Tab Icons (Punch & Shake Animations)")]
    [Tooltip("Shop icon inside the shop button.")]
    [SerializeField] private RectTransform shopIcon;

    [Tooltip("Home icon inside the home button.")]
    [SerializeField] private RectTransform homeIcon;

    [Tooltip("Lock icon inside the locked button.")]
    [SerializeField] private RectTransform lockedIcon;

    [Header("Content Panels (Optional)")]
    [Tooltip("Optional panel to activate when Shop tab is selected.")]
    [SerializeField] private GameObject shopPanel;

    [Tooltip("Optional panel to activate when Home tab is selected.")]
    [SerializeField] private GameObject homePanel;

    [Header("Animation Settings")]
    [Tooltip("Scale multiplier for the selected active tab.")]
    [SerializeField] private float activeTabScale = 1.08f;

    [Tooltip("Duration of the tab scale transition.")]
    [SerializeField] private float transitionDuration = 0.2f;

    [Tooltip("Shake rotation strength for the locked tab.")]
    [SerializeField] private float lockedShakeStrength = 22f;

    public TabType CurrentTab { get; private set; } = TabType.Home;

    public static event Action<TabType> OnTabChanged;

    private Tween shopTween;
    private Tween homeTween;
    private Tween lockedTween;

    private void Awake()
    {
        AutoResolveReferences();
        SetupButtonListeners();
    }

    private void Start()
    {
        // Select Home by default without animation
        SelectTab(TabType.Home, immediate: true);
    }

    private void SetupButtonListeners()
    {
        if (shopButton != null) shopButton.onClick.AddListener(OnShopClicked);
        if (homeButton != null) homeButton.onClick.AddListener(OnHomeClicked);
        if (lockedButton != null) lockedButton.onClick.AddListener(OnLockedClicked);
    }

    #region Tab Selection

    /// <summary>
    /// Selects a tab, scales the active tab, and toggles content panels.
    /// Preserves layout positions (safe with HorizontalLayoutGroup).
    /// </summary>
    public void SelectTab(TabType newTab, bool immediate = false)
    {
        CurrentTab = newTab;

        AnimateTabScale(shopButton != null ? shopButton.transform as RectTransform : null, newTab == TabType.Shop, ref shopTween, immediate);
        AnimateTabScale(homeButton != null ? homeButton.transform as RectTransform : null, newTab == TabType.Home, ref homeTween, immediate);
        AnimateTabScale(lockedButton != null ? lockedButton.transform as RectTransform : null, newTab == TabType.Locked, ref lockedTween, immediate);

        if (shopPanel != null) shopPanel.SetActive(newTab == TabType.Shop);
        if (homePanel != null) homePanel.SetActive(newTab == TabType.Home);

        OnTabChanged?.Invoke(newTab);
    }

    private void AnimateTabScale(RectTransform tabRect, bool isSelected, ref Tween activeTween, bool immediate)
    {
        if (tabRect == null) return;

        activeTween?.Kill();

        Vector3 targetScale = isSelected ? Vector3.one * activeTabScale : Vector3.one;

        if (immediate)
        {
            tabRect.localScale = targetScale;
            return;
        }

        activeTween = tabRect.DOScale(targetScale, transitionDuration).SetEase(Ease.OutQuad);
    }

    #endregion

    #region Button Click Handlers

    private void OnHomeClicked()
    {
        if (CurrentTab != TabType.Home)
        {
            SelectTab(TabType.Home);
        }

        if (homeIcon != null)
        {
            homeIcon.DOKill();
            homeIcon.localScale = Vector3.one;
            homeIcon.DOPunchScale(Vector3.one * 0.18f, 0.25f, 1, 0.5f);
        }
    }

    private void OnShopClicked()
    {
        if (CurrentTab != TabType.Shop)
        {
            SelectTab(TabType.Shop);
        }

        if (shopIcon != null)
        {
            shopIcon.DOKill();
            shopIcon.localScale = Vector3.one;
            shopIcon.DOPunchScale(Vector3.one * 0.18f, 0.25f, 1, 0.5f);
        }
    }

    private void OnLockedClicked()
    {
        // Does not switch tab; shakes the lock icon
        if (lockedIcon != null)
        {
            lockedIcon.DOKill();
            lockedIcon.localRotation = Quaternion.identity;
            lockedIcon.localScale = Vector3.one;

            lockedIcon.DOShakeRotation(0.35f, new Vector3(0f, 0f, lockedShakeStrength), 12, 90f);
            lockedIcon.DOPunchScale(Vector3.one * 0.12f, 0.25f, 2, 0.5f);
        }

        Debug.Log("<color=#f39c12><b>[BottomNavManager]</b> This feature is locked! Coming soon.</color>");
    }

    #endregion

    private void AutoResolveReferences()
    {
        // Auto-find buttons inside child TabsContainer if not assigned
        if (shopButton == null)
        {
            Transform t = transform.Find("TabsContainer/ShopTab") ?? transform.Find("ShopTab");
            if (t != null) shopButton = t.GetComponent<Button>();
        }

        if (homeButton == null)
        {
            Transform t = transform.Find("TabsContainer/HomeTab") ?? transform.Find("HomeTab");
            if (t != null) homeButton = t.GetComponent<Button>();
        }

        if (lockedButton == null)
        {
            Transform t = transform.Find("TabsContainer/LockedTab") ?? transform.Find("LockedTab");
            if (t != null) lockedButton = t.GetComponent<Button>();
        }

        // Auto-find icons
        if (shopIcon == null && shopButton != null)
        {
            Transform iconT = shopButton.transform.Find("ShopIcon") ?? shopButton.transform.Find("Icon") ?? shopButton.transform.Find("Image");
            if (iconT != null) shopIcon = iconT as RectTransform;
        }

        if (homeIcon == null && homeButton != null)
        {
            Transform iconT = homeButton.transform.Find("HomeIcon") ?? homeButton.transform.Find("Icon") ?? homeButton.transform.Find("Image");
            if (iconT != null) homeIcon = iconT as RectTransform;
        }

        if (lockedIcon == null && lockedButton != null)
        {
            Transform iconT = lockedButton.transform.Find("LockedIcon") ?? lockedButton.transform.Find("Image") ?? lockedButton.transform.Find("Icon");
            if (iconT != null) lockedIcon = iconT as RectTransform;
        }
    }

    private void OnDestroy()
    {
        shopTween?.Kill();
        homeTween?.Kill();
        lockedTween?.Kill();

        if (shopIcon != null) shopIcon.DOKill();
        if (homeIcon != null) homeIcon.DOKill();
        if (lockedIcon != null) lockedIcon.DOKill();
    }
}
