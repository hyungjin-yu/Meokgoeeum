using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// CubeFaceData (큐브 면 데이터)
/// [[12 큐브 좌표계 설계]]의 면별 ScriptableObject. 면 6개(Front/Back/Top/Bottom/Left/Right)마다
/// 1개씩 에셋으로 만들어서 [[GameState]]에 등록합니다.
///
/// 단순화: 원안에는 revisitOverrideData(재방문 시 배치 차이)/isColorRestored/faceColor도 있지만,
/// 그건 [[24 디자인 필러]] 필러 2(반복 층 변주) 관련 v0.3 내러티브 레이어라 v0.2 첫 버전에서는
/// 뺐습니다 — 지금은 "회전하면 다른 면으로 진짜 전환된다"는 핵심 메커니즘만 검증하는 게 목표.
/// </summary>
[CreateAssetMenu(menuName = "MG/Cube Face Data", fileName = "CubeFaceData")]
public class CubeFaceData : ScriptableObject
{
    [Tooltip("면 인덱스. 0=Front, 1=Back, 2=Top, 3=Bottom, 4=Left, 5=Right (12 큐브 좌표계 설계 기준)")]
    public int faceIndex;

    [Tooltip("이 면에 해당하는 Addressable 씬입니다. Addressables Groups에서 씬을 등록한 뒤 여기 연결하세요.")]
    public AssetReference sceneReference;

    [Tooltip("최소 1번이라도 방문했는지입니다. 방문 순서 보장 규칙(1~6층은 항상 신규 면)에 씁니다.")]
    public bool isVisited;

    [Tooltip("이 면을 몇 번 방문했는지입니다. 0=미방문, 1=최초 방문, 2 이상=재방문.")]
    public int visitCount;

    /// <summary>
    /// 플레이 세션 시작마다 [[GameState]]가 호출해서 초기화합니다. ScriptableObject 에셋은
    /// 프로젝트에 영구 저장되므로, 이걸 안 하면 지난 플레이의 방문 기록이 다음 플레이에도 남습니다.
    /// </summary>
    public void ResetRuntimeState()
    {
        isVisited = false;
        visitCount = 0;
    }
}
