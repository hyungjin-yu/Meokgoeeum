using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// NarrationManager (반복 층 나레이션)
/// [[22 핵심 시스템 통합 기획]] "노이즈 텍스트(Glitch Text)" 구현. 같은 면을 재방문하면
/// [[CubeMapManager]]가 이걸 호출해서 화면 상단에 글리치 효과로 내레이션을 띄웁니다.
///
/// [[12 큐브 좌표계 설계]]의 NarrationTier(Hint/Confirmed) 그대로 — 첫 재방문(2번째)엔
/// 약한 데자뷰 문구, 그 이후(3번째+)엔 강한 확신 문구를 랜덤으로 고릅니다. 기본 문구는
/// [[19 층별 상세 설계]] 5층/8층 예시 대사를 그대로 가져왔습니다(문서 자체가 "층 번호가
/// 아니라 면+방문횟수 조합의 예시"라고 명시하고 있어서, 실제로는 아무 면의 재방문에서나 나옴).
///
/// UI를 손으로 만들지 않고 Awake()에서 코드로 직접 생성합니다 ([[FadeManager]]와 같은 이유).
/// BGM 피치/에코 왜곡은 [[08 사운드 기획]] 스펙이지만 프로젝트에 아직 오디오 에셋이 하나도
/// 없어서 이번엔 빠졌습니다 — 나중에 BGM 에셋이 들어오면 AudioMixer Snapshot 전환을 추가하면 됨.
/// </summary>
public class NarrationManager : MonoBehaviour
{
    public static NarrationManager Instance { get; private set; }

    [Tooltip("문구가 스크램블(글리치)되다가 정상으로 맞춰지는 데 걸리는 시간입니다.")]
    public float glitchDuration = 0.4f;

    [Tooltip("정상 문구로 화면에 유지되는 시간입니다.")]
    public float holdDuration = 3f;

    [Tooltip("첫 재방문(2번째 방문)일 때 후보 문구입니다. (19 층별 상세 설계 5층 예시 기준)")]
    public string[] hintLines =
    {
        "이 골목... 전에 본 것 같은데.",
        "왠지... 낯익다.",
    };

    [Tooltip("3번째 이상 재방문일 때 후보 문구입니다. (19 층별 상세 설계 8~11층 예시 기준)")]
    public string[] confirmedLines =
    {
        "분명히 여기 왔었어. 왜 또 여기지?",
        "...누가 일부러 이러는 것 같아.",
        "이 세계에서 나갈 수 없는 걸까...",
    };

    private const string GlitchCharset = "アイウエオカキクケコ★☆#@!?><一二三ㅁㄴㅇㄹㅎㅋㅌ";

    private Text narrationText;
    private CanvasGroup canvasGroup;
    private Coroutine activeRoutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("[NarrationManager] 이미 인스턴스가 존재합니다. 중복 오브젝트를 파괴합니다.");
            Destroy(gameObject);
            return;
        }

        BuildNarrationCanvas();
    }

    private void BuildNarrationCanvas()
    {
        var canvasObj = new GameObject("NarrationCanvas");
        canvasObj.transform.SetParent(transform, false);

        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900; // FadeManager(1000)보다는 아래, 나머지 UI보다는 위

        canvasObj.AddComponent<CanvasScaler>();
        canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;

        var textObj = new GameObject("NarrationText");
        textObj.transform.SetParent(canvasObj.transform, false);

        narrationText = textObj.AddComponent<Text>();
        narrationText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        narrationText.fontSize = 28;
        narrationText.alignment = TextAnchor.MiddleCenter;
        narrationText.color = Color.white;
        narrationText.text = string.Empty;

        var rect = narrationText.rectTransform;
        rect.anchorMin = new Vector2(0.1f, 0.85f);
        rect.anchorMax = new Vector2(0.9f, 0.95f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// [[CubeMapManager]]가 재방문 판정 시 호출합니다. 티어에 맞는 문구를 랜덤으로 골라 띄웁니다.
    /// </summary>
    public void TriggerRevisitNarration(NarrationTier tier)
    {
        string[] pool = tier == NarrationTier.Hint ? hintLines : confirmedLines;
        if (pool == null || pool.Length == 0) return;

        string line = pool[Random.Range(0, pool.Length)];
        ShowNarration(line);
    }

    /// <summary>임의의 문구를 글리치 효과로 띄웁니다.</summary>
    public void ShowNarration(string text)
    {
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(NarrationRoutine(text));
    }

    private IEnumerator NarrationRoutine(string finalText)
    {
        canvasGroup.alpha = 1f;

        // 1. 글리치 스크램블 → 왼쪽부터 점점 원래 글자로 맞춰짐
        float elapsed = 0f;
        int length = finalText.Length;
        var sb = new StringBuilder();

        while (elapsed < glitchDuration)
        {
            elapsed += Time.deltaTime;
            int revealedCount = Mathf.FloorToInt(length * (elapsed / glitchDuration));

            sb.Clear();
            for (int i = 0; i < length; i++)
            {
                char c = finalText[i];
                sb.Append(i < revealedCount || c == ' ' ? c : GlitchCharset[Random.Range(0, GlitchCharset.Length)]);
            }
            narrationText.text = sb.ToString();
            yield return null;
        }

        narrationText.text = finalText;

        // 2. 정상 문구 유지
        yield return new WaitForSeconds(holdDuration);

        // 3. 페이드 아웃
        float fadeElapsed = 0f;
        const float fadeOutDuration = 0.5f;
        while (fadeElapsed < fadeOutDuration)
        {
            fadeElapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, fadeElapsed / fadeOutDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
        narrationText.text = string.Empty;

        activeRoutine = null;
    }
}

/// <summary>[[12 큐브 좌표계 설계]] 기준 — 첫 재방문(Hint)과 그 이후(Confirmed)로 구분.</summary>
public enum NarrationTier
{
    Hint,
    Confirmed,
}
