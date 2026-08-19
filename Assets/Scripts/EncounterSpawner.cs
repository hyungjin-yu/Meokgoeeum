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
        if (autoStart) StartEncounter();
    }

    public void StartEncounter()
    {
        StartCoroutine(RunWaves());
    }

    private IEnumerator RunWaves()
    {
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

        for (int i = 0; i < wave.enemyPrefabs.Length; i++)
        {
            if (wave.enemyPrefabs[i] == null) continue;

            Transform spawnPoint = (wave.spawnPoints != null && i < wave.spawnPoints.Length && wave.spawnPoints[i] != null)
                ? wave.spawnPoints[i]
                : transform;

            GameObject instance = Instantiate(wave.enemyPrefabs[i], spawnPoint.position, spawnPoint.rotation);

            var health = instance.GetComponent<EnemyHealth>();
            if (health == null) continue;

            if (!Mathf.Approximately(wave.hpMultiplier, 1f))
                health.ConfigureMaxHP(health.maxHP * wave.hpMultiplier);

            aliveInCurrentWave.Add(health);
        }
    }
}
