using System.IO;
using UnityEngine;

/// <summary>
/// SaveManager ([[18 세이브 & 로드 기획]] 최소 버전)
/// 문서의 "자동 저장, 층 입장 시 / 보스 처치 후" 그대로 — [[CubeMapManager]]가 면을 새로 로드할
/// 때마다, [[BossHealth]]가 죽을 때 각각 `Save()`를 호출합니다. `JsonUtility`로 [[SaveData]]를
/// `Application.persistentDataPath`에 JSON으로 저장/로드합니다(문서의 Unity 구현 방식 그대로).
///
/// 문서 대비 스코프 컷 (지금 단계에서 안 만든 것):
/// - **슬롯 3개 → 1개만.** 슬롯을 고르는 타이틀/메뉴 화면 자체가 아직 없어서, 여러 슬롯을
///   만들어봐야 고를 방법이 없습니다. 나중에 메뉴 UI가 생기면 `SaveFileName`을 슬롯 번호
///   매개변수로 바꾸면 됨.
/// - **Steam Cloud 연동 → 로컬 파일만.** 문서 자체도 "도입 예정"으로 미확정 상태.
/// - **뉴게임+/도감 회차 간 유지 로직 → 아직 없음.** `SaveData`에 필드는 만들어뒀지만
///   (`newGamePlusCount`, `orbCollected`) 그 시스템 자체가 없어서 지금은 그냥 저장/로드만 되고
///   게임플레이에 영향을 주지는 않습니다.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private const string SaveFileName = "save_slot_0.json"; // 슬롯 1개 고정 (스코프 컷, 위 설명 참고)

    private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("[SaveManager] 이미 인스턴스가 존재합니다. 중복 오브젝트를 파괴합니다.");
            Destroy(gameObject);
        }
    }

    public bool HasSaveFile() => File.Exists(SavePath);

    /// <summary>
    /// 지금 게임 상태를 그러모아 파일로 저장합니다. 각 매니저가 아직 씬에 없으면(널이면)
    /// 그 항목은 기본값(0)으로 저장됩니다 — 시스템 일부만 붙어있는 개발 중에도 에러 없이 동작.
    /// </summary>
    public void Save()
    {
        var data = new SaveData
        {
            currentFloor = GameState.Instance != null ? GameState.Instance.floorNumber : 0,
            playerHP = GetPlayerHP(),
            currentFaceIndex = CubeMapManager.Instance != null ? CubeMapManager.Instance.CurrentFaceIndex : 0,
            totalKills = GameState.Instance != null ? GameState.Instance.totalKills : 0,
            newGamePlusCount = 0, // 뉴게임+ 시스템 없음 — 항상 0
        };

        for (int i = 0; i < 6; i++)
        {
            var color = (OrbColor)i;
            data.orbCounts[i] = ColorSystemManager.Instance != null ? ColorSystemManager.Instance.GetHeld(color) : 0;
            data.orbLifetimeCollected[i] = ColorSystemManager.Instance != null ? ColorSystemManager.Instance.GetLifetimeCollected(color) : 0;
            data.orbCollected[i] = data.orbLifetimeCollected[i] > 0;

            var faceData = GameState.Instance != null ? GameState.Instance.GetFaceData(i) : null;
            data.faceVisited[i] = faceData != null && faceData.isVisited;
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"[SaveManager] 저장 완료! (층 {data.currentFloor}, 면 {data.currentFaceIndex}) → {SavePath}");
    }

    /// <summary>세이브 파일을 읽어서 SaveData로 돌려줍니다. 파일이 없으면 null.</summary>
    public SaveData Load()
    {
        if (!HasSaveFile())
        {
            Debug.LogWarning("[SaveManager] 로드할 세이브 파일이 없습니다.");
            return null;
        }

        string json = File.ReadAllText(SavePath);
        var data = JsonUtility.FromJson<SaveData>(json);
        Debug.Log($"[SaveManager] 로드 완료! (층 {data.currentFloor}, 면 {data.currentFaceIndex})");
        return data;
    }

    /// <summary>[[17 게임오버 & 리트라이]] "처음부터 재시작"을 나중에 구현할 때, 또는 디버그용으로 씁니다.</summary>
    public void DeleteSave()
    {
        if (!HasSaveFile()) return;
        File.Delete(SavePath);
        Debug.Log("[SaveManager] 세이브 파일 삭제됨.");
    }

    private float GetPlayerHP()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        var health = player != null ? player.GetComponent<PlayerHealth>() : null;
        return health != null ? health.CurrentHP : 0f;
    }
}
