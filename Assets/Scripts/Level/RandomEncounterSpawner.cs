using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RandomEncounterSpawner (무작위 인카운터 스포너)
/// [[EncounterSpawner]]는 1~6층처럼 "이 층엔 이 조합"을 손으로 고정 배치하는 용도인데,
/// [[CubeMapManager]]는 6면을 전부 방문한 뒤(7번째 회전부터)엔 다음 면을 완전 무작위로
/// 고릅니다 — 즉 "7층엔 반드시 이 면"처럼 층 번호에 특정 콘텐츠를 고정 배정할 방법이 없습니다.
///
/// 그래서 재방문용 콘텐츠는 층 번호를 맞추려 하지 않고, 그 시점까지 구현된 먹괴음 풀에서
/// 무작위로 N마리를 뽑아 스폰하는 쪽으로 단순화했습니다. 같은 종류가 몰리지 않도록
/// 종류당 최대 마릿수를 제한합니다.
///
/// [[RoomClearGate]]는 "범위 안에 EnemyHealth가 남아있는가"만 물리 쿼리로 검사하므로,
/// 누가 어떻게 스폰했는지 몰라도 그대로 맞물립니다(EncounterSpawner와 동일한 이유).
/// </summary>
public class RandomEncounterSpawner : MonoBehaviour
{
    [Tooltip("여기서 무작위로 뽑습니다. (평/원/흡/분/광 프리팹을 등록해두면 됨 — 아직 안 풀린 종류는 빼두면 됨)")]
    public GameObject[] enemyPool;

    [Tooltip("이번 인카운터에서 스폰할 총 마릿수입니다.")]
    public int spawnCount = 3;

    [Tooltip("같은 종류가 최대 몇 마리까지 중복 등장할 수 있는지입니다.")]
    public int maxDuplicatesPerType = 2;

    [Tooltip("이 반경 안에 스폰 위치를 무작위로 흩뿌립니다 (스포너 위치 기준, 층마다 스폰 포인트를 따로 안 만들어도 되게 하려고 이렇게 처리).")]
    public float scatterRadius = 3f;

    [Tooltip("씬 시작과 동시에 자동으로 스폰할지 여부입니다. 꺼두면 StartEncounter()를 직접 호출해야 합니다.")]
    public bool autoStart = true;

    [Tooltip("이 면을 최소 몇 번 방문한 뒤부터 스폰할지입니다. 기본 1 = 첫 방문(1~6층 튜토리얼/기획 페이싱)엔 안 나오고 재방문부터 나옴. 0으로 하면 첫 방문부터도 나옵니다.")]
    public int minVisitCount = 1;

    private void Start()
    {
        if (autoStart) StartEncounter();
    }

    public void StartEncounter()
    {
        if (!HasReachedMinVisitCount())
        {
            Debug.Log($"[RandomEncounterSpawner] {name}: 아직 재방문 조건(minVisitCount={minVisitCount})을 채우지 못해 스폰을 건너뜁니다.");
            return;
        }

        SpawnRandomEncounter();
    }

    /// <summary>
    /// 이 씬이 몇 번째 방문인지 [[CubeMapManager]]/[[GameState]]에서 조회합니다. 대상을 못 찾으면
    /// (예: 이 컴포넌트만 떼서 단독 테스트하는 경우) 안전하게 "조건 충족"으로 처리해서 스폰합니다 —
    /// 게이팅 실패로 조용히 아무 일도 안 일어나는 것보다 눈에 보이는 게 디버깅에 낫습니다.
    /// </summary>
    private bool HasReachedMinVisitCount()
    {
        if (CubeMapManager.Instance == null || GameState.Instance == null)
        {
            Debug.LogWarning($"[RandomEncounterSpawner] {name}: CubeMapManager/GameState를 못 찾아 재방문 여부를 확인할 수 없습니다 — 그냥 스폰합니다.");
            return true;
        }

        var faceData = GameState.Instance.GetFaceData(CubeMapManager.Instance.CurrentFaceIndex);
        if (faceData == null)
        {
            Debug.LogWarning($"[RandomEncounterSpawner] {name}: 현재 면의 CubeFaceData를 못 찾아 재방문 여부를 확인할 수 없습니다 — 그냥 스폰합니다.");
            return true;
        }

        // MarkVisited()가 이 씬의 Start()보다 나중에 호출되므로, 여기서 읽는 visitCount는
        // "이번 방문 포함 전"의 값입니다 — 즉 0이면 이번이 첫 방문, 1이면 이번이 두 번째(첫 재방문).
        return faceData.visitCount >= minVisitCount;
    }

    private void SpawnRandomEncounter()
    {
        if (enemyPool == null || enemyPool.Length == 0)
        {
            Debug.LogWarning($"[RandomEncounterSpawner] {name}: Enemy Pool이 비어있어 아무것도 스폰하지 않습니다.");
            return;
        }

        // 뽑을 때마다 이 목록에서 고르고, 중복 한도에 도달한 종류는 여기서 제거합니다.
        var pickable = new List<GameObject>(enemyPool);
        var spawnedCount = new Dictionary<GameObject, int>();
        int actuallySpawned = 0;

        for (int i = 0; i < spawnCount; i++)
        {
            if (pickable.Count == 0)
            {
                Debug.LogWarning($"[RandomEncounterSpawner] {name}: 중복 제한 때문에 더 뽑을 종류가 없어서 {actuallySpawned}/{spawnCount}마리만 스폰합니다. (Enemy Pool 종류 수 × 중복 한도 < 스폰 마릿수인지 확인)");
                break;
            }

            GameObject prefab = pickable[Random.Range(0, pickable.Count)];
            Vector2 offset = Random.insideUnitCircle * scatterRadius;
            Vector3 spawnPos = transform.position + new Vector3(offset.x, 0f, offset.y);

            // ⚠️ 2026-08-24 버그 수정: parent를 안 주면 "지금 활성 씬"에 생성되는데, Addressables로
            // Additive 로드된 면 씬은 자동으로 활성 씬이 되지 않아서(자세한 이유는 EncounterSpawner.cs
            // 주석 참고) 면을 나가도 이 적들이 안 없어지고 계속 쌓이는 버그가 있었습니다 — 이 버그를
            // 실제로 발견한 계기가 이 스크립트의 재방문 테스트였습니다.
            Instantiate(prefab, spawnPos, Quaternion.identity, transform);
            actuallySpawned++;

            spawnedCount.TryGetValue(prefab, out int count);
            count++;
            spawnedCount[prefab] = count;

            if (count >= maxDuplicatesPerType)
                pickable.Remove(prefab); // 이번 인카운터에서 이 종류는 한도 도달 — 더 안 뽑히게
        }

        Debug.Log($"[RandomEncounterSpawner] {name}: 무작위 인카운터 스폰 완료 ({actuallySpawned}마리, 풀 {enemyPool.Length}종)");
    }
}
