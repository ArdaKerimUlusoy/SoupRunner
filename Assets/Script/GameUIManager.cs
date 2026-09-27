using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameUIManager : MonoBehaviour
{
    public static GameUIManager Instance;

    [Header("Referanslar")]
    public SoupPhysics soupPhysics;
    public Transform player;

    [Header("Üst HUD Elemanlarý")]
    public Slider soupBar;
    public Image soupFillImage;
    public TextMeshProUGUI scoreText;

    [Header("Game Over Paneli")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI finalDistanceText;
    public TextMeshProUGUI bestDistanceText;
    public Button tryAgainButton;
    public Button mainMenuButton; // Ana Menü Butonu

    [Header("Sahne Ayarlarý")]
    public string mainMenuSceneName = "MainMenu"; // Ana menü sahnesinin tam adý

    [Header("Renk Uyarýlarý")]
    public Color normalSoupColor = new Color(1f, 0.35f, 0.05f);
    public Color dangerSoupColor = new Color(0.9f, 0.1f, 0.1f);

    private float startZ;
    private int currentDistance = 0;
    private bool isGameOver = false;

    private const string BEST_SCORE_KEY = "SoupRunner_BestDistance";

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
    }

    private void Update()
    {
        if (isGameOver) return;

        UpdateSoupDisplay();
        UpdateScoreDisplay();
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

    public void TriggerGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;

        // Barýn tamamen sýfýra boþalmasýný bekleyip paneli öyle açan Coroutine
        StartCoroutine(GameOverRoutine());
    }

    private IEnumerator GameOverRoutine()
    {
        // 1. Barý zorla ve hýzlýca 0'a çek
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
            soupBar.value = 0f; // Kesin olarak sýfýrla
        }

        // 2. Rekor ve skor hesaplama
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
            bestDistanceText.text = $"En Ýyi: {bestDistance} m";
        }

        // 3. Paneli göster ve oyunu durdur
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

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