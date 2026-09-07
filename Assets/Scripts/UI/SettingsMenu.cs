using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

/// <summary>
/// SettingsMenu (설정 메뉴)
/// [[MouseSensitivitySetting]]에 이미 준비돼있던 `SetSensitivity()`를 실제 슬라이더에 연결하는
/// 최소 버전 설정 화면입니다. [[NarrationManager]]/[[ColorOrbPool]]과 같은 이유로 — 아직 UI
/// 아트 에셋/디자인이 없어서 — 손으로 씬을 꾸미지 않고 Awake()에서 코드로 직접 만듭니다.
///
/// Esc로 열고 닫는 오버레이 패널이자 일시정지 메뉴입니다. 열리는 순간 `Time.timeScale = 0`으로
/// 게임을 멈춥니다 — 설정 만지는 동안 적한테 맞아서 게임오버 되는 UX가 나빠서(2026-08-25 추가).
/// 프로젝트의 모든 전투/이동 타이머가 스케일드 `Time.deltaTime`/`Time.time` 기반이라(오늘 만든
/// [[BossHealth]] DPS 계측 포함) `timeScale = 0`만으로 적 AI/공격/이동이 전부 같이 멈춥니다 —
/// 별도로 PlayerController 입력을 잠글 필요가 없습니다. 이 스크립트 자신의 Esc 폴링(`Update()`)은
/// `Time.deltaTime`을 안 쓰므로 `timeScale = 0`이어도 계속 정상 동작합니다(안 그러면 못 닫힘).
///
/// 프로젝트가 New Input System 전용(activeInputHandler: 1)이라 uGUI 상호작용에는
/// [[EventSystem]] + `InputSystemUIInputModule`이 필요합니다 — 씬에 없으면 여기서 같이 만듭니다.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    public static SettingsMenu Instance { get; private set; }

    private GameObject panel;
    private Slider sensitivitySlider;
    private Text sensitivityValueText;
    private MouseSensitivitySetting sensitivitySetting;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("[SettingsMenu] 이미 인스턴스가 존재합니다. 중복 오브젝트를 파괴합니다.");
            Destroy(gameObject);
            return;
        }

        EnsureEventSystem();
        BuildUI();
        panel.SetActive(false);
    }

    /// <summary>
    /// [[TutorialSkipManager]]와 Esc를 공유합니다 — 둘 다 각자 Esc를 폴링하면 같은 프레임에
    /// 둘 다 열리는 경쟁 상태가 생겨서, 이 스크립트 하나가 우선순위를 판단해 대신 호출해줍니다
    /// (2026-08-31 추가). 우선순위: 스킵 확인창이 떠있으면 그걸 닫는 것부터(취소) → 스킵이
    /// 아직 가능한 구간이면 설정 대신 스킵 확인창을 띄움 → 그 외엔 평소대로 설정 메뉴.
    /// </summary>
    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;

        if (TutorialSkipManager.Instance != null && TutorialSkipManager.Instance.IsShowing)
        {
            TutorialSkipManager.Instance.Cancel();
            return;
        }

        if (TutorialSkipManager.Instance != null && TutorialSkipManager.Instance.SkipAvailable && !panel.activeSelf)
        {
            TutorialSkipManager.Instance.ShowSkipConfirm();
            return;
        }

        Toggle();
    }

    private void Toggle()
    {
        bool willOpen = !panel.activeSelf;
        panel.SetActive(willOpen);
        Time.timeScale = willOpen ? 0f : 1f;
        if (willOpen) RefreshSliderValue();
    }

    /// <summary>
    /// 씬 전환이나 오브젝트 파괴로 이 컴포넌트가 사라질 때 열려있던 채로 timeScale=0이 박제되지
    /// 않도록 방어합니다(싱글턴이라 평소엔 안 없어지지만, 혹시 몰라 안전장치로 둠).
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this && panel != null && panel.activeSelf)
            Time.timeScale = 1f;
    }

    /// <summary>씬에 EventSystem이 없으면 만듭니다 — 없으면 슬라이더를 드래그해도 반응하지 않습니다.</summary>
    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;

        var esObj = new GameObject("EventSystem");
        esObj.AddComponent<EventSystem>();
        // AddComponent로 붙이면 OnEnable에서 자동으로 기본 UI 액션(포인터/클릭 등)이 배정됩니다
        // (Unity Input System 공식 문서 — InputSystemUIInputModule.AssignDefaultActions 참고).
        esObj.AddComponent<InputSystemUIInputModule>();
    }

    private void BuildUI()
    {
        var canvasObj = new GameObject("SettingsCanvas");
        canvasObj.transform.SetParent(transform, false);

        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950; // NarrationManager(900)보다 위, FadeManager(1000)보다 아래

        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        panel = new GameObject("Panel");
        panel.transform.SetParent(canvasObj.transform, false);
        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.85f);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.3f, 0.32f);
        panelRect.anchorMax = new Vector2(0.7f, 0.68f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        CreateText("Title", panel.transform, "설정", 24, TextAnchor.MiddleCenter,
            new Vector2(0.05f, 0.8f), new Vector2(0.95f, 0.98f));

        sensitivityValueText = CreateText("SensitivityLabel", panel.transform, "마우스 감도: 1.0", 18,
            TextAnchor.MiddleLeft, new Vector2(0.08f, 0.55f), new Vector2(0.92f, 0.72f));

        BuildSensitivitySlider();

        CreateText("CloseHint", panel.transform, "Esc로 닫기", 14, TextAnchor.MiddleCenter,
            new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.16f));
    }

    private void BuildSensitivitySlider()
    {
        var sliderObj = new GameObject("SensitivitySlider");
        sliderObj.transform.SetParent(panel.transform, false);
        var sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.08f, 0.36f);
        sliderRect.anchorMax = new Vector2(0.92f, 0.5f);
        sliderRect.offsetMin = Vector2.zero;
        sliderRect.offsetMax = Vector2.zero;
        sensitivitySlider = sliderObj.AddComponent<Slider>();

        var bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        var bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0.2f);
        SetStretch(bgObj.GetComponent<RectTransform>(), new Vector2(0f, 0.3f), new Vector2(1f, 0.7f));

        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        var fillAreaRect = fillArea.AddComponent<RectTransform>();
        SetStretch(fillAreaRect, new Vector2(0f, 0.3f), new Vector2(1f, 0.7f));

        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.3f, 0.7f, 1f, 1f);
        var fillRect = fill.GetComponent<RectTransform>();
        SetStretch(fillRect, Vector2.zero, Vector2.one);

        var handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderObj.transform, false);
        var handleAreaRect = handleArea.AddComponent<RectTransform>();
        SetStretch(handleAreaRect, Vector2.zero, Vector2.one);

        var handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        var handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;
        var handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(16f, 0f);

        sensitivitySlider.targetGraphic = handleImg;
        sensitivitySlider.fillRect = fillRect;
        sensitivitySlider.handleRect = handleRect;
        sensitivitySlider.direction = Slider.Direction.LeftToRight;
        sensitivitySlider.minValue = 0.1f;
        sensitivitySlider.maxValue = 3f;
        sensitivitySlider.wholeNumbers = false;
        sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
    }

    private static void SetStretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Text CreateText(string name, Transform parent, string content, int fontSize,
        TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var text = obj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = Color.white;
        text.text = content;

        var rect = text.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return text;
    }

    /// <summary>패널을 열 때마다 호출 — 저장된 실제 감도값으로 슬라이더를 맞춥니다(값 튐 방지로 SetValueWithoutNotify 사용).</summary>
    private void RefreshSliderValue()
    {
        if (sensitivitySetting == null)
            sensitivitySetting = FindObjectOfType<MouseSensitivitySetting>();

        if (sensitivitySetting == null)
        {
            Debug.LogWarning("[SettingsMenu] MouseSensitivitySetting을 씬에서 못 찾았습니다 — 슬라이더가 기본값으로만 동작합니다.");
            return;
        }

        float current = sensitivitySetting.GetCurrentSensitivity();
        sensitivitySlider.SetValueWithoutNotify(current);
        sensitivityValueText.text = $"마우스 감도: {current:F1}";
    }

    private void OnSensitivityChanged(float value)
    {
        if (sensitivitySetting == null)
            sensitivitySetting = FindObjectOfType<MouseSensitivitySetting>();

        sensitivitySetting?.SetSensitivity(value);
        sensitivityValueText.text = $"마우스 감도: {value:F1}";
    }
}
