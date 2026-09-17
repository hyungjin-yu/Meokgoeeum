using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Meokgoeeum
{
    /// <summary>
    /// DebugSpawnPanel (테스트용 — 원하는 몹 소환 + 색 구슬 종류/개수 지급 패널)
    /// [[DebugOrbCheat]](F1~F3 고정 1개 지급)보다 더 유연한 QA 도구가 필요해서 추가 —
    /// 원하는 종을 골라 플레이어 앞에 소환하고, 원하는 색 구슬을 원하는 개수만큼 지급할 수 있습니다.
    ///
    /// 몹 소환은 [[EncounterSpawner]]가 웨이브 스폰에 쓰는 것과 동일한 [[CubeEnemyConverter]]를
    /// 재사용합니다 — 평지용 프리팹을 그대로 넣어도 자동으로 큐브 면 버전(CubeEnemy*)으로
    /// 변환됩니다. `target`(CubeSurfaceWalker)만 있으면 cubeCenter/cubeHalfExtent/현재 면
    /// 법선은 전부 거기서 그대로 가져와서 별도로 안 맞춰도 됩니다.
    ///
    /// 릴리즈 빌드엔 안 들어가도록 UNITY_EDITOR/DEVELOPMENT_BUILD로 감쌌습니다 —
    /// [[DebugOrbCheat]]와 동일한 관례입니다.
    /// </summary>
    #if UNITY_EDITOR || DEVELOPMENT_BUILD
    public class DebugSpawnPanel : MonoBehaviour
    {
        [Header("연결")]
        [Tooltip("비워두면 Awake()에서 씬의 CubeSurfaceWalker를 자동으로 찾습니다.")]
        public CubeSurfaceWalker target;

        [Header("몹 프리팹 (평지용 원본 그대로 — 자동으로 큐브 면 버전으로 변환됨)")]
        public GameObject pyeongPrefab;
        public GameObject wonPrefab;
        public GameObject heupPrefab;
        public GameObject bunPrefab;
        public GameObject gwangPrefab;

        [Tooltip("플레이어 정면 이 거리(유닛)에 소환합니다. 정확한 면 좌표가 아니어도 " +
                 "CubeEnemyConverter가 스폰 뒤 표면에 재보정합니다.")]
        public float spawnDistance = 4f;

        [Tooltip("이 키로 패널을 열고 닫습니다.")]
        public Key toggleKey = Key.F5;

        private bool visible;
        private readonly Dictionary<OrbColor, string> orbCountInputs = new Dictionary<OrbColor, string>();

        private void Awake()
        {
            if (target == null)
                target = FindFirstObjectByType<CubeSurfaceWalker>();

            foreach (OrbColor color in Enum.GetValues(typeof(OrbColor)))
                orbCountInputs[color] = "1";
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
                visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible) return;

            GUILayout.BeginArea(new Rect(10, 10, 260, 420), GUI.skin.box);
            GUILayout.Label($"디버그 스폰 패널 ({toggleKey}로 닫기)");

            GUILayout.Space(8);
            GUILayout.Label("몹 소환");
            SpawnButton("평", pyeongPrefab);
            SpawnButton("원", wonPrefab);
            SpawnButton("흡", heupPrefab);
            SpawnButton("분", bunPrefab);
            SpawnButton("광", gwangPrefab);
            if (target == null)
                GUILayout.Label("⚠ CubeSurfaceWalker를 못 찾음 — 소환 불가");

            GUILayout.Space(8);
            GUILayout.Label("색 구슬 지급");
            foreach (OrbColor color in Enum.GetValues(typeof(OrbColor)))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(color.ToString(), GUILayout.Width(60));
                orbCountInputs[color] = GUILayout.TextField(orbCountInputs[color], GUILayout.Width(40));
                if (GUILayout.Button("지급", GUILayout.Width(50)))
                    GiveOrbs(color, orbCountInputs[color]);
                GUILayout.EndHorizontal();
            }
            if (ColorSystemManager.Instance == null)
                GUILayout.Label("⚠ ColorSystemManager를 못 찾음 — 지급 불가");

            GUILayout.EndArea();
        }

        private void SpawnButton(string label, GameObject prefab)
        {
            GUI.enabled = prefab != null && target != null;
            if (GUILayout.Button($"{label} 소환"))
                SpawnMob(prefab);
            GUI.enabled = true;
        }

        private void SpawnMob(GameObject prefab)
        {
            if (prefab == null || target == null) return;

            // 정확한 면 좌표를 몰라도 됨 — CubeEnemyConverter.ConvertToCubeMode()가 스폰 뒤
            // faceNormal 기준 수학적 표면에 정확히 재보정해줍니다.
            Vector3 spawnPos = target.transform.position + target.transform.forward * spawnDistance;
            Quaternion spawnRot = Quaternion.LookRotation(-target.transform.forward, target.CurrentSurfaceNormal);

            GameObject instance = Instantiate(prefab, spawnPos, spawnRot);
            var cubeEnemy = CubeEnemyConverter.ConvertToCubeMode(
                instance, target.cubeCenter, target.CurrentSurfaceNormal, target.cubeHalfExtent, target);

            Debug.Log(cubeEnemy != null
                ? $"[DebugSpawnPanel] {prefab.name} 소환함 (위치: {instance.transform.position})"
                : $"[DebugSpawnPanel] {prefab.name} 소환 실패 — 콘솔의 CubeEnemyConverter 경고 참고");
        }

        private void GiveOrbs(OrbColor color, string countText)
        {
            if (ColorSystemManager.Instance == null) return;
            if (!int.TryParse(countText, out int count) || count <= 0)
            {
                Debug.LogWarning($"[DebugSpawnPanel] \"{countText}\"은(는) 유효한 개수가 아닙니다 — 1 이상의 정수를 입력하세요.");
                return;
            }

            for (int i = 0; i < count; i++)
                ColorSystemManager.Instance.AddOrb(color);
        }
    }
    #endif
}
