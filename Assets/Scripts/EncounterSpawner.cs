using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EncounterSpawner (인카운터 웨이브 스포너)
/// [[28 레벨 디자인 - 인카운터 페이싱과 블록아웃]]의 웨이브 패턴 구현
/// (예: 1층 "1웨이브: 평×1 HP 절반 고정, 처치 시 2초 인터벌 → 2웨이브: 평×1 정상 HP").
///
/// [[RoomClearGate]]와 자연스럽게 맞물립니다 — 이 스포너가 웨이브를 순차 생성하고,
/// RoomClearGate는 "지금 이 순간 범위 안에 살아있는 적이 있는가"만 주기적으로 검사하므로,
/// 마지막 웨이브까지 전부 죽어야만 계단이 열립니다. 별도 "웨이브 완료" 이벤트 연동 없이도
/// 두 스크립트가 그냥 같이 두면 맞아떨어지도록 설계했습니다.
/// </summary>
public class EncounterSpawner : MonoBehaviour
{
    [System.Serializable]
    public class Wave
    {
        public string waveName = "웨이브";

        [Tooltip("이 웨이브에서 생성할 적 프리팹들입니다. spawnPoints와 인덱스가 대응합니다.")]
        public GameObject[] enemyPrefabs;

        [Tooltip("각 적이 생성될 위치입니다. enemyPrefabs와 인덱스가 대응합니다 (개수가 다르면 남는 쪽은 이 오브젝트 위치를 씀).")]
        public Transform[] spawnPoints;

        [Tooltip("체력 배율입니다. 1=프리팹 기본값 그대로, 0.5=절반 (예: 1층 1웨이브 'HP 절반 고정').")]
        public float hpMultiplier = 1f;
    }

    [Tooltip("순서대로 진행할 웨이브 목록입니다.")]
    public Wave[] waves;

    [Tooltip("한 웨이브를 클리어한 뒤 다음 웨이브가 나오기까지의 대기 시간입니다.")]
    public float waveInterval = 2f;

    [Tooltip("씬 시작과 동시에 자동으로 첫 웨이브를 시작할지 여부입니다. 꺼두면 StartEncounter()를 직접 호출해야 합니다.")]
    public bool autoStart = true;

    private readonly List<EnemyHealth> aliveInCurrentWave = new List<EnemyHealth>();

    private void Start()
    {
        // 씬 중복으로 EncounterSpawner가 두 개 이상 동시에 로드돼 있는지 진단
        var allSpawners = FindObjectsByType<EncounterSpawner>(FindObjectsSortMode.None);
        if (allSpawners.Length > 1)
        {
            Debug.LogWarning($"[EncounterSpawner] 현재 씬에 EncounterSpawner가 {allSpawners.Length}개 동시에 존재합니다! (SC_Face_0/SC_Face_1이 동시에 로드돼 있거나, Spawner가 중복 배치된 경우일 수 있음)");
            foreach (var s in allSpawners)
                Debug.LogWarning($"[EncounterSpawner]   - {s.name} (InstanceID={s.GetInstanceID()}), 소속 씬: \"{s.gameObject.scene.name}\"");
        }

        if (autoStart) StartEncounter();
    }

    public void StartEncounter()
    {
        StartCoroutine(RunWaves());
    }

    private static string DescribePrefabRef(GameObject p)
    {
        if (p == null) return "null";
        string kind = p.scene.IsValid() ? $"⚠씬 오브젝트({p.scene.name})" : "프리팹 에셋(정상)";
        return $"{p.name}[{kind}]";
    }

    private IEnumerator RunWaves()
    {
        // 씬 중복(SC_Face_0 / SC_Face_1) 의심 상황 진단용 — 이 스포너가 실제로 어느 씬의
        // 어떤 인스턴스인지, 그리고 Inspector에 저장된 실제 웨이브 데이터가 뭔지 콘솔에서 바로 확인 가능하게 함.
        Debug.Log($"[EncounterSpawner] {name} (InstanceID={GetInstanceID()}) 시작 — 소속 씬: \"{gameObject.scene.name}\", 웨이브 개수: {(waves == null ? 0 : waves.Length)}");
        if (waves != null)
        {
            for (int w = 0; w < waves.Length; w++)
            {
                int prefabCount = waves[w].enemyPrefabs == null ? 0 : waves[w].enemyPrefabs.Length;
                // 씬 오브젝트(Hierarchy에서 잘못 끌어온 것)인지 진짜 프리팹 에셋(Project)인지 구분 —
                // 씬 오브젝트면 그게 파괴될 때 이 참조도 같이 null이 되는 버그의 원인이 됨
                string prefabNames = prefabCount == 0 ? "(없음)" : string.Join(", ", System.Array.ConvertAll(waves[w].enemyPrefabs, DescribePrefabRef));
                Debug.Log($"[EncounterSpawner]   웨이브[{w}] \"{waves[w].waveName}\" — enemyPrefabs({prefabCount}): {prefabNames}, hpMultiplier={waves[w].hpMultiplier}");
            }
        }

        for (int i = 0; i < waves.Length; i++)
        {
            SpawnWave(waves[i]);

            // 이번 웨이브의 모든 적이 죽을 때까지 대기 (Destroy()된 오브젝트는 Unity에서 null과 같음)
            yield return new WaitUntil(() => aliveInCurrentWave.TrueForAll(e => e == null));

            if (i < waves.Length - 1)
                yield return new WaitForSeconds(waveInterval);
        }

        Debug.Log($"[EncounterSpawner] {name}: 모든 웨이브 클리어!");
    }

    private void SpawnWave(Wave wave)
    {
        aliveInCurrentWave.Clear();
        Debug.Log($"[EncounterSpawner] {name}: \"{wave.waveName}\" 시작!");

        if (wave.enemyPrefabs == null || wave.enemyPrefabs.Length == 0)
            Debug.LogWarning($"[EncounterSpawner] {name}: \"{wave.waveName}\"에 Enemy Prefabs가 비어있습니다 — 이 웨이브는 아무것도 안 나오고 바로 클리어 처리됩니다. Inspector에서 확인하세요.");

        for (int i = 0; i < wave.enemyPrefabs.Length; i++)
        {
            if (wave.enemyPrefabs[i] == null)
            {
                Debug.LogWarning($"[EncounterSpawner] {name}: \"{wave.waveName}\"의 Enemy Prefabs[{i}]가 비어있어 건너뜁니다.");
                continue;
            }

            Transform spawnPoint = (wave.spawnPoints != null && i < wave.spawnPoints.Length && wave.spawnPoints[i] != null)
                ? wave.spawnPoints[i]
                : transform;

            GameObject instance = Instantiate(wave.enemyPrefabs[i], spawnPoint.position, spawnPoint.rotation);

            var health = instance.GetComponent<EnemyHealth>();
            if (health == null)
            {
                Debug.LogWarning($"[EncounterSpawner] {name}: \"{wave.waveName}\"에서 생성한 {instance.name}에 EnemyHealth가 없어 웨이브 클리어 판정에서 제외됩니다.");
                continue;
            }

            if (!Mathf.Approximately(wave.hpMultiplier, 1f))
                health.ConfigureMaxHP(health.maxHP * wave.hpMultiplier);

            aliveInCurrentWave.Add(health);
        }

        if (aliveInCurrentWave.Count == 0)
            Debug.LogWarning($"[EncounterSpawner] {name}: \"{wave.waveName}\"에서 실제로 생성된 적이 0마리라, 이 웨이브는 즉시 클리어 처리됩니다.");
    }
}
