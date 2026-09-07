using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Meokgoeeum
{
    /// <summary>
    /// HintPopupManager (튜토리얼 키 안내 팝업)
    /// [[15 튜토리얼 설계]] "키 안내 팝업 타이밍" 6종("WASD — 이동", "Space — 구르기" 등)을 위한
    /// 재사용 가능한 최소 팝업 표시기입니다. "환경으로 자연스럽게 유도... 텍스트 팝업 힌트 최소화"
    /// 원칙에 맞춰 입력을 막지 않는 화면 하단 토스트 한 줄로 — 일시정지도 안 시키고, 클릭도 안 먹습니다.
    ///
    /// [[NarrationManager]](재방문 나레이션)의 "페이드 인 → 유지 → 페이드 아웃" 코루틴 구조를 그대로
    /// 재활용했습니다. 다른 점은 글리치 스크램블 연출이 없다는 것뿐 — 나레이션은 "데자뷰" 연출이
    /// 목적이지만, 키 안내는 그냥 바로 읽혀야 하는 정보라 즉시 표시합니다.
    ///
    /// UI를 손으로 만들지 않고 Awake()에서 코드로 직접 생성합니다(SettingsMenu/NarrationManager와 같은 이유
    /// — 아직 UI 아트 에셋이 없음).
    /// </summary>
    public class HintPopupManager : MonoBehaviour
    {
        public static HintPopupManager Instance { get; private set; }

        [Tooltip("문구가 화면에 유지되는 시간입니다.")]
        public float holdDuration = 3f;

        [Tooltip("페이드 인/아웃에 걸리는 시간입니다.")]
        public float fadeDuration = 0.3f;

        private Text hintText;
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
                Debug.LogWarning("[HintPopupManager] 이미 인스턴스가 존재합니다. 중복 오브젝트를 파괴합니다.");
                Destroy(gameObject);
                return;
            }

            BuildHintCanvas();
        }

        private void BuildHintCanvas()
        {
            var canvasObj = new GameObject("HintCanvas");
            canvasObj.transform.SetParent(transform, false);

            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // NarrationManager(900)보다는 위, SettingsMenu(950)보다는 아래 — 설정 메뉴가 열리면
            // 힌트가 그 위를 덮지 않도록.
            canvas.sortingOrder = 910;

            canvasObj.AddComponent<CanvasScaler>();
            canvasGroup = canvasObj.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false; // 입력을 막지 않음 — 유도 중에도 계속 움직이고 공격할 수 있어야 함

            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(canvasObj.transform, false);
            var bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.55f);
            var bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.3f, 0.14f);
            bgRect.anchorMax = new Vector2(0.7f, 0.22f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var textObj = new GameObject("HintText");
            textObj.transform.SetParent(bgObj.transform, false);

            hintText = textObj.AddComponent<Text>();
            hintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hintText.fontSize = 22;
            hintText.alignment = TextAnchor.MiddleCenter;
            hintText.color = Color.white;
            hintText.text = string.Empty;

            var rect = hintText.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>임의의 문구를 화면 하단에 잠깐 띄웁니다. 이미 떠있는 힌트가 있으면 새 문구로 교체합니다.</summary>
        public void ShowHint(string text)
        {
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = StartCoroutine(HintRoutine(text));
        }

        private IEnumerator HintRoutine(string text)
        {
            hintText.text = text;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;

            yield return new WaitForSeconds(holdDuration);

            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                yield return null;
            }
            canvasGroup.alpha = 0f;
            hintText.text = string.Empty;

            activeRoutine = null;
        }
    }
}
