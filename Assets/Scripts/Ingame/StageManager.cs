using Assets.Scripts;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static EnemyBase;

public class StageManager : MonoBehaviour
{
    private bool PressedAnyKey = false;
    private bool IsStageStarted = false;
    private bool IsStagePaused = false;
    public bool ReadIsStagePaused => IsStagePaused;

    [SerializeField] private GameObject pauseOverlay, titleOverlay;
    [SerializeField] private EnemyCode[] appearingEnemies;
    [SerializeField] private float extraEnemyWaittime = 0f;
    [SerializeField] private TMP_Text LoadingState;
    [SerializeField] private CharacterPrefabsStorage prefabStorage;

    [SerializeField] private Image PauseButton;
    [SerializeField] private Sprite PausedSprite, UnpausedSprite;

    private PlayerManager playerManager;

    bool IsStageReady = false, IsStageEnd = false;

    private void Start()
    {
        playerManager = GetComponent<PlayerManager>();
        LoadingState.text = "Loading stage, please wait...";
        StartCoroutine(LoadRequiredPrefabs());
        EnemyTooltipsScript.isAnyTooltipsShowing = false;
        Time.timeScale = 0f;
    }

    private IEnumerator LoadRequiredPrefabs()
    {
        // Load all player prefabs
        CharacterPrefabsStorage.PlayerPrefabs = new();
        int i = 0;
        foreach (var reference in prefabStorage.PlayerAssetReferences)
        {
            var handle = DataHandler.Instance.LoadAddressable<GameObject>(reference);
            yield return handle;
            CharacterPrefabsStorage.PlayerPrefabs[i] = handle.Result;
            i++;
        }

        // Load only required enemy prefabs
        CharacterPrefabsStorage.EnemyPrefabs = new();
        HashSet<int> uniqueIndices = new(); // prevent duplicate loads

        foreach (var code in appearingEnemies)
        {
            if (uniqueIndices.Add((int) code)) // only process unique ones
            {
                var reference = prefabStorage.EnemyAssetReferences[(int)code];
                var handle = DataHandler.Instance.LoadAddressable<GameObject>(reference);
                yield return handle;
                CharacterPrefabsStorage.EnemyPrefabs[(int)code] = handle.Result;
            }
        }

        IsStageReady = true;
        LoadingState.text = "<color=green>---Press any key to start---</color>";
    }

    IEnumerator TitleFadeOut()
    {
        CanvasGroup canvasGroup = titleOverlay.GetComponent<CanvasGroup>();

        float c = 0, d = 1.25f, cJump = 0.02f;
        while (c < d)
        {
            canvasGroup.alpha = Mathf.Lerp(1, 0, c * 1.0f / d);
            c += cJump;
            yield return new WaitForSecondsRealtime(cJump);
        }

        canvasGroup.alpha = 0;
        Destroy(titleOverlay);

        Time.timeScale = 1f;
        EntityManager.OnStageStart(extraEnemyWaittime);
        yield return new WaitForSeconds(1.5f);
        GetComponent<PlayerManager>().enabled = true;
        yield return null;

        IsStageStarted = true;
        StartCoroutine(CheckStageStatus());
    }

    private void Update()
    {
        if (!IsStageReady) return;

        Time.timeScale = IsStagePaused || playerManager.IsReadingSkillView ? 0f : 1f;

        if (!PressedAnyKey && !IsStageStarted && Input.anyKeyDown)
        {
            PressedAnyKey = true;
            StartCoroutine(TitleFadeOut());
        }

        if (!IsStageStarted) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePauseStage();
        }
    }

    IEnumerator CheckStageStatus()
    {
        while (IsStageStarted)
        {
            yield return new WaitForSeconds(2f);
            if (!playerManager.IsPlayerAlive || !EntityManager.Enemies.Any(e => e && e.IsAlive()))
            {
                IsStageStarted = false;
                OnStageEnd(playerManager.IsPlayerAlive);
            }
        }
    }

    public void OnStageEnd(bool resultIsWin)
    {
        IsStageEnd = true;
        StartCoroutine(FadeIn(resultIsWin));
    }

    IEnumerator FadeIn(bool resultIsWin)
    {
        TMP_Text text = pauseOverlay.GetComponentInChildren<TMP_Text>();
        text.text = resultIsWin ? "<color=green>Stage completed</color>" : "<color=red>Defeated</color>";
        CanvasGroup canvasGroup = pauseOverlay.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0;
        pauseOverlay.SetActive(true);

        float c = 0, d = 1.25f, cJump = 0.02f;
        while (c < d)
        {
            canvasGroup.alpha = Mathf.Lerp(0, 1, c * 1.0f / d);
            c += cJump;
            yield return new WaitForSecondsRealtime(cJump);
        }
    }

    public void TogglePauseStage()
    {
        if (IsStageEnd) return;

        IsStagePaused = !IsStagePaused;
        pauseOverlay.SetActive(IsStagePaused);

        PauseButton.sprite = IsStagePaused ? PausedSprite : UnpausedSprite;
    }

    public void RetryStage()
    {
        Time.timeScale = 1f;
        EnemySpawnpointScript.OnStageRetry();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitStage()
    {
        EnemySpawnpointScript.OnStageRetry();
        CharacterPrefabsStorage.EnemyPrefabs.Clear();
        CharacterPrefabsStorage.PlayerPrefabs.Clear();

        var menuScene = SceneManager.GetSceneByName("MainMenu");
        if (menuScene.IsValid())
        {
            SceneManager.LoadScene("MainMenu");
        }
        else
        {
            Application.Quit();
        }
    }

    public static void SpecialStageAddsOn(EntityBase entity)
    {

    }
}