using UnityEngine;

/// <summary>
/// TutorialCheckpoint (튜토리얼 체크포인트)
/// [[15 튜토리얼 설계]] 1단계("복도 끝 도달")/2단계("장애물 통과") 같은 "이 구간을 통과했다"는
/// 달성 조건을 확인합니다. Console 로그로 "이 트리거가 제대로 배치됐고 플레이어가 실제로
/// 지나갔는지"를 검증하는 동시에, `hintText`가 비어있지 않으면 문서의 "키 안내 팝업" 6종 중
/// 해당하는 문구를 [[HintPopupManager]]로 띄웁니다(2026-08-31 추가 — 그 전엔 로그만 있었음).
///
/// 씬에 트리거 콜라이더(Is Trigger 켠 Box Collider 등)와 함께 배치해서 씁니다.
/// </summary>
public class TutorialCheckpoint : MonoBehaviour
{
    [Tooltip("Console 로그에 표시할 이름입니다. (예: '1단계 - 복도 끝', '2단계 - 장애물 통과')")]
    public string checkpointName = "체크포인트";

    [Tooltip("비어있지 않으면 도달 시 이 문구로 힌트 팝업을 띄웁니다. (예: 'Space — 구르기')")]
    public string hintText = "";

    [Tooltip("설정하면 도달 시 이 스포너의 StartEncounter()를 호출합니다. 2026-08-31 추가 — EncounterSpawner의 autoStart=true는 씬 로드와 동시에(플레이어가 아직 복도에 있어도) 전투를 시작시켜서, '방 진입 직후 전투 시작'이라는 튜토리얼 의도와 어긋났음. autoStart를 끄고 이 체크포인트(장애물 통과 직후)에서 대신 시작시킴.")]
    public EncounterSpawner encounterSpawnerToStart;

    [Tooltip("체크되면 도달 시 [[TutorialSkipManager]].DisableSkip()을 호출합니다 — 이 구간을 이미 정상 통과했으니 ESC 스킵 확인창을 더 이상 띄울 이유가 없다는 뜻입니다.")]
    public bool disablesTutorialSkip = false;

    private bool triggered;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        triggered = true;
        Debug.Log($"[TutorialCheckpoint] {checkpointName} 도달!");

        if (!string.IsNullOrEmpty(hintText))
            HintPopupManager.Instance?.ShowHint(hintText);

        encounterSpawnerToStart?.StartEncounter();

        if (disablesTutorialSkip)
            TutorialSkipManager.Instance?.DisableSkip();
    }
}
