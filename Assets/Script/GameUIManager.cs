using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameUIManager : MonoBehaviour
{
    public static GameUIManager Instance;

    [Header("References")]
    public SoupPhysics soupPhysics;
    public Transform player;

    [Header("HUD")]
    public Slider soupBar;
    public Image soupFillImage;
    public TextMeshProUGUI scoreText;

    [Header("Game Over")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI finalDistanceText;
    public TextMeshProUGUI bestDistanceText;
    public Button tryAgainButton;
    public Button mainMenuButton;

    [Header("Scenes")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Colors")]
    public Color normalSoupColor = new Color(1f, 0.45f, 0.1f);
    public Color dangerSoupColor = new Color(0.95f, 0.15f, 0.15f);

    [Header("Co-op HUD")]
    public bool showBalanceMeter = true;
    public bool showControlsBanner = true;
    public RectTransform balanceMeterRoot;
    public RectTransform balancePuck;
    public Image balancePuckImage;
    public TextMeshProUGUI spillWarningText;
    public TextMeshProUGUI braceText;

    private float startZ;
    private int currentDistance = 0;
    private bool isGameOver = false;
    private const string BEST_SCORE_KEY = "SoupRunner_BestDistance";

    private const float METER_RADIUS = 52f;

    private SimpleRunnerController runnerController;
    private TextMeshProUGUI floatingTextObj;
    private Coroutine floatingTextRoutine;
    private TextMeshProUGUI powerUpBadge;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Time.timeScale = 1f;

        if (player != null)
        {
            startZ = player.position.z;
            runnerController = player.GetComponent<SimpleRunnerController>();
        }

        if (soupBar != null && soupPhysics != null)
        {
            soupBar.maxValue = soupPhysics.maxSoup;
            soupBar.value = soupPhysics.currentSoup;
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        if (tryAgainButton != null)
        {
            tryAgainButton.onClick.AddListener(RestartGame);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(GoToMainMenu);
        }

        SetupCoopHUD();
        SetupScreenJuice();
    }

    private void Update()
    {
        if (isGameOver)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.rKey.wasPressedThisFrame))
            {
                RestartGame();
            }
            return;
        }

        UpdateSoupDisplay();
        UpdateScoreDisplay();
        UpdateBalanceMeter();
        UpdatePowerUpBadges();
    }

    private void SetupScreenJuice()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        TMP_FontAsset font = scoreText != null ? scoreText.font : null;

        GameObject floatObj = new GameObject("FloatingBannerText", typeof(RectTransform));
        floatObj.transform.SetParent(canvas.transform, false);
        RectTransform fRect = floatObj.GetComponent<RectTransform>();
        fRect.anchorMin = new Vector2(0.5f, 0.68f);
        fRect.anchorMax = new Vector2(0.5f, 0.68f);
        fRect.sizeDelta = new Vector2(500f, 60f);

        floatingTextObj = floatObj.AddComponent<TextMeshProUGUI>();
        if (font != null) floatingTextObj.font = font;
        floatingTextObj.fontSize = 28f;
        floatingTextObj.alignment = TextAlignmentOptions.Center;
        floatingTextObj.color = Color.clear;
        floatingTextObj.raycastTarget = false;

        GameObject badgeObj = new GameObject("PowerUpBadge", typeof(RectTransform));
        badgeObj.transform.SetParent(canvas.transform, false);
        RectTransform bRect = badgeObj.GetComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0.5f, 0.88f);
        bRect.anchorMax = new Vector2(0.5f, 0.88f);
        bRect.sizeDelta = new Vector2(300f, 40f);

        powerUpBadge = badgeObj.AddComponent<TextMeshProUGUI>();
        if (font != null) powerUpBadge.font = font;
        powerUpBadge.fontSize = 18f;
        powerUpBadge.alignment = TextAlignmentOptions.Center;
        powerUpBadge.color = Color.yellow;
        powerUpBadge.gameObject.SetActive(false);
    }

    private void SetupCoopHUD()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        TMP_FontAsset font = scoreText != null ? scoreText.font : null;

        if (showControlsBanner)
        {
            GameObject bannerObj = new GameObject("CoopControlsBanner", typeof(RectTransform));
            bannerObj.transform.SetParent(canvas.transform, false);
            RectTransform bannerRect = bannerObj.GetComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0f, 1f);
            bannerRect.anchorMax = new Vector2(0f, 1f);
            bannerRect.pivot = new Vector2(0f, 1f);
            bannerRect.anchoredPosition = new Vector2(25f, -25f);
            bannerRect.sizeDelta = new Vector2(340f, 65f);

            Image bannerBg = bannerObj.AddComponent<Image>();
            bannerBg.color = new Color(0f, 0f, 0f, 0.45f);

            GameObject textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(bannerObj.transform, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 5f);
            textRect.offsetMax = new Vector2(-10f, -5f);

            TextMeshProUGUI bannerTMP = textObj.AddComponent<TextMeshProUGUI>();
            if (font != null) bannerTMP.font = font;
            bannerTMP.fontSize = 13.5f;
            bannerTMP.richText = true;
            bannerTMP.color = Color.white;
            bannerTMP.text = "<color=#FFE600><b>P1 (RUNNER):</b></color> Hold W (Accelerate) / A D (Steer) / Space\n<color=#00E5FF><b>P2 (CHEF):</b></color> Mouse to Counter-Balance | LMB: Brace";
        }

        if (showBalanceMeter && balanceMeterRoot == null)
        {
            GameObject meterObj = new GameObject("BalanceMeterRoot", typeof(RectTransform));
            meterObj.transform.SetParent(canvas.transform, false);
            balanceMeterRoot = meterObj.GetComponent<RectTransform>();
            balanceMeterRoot.anchorMin = new Vector2(1f, 0f);
            balanceMeterRoot.anchorMax = new Vector2(1f, 0f);
            balanceMeterRoot.pivot = new Vector2(1f, 0f);
            balanceMeterRoot.anchoredPosition = new Vector2(-30f, 30f);
            balanceMeterRoot.sizeDelta = new Vector2(140f, 140f);

            Image meterBg = meterObj.AddComponent<Image>();
            meterBg.color = new Color(0.08f, 0.08f, 0.12f, 0.75f);

            GameObject safeRingObj = new GameObject("SafeZoneRing", typeof(RectTransform));
            safeRingObj.transform.SetParent(meterObj.transform, false);
            RectTransform safeRect = safeRingObj.GetComponent<RectTransform>();
            safeRect.sizeDelta = new Vector2(METER_RADIUS * 1.6f, METER_RADIUS * 1.6f);
            Image safeImg = safeRingObj.AddComponent<Image>();
            safeImg.color = new Color(0f, 1f, 0.6f, 0.25f);

            GameObject puckObj = new GameObject("BalancePuck", typeof(RectTransform));
            puckObj.transform.SetParent(meterObj.transform, false);
            balancePuck = puckObj.GetComponent<RectTransform>();
            balancePuck.sizeDelta = new Vector2(26f, 26f);
            balancePuckImage = puckObj.AddComponent<Image>();
            balancePuckImage.color = new Color(0.2f, 0.9f, 1f, 1f);

            GameObject warnObj = new GameObject("SpillWarningText", typeof(RectTransform));
            warnObj.transform.SetParent(meterObj.transform, false);
            RectTransform warnRect = warnObj.GetComponent<RectTransform>();
            warnRect.anchoredPosition = new Vector2(0f, 85f);
            warnRect.sizeDelta = new Vector2(180f, 30f);
            spillWarningText = warnObj.AddComponent<TextMeshProUGUI>();
            if (font != null) spillWarningText.font = font;
            spillWarningText.fontSize = 16f;
            spillWarningText.alignment = TextAlignmentOptions.Center;
            spillWarningText.color = new Color(1f, 0.3f, 0.1f, 1f);
            spillWarningText.text = "<b>LEAKING!</b>";
            spillWarningText.gameObject.SetActive(false);

            GameObject braceObj = new GameObject("BraceText", typeof(RectTransform));
            braceObj.transform.SetParent(meterObj.transform, false);
            RectTransform bRect = braceObj.GetComponent<RectTransform>();
            bRect.anchoredPosition = new Vector2(0f, -80f);
            bRect.sizeDelta = new Vector2(160f, 25f);
            braceText = braceObj.AddComponent<TextMeshProUGUI>();
            if (font != null) braceText.font = font;
            braceText.fontSize = 12f;
            braceText.alignment = TextAlignmentOptions.Center;
            braceText.color = new Color(0f, 1f, 1f, 0.9f);
            braceText.text = "[BRACED]";
            braceText.gameObject.SetActive(false);
        }
    }

    private void UpdateSoupDisplay()
    {
        if (soupPhysics == null || soupBar == null) return;

        soupBar.value = Mathf.Lerp(soupBar.value, soupPhysics.currentSoup, Time.deltaTime * 12f);

        if (soupFillImage != null)
        {
            soupFillImage.color = (soupPhysics.currentSoup < 25f) ? dangerSoupColor : normalSoupColor;
        }
    }

    private void UpdateScoreDisplay()
    {
        if (player == null || scoreText == null) return;

        float distance = Mathf.Max(0f, player.position.z - startZ);
        currentDistance = Mathf.FloorToInt(distance);
        scoreText.text = $"{currentDistance} m";
    }

    private void UpdateBalanceMeter()
    {
        if (soupPhysics == null || balancePuck == null) return;

        Vector2 netTilt = soupPhysics.GetNetTilt();
        float threshold = soupPhysics.GetSpillThreshold();

        float normX = Mathf.Clamp(netTilt.x / threshold, -1.8f, 1.8f);
        float normY = Mathf.Clamp(netTilt.y / threshold, -1.8f, 1.8f);

        Vector2 targetPos = new Vector2(normX * (METER_RADIUS * 0.8f), normY * (METER_RADIUS * 0.8f));
        balancePuck.anchoredPosition = Vector2.Lerp(balancePuck.anchoredPosition, targetPos, Time.deltaTime * 18f);

        bool isSpilling = soupPhysics.IsCurrentlySpilling();
        bool isBracing = soupPhysics.IsBracing();
        bool hasLid = soupPhysics.HasLid();

        if (balancePuckImage != null)
        {
            if (hasLid)
            {
                balancePuckImage.color = new Color(1f, 0.85f, 0.1f, 1f);
            }
            else if (isSpilling)
            {
                balancePuckImage.color = dangerSoupColor;
            }
            else if (isBracing)
            {
                balancePuckImage.color = new Color(0.2f, 1f, 0.3f, 1f);
            }
            else
            {
                float tiltMagnitude = netTilt.magnitude / threshold;
                balancePuckImage.color = (tiltMagnitude > 0.75f) 
                    ? new Color(1f, 0.7f, 0.1f) 
                    : new Color(0.2f, 0.85f, 1f);
            }
        }

        if (spillWarningText != null)
        {
            spillWarningText.gameObject.SetActive(isSpilling && !hasLid);
            spillWarningText.alpha = 1f;
        }

        if (braceText != null)
        {
            braceText.gameObject.SetActive(isBracing);
        }
    }

    private void UpdatePowerUpBadges()
    {
        if (powerUpBadge == null) return;

        if (soupPhysics != null && soupPhysics.HasLid())
        {
            powerUpBadge.gameObject.SetActive(true);
            powerUpBadge.color = new Color(1f, 0.85f, 0.1f);
            powerUpBadge.text = $"<b>LID LOCKED: {soupPhysics.GetLidRemainingTime():F1}s</b>";
        }
        else if (runnerController != null && runnerController.IsSpicyTurboActive())
        {
            powerUpBadge.gameObject.SetActive(true);
            powerUpBadge.color = new Color(1f, 0.25f, 0.1f);
            powerUpBadge.text = $"<b>SPICY TURBO: {runnerController.GetSpicyTurboRemainingTime():F1}s</b>";
        }
        else
        {
            powerUpBadge.gameObject.SetActive(false);
        }
    }

    public void ShowFloatingText(string message, Color color)
    {
        if (floatingTextObj == null) return;

        if (floatingTextRoutine != null) StopCoroutine(floatingTextRoutine);
        floatingTextRoutine = StartCoroutine(FloatingTextRoutine(message, color));
    }

    private IEnumerator FloatingTextRoutine(string message, Color color)
    {
        floatingTextObj.text = $"<b>{message}</b>";
        floatingTextObj.color = color;
        floatingTextObj.transform.localScale = Vector3.one * 1.3f;

        float elapsed = 0f;
        float duration = 1.35f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            floatingTextObj.transform.localScale = Vector3.Lerp(Vector3.one * 1.3f, Vector3.one, t * 2f);
            floatingTextObj.color = new Color(color.r, color.g, color.b, 1f - Mathf.Pow(t, 2f));
            yield return null;
        }

        floatingTextObj.color = Color.clear;
    }

    public void TriggerGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;

        if (soupPhysics != null)
        {
            soupPhysics.LockCursor(false);
        }

        StartCoroutine(GameOverRoutine());
    }

    private IEnumerator GameOverRoutine()
    {
        float t = 0f;
        float initialBarVal = soupBar != null ? soupBar.value : 0f;

        while (t < 0.25f)
        {
            t += Time.deltaTime;
            if (soupBar != null)
            {
                soupBar.value = Mathf.Lerp(initialBarVal, 0f, t / 0.25f);
            }
            yield return null;
        }

        if (soupBar != null)
        {
            soupBar.value = 0f;
        }

        int bestDistance = PlayerPrefs.GetInt(BEST_SCORE_KEY, 0);
        if (currentDistance > bestDistance)
        {
            bestDistance = currentDistance;
            PlayerPrefs.SetInt(BEST_SCORE_KEY, bestDistance);
            PlayerPrefs.Save();
        }

        if (finalDistanceText != null)
        {
            finalDistanceText.text = $"Mesafe: {currentDistance} m";
        }

        if (bestDistanceText != null)
        {
            bestDistanceText.text = $"En Iyi: {bestDistance} m";
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Time.timeScale = 0f;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
