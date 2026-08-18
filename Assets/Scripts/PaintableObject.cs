using System.Collections;
using UnityEngine;

/// <summary>
/// PaintableObject (색칠 가능한 오브젝트)
/// 처음엔 회색이다가, [[04 색 구슬 시스템]]/[[11 셰이더 설계 - 색 복원 파동]]의
/// "색 복원 파동"이 닿으면 원래 색으로 돌아옵니다.
///
/// 원 문서는 Shader Graph 거리 마스크 방식을 스펙으로 잡았지만, Shader Graph 에셋을
/// 텍스트로 안전하게 손으로 만들기 어려워서 v0.1에서는 오브젝트별 색 Lerp로
/// 대체 구현했습니다. 시각적 목표(파동이 퍼지며 색이 돌아옴)는 동일합니다.
/// 나중에 Shader Graph로 바꾸더라도 Paint() API는 그대로 유지하면 됩니다.
/// </summary>
public class PaintableObject : MonoBehaviour
{
    [Tooltip("원래(진짜) 색입니다. 비워두면 지금 머티리얼 색을 그대로 원래 색으로 씁니다.")]
    public Color trueColor = Color.white;

    [Tooltip("회색에서 원래 색으로 돌아오는 데 걸리는 시간입니다.")]
    public float fadeDuration = 0.2f; // 2026-08-16: 0.5 -> 0.2. 파동의 오브젝트 간 트리거 시간차(보통 0.2~0.5초)보다 짧아야 "순서대로 칠해지는" 게 눈에 보임

    [Tooltip("시작할 때 자동으로 회색조로 만들지 여부입니다.")]
    public bool startGray = true;

    private Renderer cachedRenderer;
    private bool isPainted;
    private Coroutine fadeRoutine;

    private void Start()
    {
        cachedRenderer = GetComponent<Renderer>();
        if (cachedRenderer == null) return;

        if (startGray)
        {
            Color gray = Color.Lerp(trueColor, GrayscaleOf(trueColor), 0.85f);
            cachedRenderer.material.color = gray;
        }
    }

    /// <summary>
    /// 색을 되돌립니다. 이미 칠해졌으면 아무 일도 안 합니다 (중복 파동에 안전).
    /// </summary>
    public void Paint()
    {
        if (isPainted || cachedRenderer == null) return;
        isPainted = true;

        RestoredAreaRegistry.Register(transform.position); // [[EnemyHeup]]이 찾아갈 수 있게 등록

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeToTrueColor());
    }

    private IEnumerator FadeToTrueColor()
    {
        float elapsed = 0f;
        Color start = cachedRenderer.material.color;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            cachedRenderer.material.color = Color.Lerp(start, trueColor, elapsed / fadeDuration);
            yield return null;
        }

        cachedRenderer.material.color = trueColor;
    }

    private static Color GrayscaleOf(Color c)
    {
        float gray = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
        return new Color(gray, gray, gray, c.a);
    }
}
