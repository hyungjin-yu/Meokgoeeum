using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FadeManager (화면 페이드 매니저)
/// 큐브 면 전환 시 화면을 검게 덮었다가 걷어내는 연출입니다. [[22 핵심 시스템 통합 기획]]
/// 시퀀스의 ⑤ SceneTransition 단계에서 [[CubeMapManager]]가 호출합니다.
///
/// UI를 손으로 만들지 않고 Awake()에서 풀스크린 검은 Image를 코드로 직접 생성합니다
/// (ColorOrbPool이 프리미티브를 코드로 만드는 것과 같은 이유 — 에디터에서 손으로 Canvas를
/// 잘못 만들 위험을 없애고, 씬에 아무것도 없어도 바로 동작하게 하기 위함).
/// </summary>
public class FadeManager : MonoBehaviour
{
    public static FadeManager Instance { get; private set; }

    [Tooltip("페이드 인/아웃에 걸리는 시간입니다.")]
    public float fadeDuration = 0.4f;

    private Image fadeImage;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("[FadeManager] 이미 인스턴스가 존재합니다. 중복 오브젝트를 파괴합니다.");
            Destroy(gameObject);
            return;
        }

        BuildFadeCanvas();
    }

    private void BuildFadeCanvas()
    {
        var canvasObj = new GameObject("FadeCanvas");
        canvasObj.transform.SetParent(transform, false);

        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // 다른 UI보다 항상 위에 그려지도록

        canvasObj.AddComponent<CanvasScaler>();

        var imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);

        fadeImage = imageObj.AddComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f); // 시작은 완전 투명
        fadeImage.raycastTarget = false; // 평소엔 입력을 막지 않음

        var rect = fadeImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public IEnumerator FadeOut() => Fade(0f, 1f);
    public IEnumerator FadeIn() => Fade(1f, 0f);

    private IEnumerator Fade(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            fadeImage.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }
        fadeImage.color = new Color(0f, 0f, 0f, to);
    }
}
