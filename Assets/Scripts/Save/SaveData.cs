/// <summary>
/// SaveData ([[18 세이브 & 로드 기획]] 저장 데이터 구조)
/// 문서의 표를 그대로 따르되, 지금 코드베이스에 없는 개념 하나는 뺐습니다:
/// `faceColorRestored`(면 전체 색 복원 여부) — 각 [[PaintableObject]]의 칠해짐 상태가
/// 중앙에서 추적되지 않고 있어서, 지금은 "면 방문 여부"만 저장합니다. 나중에
/// [[RestoredAreaRegistry]]가 면별로 묶어서 관리하게 확장되면 이 필드를 추가하면 됩니다.
///
/// `orbCollected`(색 도감용)는 별도 상태로 안 두고, 로드/저장 시 `orbLifetimeCollected[i] > 0`에서
/// 파생해서 채웁니다 — 값이 둘 다 저장되지만 사실상 하나의 소스에서 나오는 정보입니다.
/// </summary>
[System.Serializable]
public class SaveData
{
    public int currentFloor;
    public float playerHP;
    public int[] orbCounts = new int[6];              // 보유(Held) — 색별
    public int[] orbLifetimeCollected = new int[6];    // 누적 획득 — 색별
    public int currentFaceIndex;
    public bool[] faceVisited = new bool[6];
    public int totalKills;
    public bool[] orbCollected = new bool[6];           // 도감용 — orbLifetimeCollected에서 파생
    public int newGamePlusCount;                        // 뉴게임+ 시스템 자체가 아직 없어서 항상 0
}
