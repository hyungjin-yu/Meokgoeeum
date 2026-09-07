using UnityEngine;

/// <summary>
/// GameState (게임 상태 관리자)
/// [[10 씬 구조 & 기술 스펙]]은 GameState를 ScriptableObject로 설계했지만, 층 번호/방문
/// 기록처럼 "플레이 세션마다 초기화돼야 하는 런타임 상태"를 ScriptableObject 에셋에 두면
/// 에디터에 값이 영구로 눌러붙는 문제가 생깁니다(플레이 종료해도 에셋 값이 안 돌아옴).
/// 그래서 이 프로젝트의 다른 매니저(ColorSystemManager 등)와 같은 MonoBehaviour 싱글톤으로
/// 바꿔서 구현했습니다 — 세이브/로드가 생기면 그때 JsonUtility로 이 안의 값만 직렬화하면 됨.
/// </summary>
public class GameState : MonoBehaviour
{
    public static GameState Instance { get; private set; }

    [Tooltip("6개 면의 데이터입니다. faceIndex 0~5 순서로 정확히 6개 등록해야 합니다.")]
    public CubeFaceData[] faceData = new CubeFaceData[6];

    [Tooltip("화면에 표시되는 진행 카운터입니다. 면(faceIndex)과 별개 — 12 큐브 좌표계 설계 참고.")]
    public int floorNumber;

    [Tooltip("지금까지 처치한 먹괴음 총 수입니다. [[18 세이브 & 로드 기획]] 저장 항목, [[EnemyHealth]]가 증가시킵니다.")]
    public int totalKills;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("[GameState] 이미 인스턴스가 존재합니다. 중복 오브젝트를 파괴합니다.");
            Destroy(gameObject);
            return;
        }

        foreach (var data in faceData)
            data?.ResetRuntimeState(); // ScriptableObject 에셋에 이전 플레이 기록이 남아있지 않게 초기화
    }

    /// <summary>faceIndex(0~5)에 해당하는 면 데이터를 가져옵니다. 범위를 벗어나거나 비어있으면 null.</summary>
    public CubeFaceData GetFaceData(int faceIndex)
    {
        if (faceIndex < 0 || faceIndex >= faceData.Length) return null;
        return faceData[faceIndex];
    }
}
