using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Meokgoeeum
{
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

        [Tooltip("[[15 튜토리얼 설계]] \"마우스 좌클릭 — 공격\" 힌트(첫 먹괴음 감지 시)를 이 스포너의 1웨이브 시작 시 띄울지 여부입니다. 이 스크립트는 여러 층에서 재사용되므로 기본은 꺼짐 — 1층 튜토리얼 스포너에서만 켭니다.")]
        public bool showFirstAttackHint = false;

        // 세션 전체에서 한 번만 뜨면 되는 힌트라 static — 여러 층을 오가도 두 번 안 뜸.
        private static bool hasShownFirstAttackHint;

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
            // 2026-09-02: 보스 단독 테스트하려고 이 오브젝트를 Hierarchy에서 꺼둔 상태로
            // 체크포인트를 지나면 StartCoroutine이 예외를 던지며 시끄러운 에러 로그가 남고,
            // 체크포인트 쪽의 이후 로직(disablesTutorialSkip 등)까지 같이 끊겨버렸음.
            // 비활성 상태면 조용히 무시하도록 방어.
            if (!isActiveAndEnabled)
            {
                Debug.LogWarning($"[EncounterSpawner] {name}이 비활성 상태라 인카운터를 시작하지 않습니다.");
                return;
            }
            StartCoroutine(RunWaves());
        }

        private IEnumerator RunWaves()
        {
            for (int i = 0; i < waves.Length; i++)
            {
                SpawnWave(waves[i]);

                if (i == 0 && showFirstAttackHint && !hasShownFirstAttackHint)
                {
                    if (HintPopupManager.Instance != null)
                    {
                        hasShownFirstAttackHint = true; // 실제로 뜬 경우에만 소모 — null이면 다음 기회에 다시 시도
                        HintPopupManager.Instance.ShowHint("마우스 좌클릭 — 공격");
                        Debug.Log("[EncounterSpawner] 좌클릭 공격 힌트 표시함.");
                    }
                    else
                    {
                        Debug.LogWarning("[EncounterSpawner] HintPopupManager.Instance가 아직 없어서 좌클릭 공격 힌트를 못 띄웠습니다 (초기화 순서 문제일 수 있음).");
                    }
                }

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

                // ⚠️ 2026-08-24 버그 수정: parent를 안 주면 "지금 활성 씬"에 생성되는데, Addressables로
                // Additive 로드된 면 씬은 자동으로 활성 씬이 되지 않습니다(CubeMapManager가 명시적으로
                // SetActiveScene을 해도 이 스크립트의 Start()가 그보다 먼저 실행되는 타이밍 문제가 있음).
                // 그래서 스포너 자신(transform)을 부모로 명시해서, 활성 씬이 뭐든 상관없이 항상
                // 이 스포너와 같은 씬(=지금 방문 중인 면)에 생성되도록 고정합니다. 안 그러면 면을
                // 나가도 이 적들이 안 없어지고 계속 쌓입니다([[RandomEncounterSpawner]] 테스트 중 발견).
                GameObject instance = Instantiate(wave.enemyPrefabs[i], spawnPoint.position, spawnPoint.rotation, transform);

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
}
