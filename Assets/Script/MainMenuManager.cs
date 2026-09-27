using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Oyun Sahnesi")]
    public string gameSceneName = "SampleScene"; // Baþlatýlacak sahnenin adý

    // Play Butonuna baðlanacak fonksiyon
    public void PlayGame()
    {
        Time.timeScale = 1f; // Oyun içi dondurma kalmýþsa normale döndür
        SceneManager.LoadScene(gameSceneName);
    }

    // Quit Butonuna baðlanacak fonksiyon
    public void QuitGame()
    {
        Debug.Log("<color=yellow>[SOUPRUNNER] Oyundan çýkýlýyor...</color>");

        // Derlenmiþ oyunda (Build) uygulamayý kapatýr
        Application.Quit();

#if UNITY_EDITOR
        // Unity Editör içinde test ederken Play modunu durdurur
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}