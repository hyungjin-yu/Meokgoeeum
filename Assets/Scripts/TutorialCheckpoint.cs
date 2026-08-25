using UnityEngine;

/// <summary>
/// TutorialCheckpoint (튜토리얼 체크포인트)
/// [[15 튜토리얼 설계]] 1단계("복도 끝 도달")/2단계("장애물 통과") 같은 "이 구간을 통과했다"는
/// 달성 조건을 확인하는 최소 버전입니다. 실제 팝업 힌트 UI(문서의 "키 안내 팝업" 6종)는 아직
/// 없어서(스코프 밖 — [[changelog/2026-08-25_1층콘텐츠-입장복도]] 참고) 지금은 Console 로그
/// 하나로 "이 트리거가 제대로 배치됐고 플레이어가 실제로 지나갔는지"만 검증합니다. 나중에
/// 팝업 UI를 붙일 때 이 스크립트의 OnReached 지점에 훅을 걸면 됩니다.
///
/// 씬에 트리거 콜라이더(Is Trigger 켠 Box Collider 등)와 함께 배치해서 씁니다.
/// </summary>
public class TutorialCheckpoint : MonoBehaviour
{
    [Tooltip("Console 로그에 표시할 이름입니다. (예: '1단계 - 복도 끝', '2단계 - 장애물 통과')")]
    public string checkpointName = "체크포인트";

    private bool triggered;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        triggered = true;
        Debug.Log($"[TutorialCheckpoint] {checkpointName} 도달!");
    }
}
