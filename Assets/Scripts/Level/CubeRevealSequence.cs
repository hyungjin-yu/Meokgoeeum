using System.Collections;
using UnityEngine;

/// <summary>
/// CubeRevealSequence (큐브 구조 노출 연출 — v0.3 최소 틀)
/// [[05 맵 시스템 - 큐브 구조]] "12층 = 큐브 구조 노출(면이 아닌 특수 공간)" 지점의 연출
/// 트리거만 미리 만들어둔 버전입니다.
///
/// 진짜 연출(Timeline + Cinemachine, 벽이 펼쳐지며 큐브 6면 구조가 드러나는 컷씬,
/// `SC_CubeReveal` 전용 씬)은 아트 에셋이 있어야 의미 있게 만들 수 있어서, 지금은
/// [[CubeMapManager]]의 `OnCubeRevealTriggered` 훅이 실제로 발동하는지 확인할 수 있는
/// 자리표시자(페이드 + 나레이션 한 줄 + 대기)로 구현했습니다.
///
/// 나중에 Timeline 에셋과 SC_CubeReveal 씬이 생기면, `PlaceholderRoutine()` 안의
/// 페이드+대기 부분을 `PlayableDirector.Play()` 호출로 바꾸면 됩니다 — 훅 연결 지점은 그대로.
/// </summary>
public class CubeRevealSequence : MonoBehaviour
{
    [Tooltip("자리표시자 연출이 유지되는 시간입니다. (실제 Timeline 길이로 나중에 교체)")]
    public float placeholderDuration = 3f;

    [Tooltip("자리표시자 연출 중 띄울 나레이션 문구입니다.")]
    public string placeholderLine = "...이 모든 게, 하나의 상자 안이었어?";

    private void Start()
    {
        if (CubeMapManager.Instance != null)
            CubeMapManager.Instance.OnCubeRevealTriggered += HandleCubeReveal;
        else
            Debug.LogWarning("[CubeRevealSequence] CubeMapManager가 없어서 훅을 연결하지 못했습니다.");
    }

    private void OnDestroy()
    {
        if (CubeMapManager.Instance != null)
            CubeMapManager.Instance.OnCubeRevealTriggered -= HandleCubeReveal;
    }

    private void HandleCubeReveal()
    {
        StartCoroutine(PlaceholderRoutine());
    }

    private IEnumerator PlaceholderRoutine()
    {
        Debug.Log("[CubeRevealSequence] 큐브 구조 노출 연출 시작! (자리표시자 — 실제 Timeline 연출로 나중에 교체)");

        NarrationManager.Instance?.ShowNarration(placeholderLine);

        // 2026-08-21: CubeMapManager의 면 전환과 같은 이유 — 화면이 페이드로 가려진 동안에도
        // 적 AI/공격 판정은 그대로 실행돼서 안 보이는 채로 맞을 수 있음. 연출 구간 동안 무적 처리.
        SetPlayerInvulnerable(true);

        if (FadeManager.Instance != null)
            yield return FadeManager.Instance.FadeOut();

        yield return new WaitForSeconds(placeholderDuration);

        if (FadeManager.Instance != null)
            yield return FadeManager.Instance.FadeIn();

        SetPlayerInvulnerable(false);

        Debug.Log("[CubeRevealSequence] 큐브 구조 노출 연출 종료. (다음 단계: 보스 문 등장 — 아직 미구현)");
    }

    private void SetPlayerInvulnerable(bool value)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        var health = player != null ? player.GetComponent<PlayerHealth>() : null;
        health?.SetInvulnerable(value);
    }
}
