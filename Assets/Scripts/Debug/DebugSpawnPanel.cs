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

        // ⚠️ 2026-09-17 발견 — 유니티 기본 GUI 스킨 폰트(Arial 계열)는 한글 글리프가 없어서,
        // OnGUI로 그린 한글 라벨/버튼 텍스트가 전부 빈 상자로 보였습니다("패널이 아예 안 보이는데"
        // 리포트 — 실제로는 패널 자체는 그려졌는데 글자만 안 보였던 것). OS에 이미 설치돼있는
        // 한글 폰트(맑은 고딕)를 동적으로 불러와 커스텀 스타일에 적용해서 해결.
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle textFieldStyle;
        private GUIStyle boxStyle;

        private void Awake()
        {
            if (target == null)
                target = FindFirstObjectByType<CubeSurfaceWalker>();

            foreach (OrbColor color in Enum.GetValues(typeof(OrbColor)))
                orbCountInputs[color] = "1";
        }

        private void EnsureStyles()
        {
            if (labelStyle != null) return;

            Font koreanFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 14);

            labelStyle = new GUIStyle(GUI.skin.label) { font = koreanFont };
            buttonStyle = new GUIStyle(GUI.skin.button) { font = koreanFont };
            textFieldStyle = new GUIStyle(GUI.skin.textField) { font = koreanFont };
            boxStyle = new GUIStyle(GUI.skin.box) { font = koreanFont };
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current[toggleKey].wasPressedThisFrame) return;

            visible = !visible;

            // 2026-09-17 발견 — 평소엔 [[CubeSurfaceCamera]]가 마우스 룩 조작을 위해 커서를
            // 잠그고 숨겨둡니다("커서가 안 보이고 클릭이 안 된다" 리포트로 발견) — 패널을 열 때는
            // 풀어서 보이게/클릭 가능하게 하고, 닫을 때 다시 잠가서 원래 조작으로 복귀시킵니다.
            if (visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnGUI()
        {
            if (!visible) return;
            EnsureStyles();

            GUILayout.BeginArea(new Rect(10, 10, 260, 420), boxStyle);
            GUILayout.Label($"디버그 스폰 패널 ({toggleKey}로 닫기)", labelStyle);

            GUILayout.Space(8);
            GUILayout.Label("몹 소환", labelStyle);
            SpawnButton("평", pyeongPrefab);
            SpawnButton("원", wonPrefab);
            SpawnButton("흡", heupPrefab);
            SpawnButton("분", bunPrefab);
            SpawnButton("광", gwangPrefab);
            if (target == null)
                GUILayout.Label("⚠ CubeSurfaceWalker를 못 찾음 — 소환 불가", labelStyle);

            GUILayout.Space(8);
            GUILayout.Label("색 구슬 지급", labelStyle);
            foreach (OrbColor color in Enum.GetValues(typeof(OrbColor)))
            {
                // ⚠️ 2026-09-17 — Dictionary는 유니티가 직렬화 못 하는 타입이라, Play 모드 중
                // 스크립트를 재컴파일하면(도메인 리로드) Awake()가 다시 안 불려서 내용이
                // 비어버릴 수 있음(KeyNotFoundException으로 실제 재현함) — 없으면 즉석에서 채움.
                if (!orbCountInputs.ContainsKey(color)) orbCountInputs[color] = "1";

                GUILayout.BeginHorizontal();
                GUILayout.Label(color.ToString(), labelStyle, GUILayout.Width(60));
                orbCountInputs[color] = GUILayout.TextField(orbCountInputs[color], textFieldStyle, GUILayout.Width(40));
                if (ManualButton("지급", buttonStyle, GUILayout.Width(50)))
                    GiveOrbs(color, orbCountInputs[color]);
                GUILayout.EndHorizontal();
            }
            if (ColorSystemManager.Instance == null)
                GUILayout.Label("⚠ ColorSystemManager를 못 찾음 — 지급 불가", labelStyle);

            GUILayout.EndArea();
        }

        private void SpawnButton(string label, GameObject prefab)
        {
            GUI.enabled = prefab != null && target != null;
            if (ManualButton($"{label} 소환", buttonStyle))
                SpawnMob(prefab);
            GUI.enabled = true;
        }

        /// <summary>
        /// ⚠️ 2026-09-17 발견 — 이 프로젝트는 Active Input Handling이 "Input System Package
        /// (New)" 전용(activeInputHandler=1)인데, 이 모드에서는 레거시 OnGUI의 클릭 판정
        /// (Event 기반 MouseDown/Up)이 제대로 안 먹는 알려진 유니티 문제가 있습니다 — 마우스
        /// 위치 추적(호버 하이라이트)은 정상 작동해서 "버튼은 보이는데 눌러도 반응이 없다"는
        /// 형태로 나타남(실측 영상으로 확인: 호버는 되는데 클릭 로그가 한 번도 안 찍힘).
        /// 프로젝트 전역 설정(Both로 변경)을 건드리는 대신, 이 패널 안에서만 New Input System의
        /// Mouse로 직접 클릭을 판정합니다 — GUILayout.Button은 그리기/호버용으로만 쓰고, 실제
        /// 클릭 여부는 방금 그려진 영역(GetLastRect)에 마우스가 있는 상태에서 이번 프레임에
        /// 눌렸는지로 직접 계산합니다.
        /// </summary>
        private bool ManualButton(string label, GUIStyle style, params GUILayoutOption[] options)
        {
            GUILayout.Button(label, style, options);
            if (Event.current.type != EventType.Repaint) return false;

            Rect rect = GUILayoutUtility.GetLastRect();
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return false;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector2 guiMousePos = new Vector2(mousePos.x, Screen.height - mousePos.y); // Input System은 아래가 0, GUI는 위가 0
            return GUI.enabled && rect.Contains(guiMousePos);
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

        // ⚠️ 2026-09-17 발견 — 개수 입력창에 "901" 같은 큰 값을 넣으면 AddOrb()를 한 프레임에
        // 900번 넘게 동기 호출해서 그 프레임이 심하게 버벅였고(콘솔 로그 900줄), 그 큰
        // Time.deltaTime 때문에 근처 몹(흡)이 플레이어 코앞까지 순간 이동하듯 파고드는 버그로
        // 이어짐([[CubeEnemyBase.MoveToward]] 쪽도 클램프로 같이 수정). 여기서도 애초에 비정상
        // 개수를 못 넣게 막아서 원천 차단.
        private const int MaxOrbGiveCount = 20;

        private void GiveOrbs(OrbColor color, string countText)
        {
            if (ColorSystemManager.Instance == null) return;
            if (!int.TryParse(countText, out int count) || count <= 0)
            {
                Debug.LogWarning($"[DebugSpawnPanel] \"{countText}\"은(는) 유효한 개수가 아닙니다 — 1 이상의 정수를 입력하세요.");
                return;
            }

            if (count > MaxOrbGiveCount)
            {
                Debug.LogWarning($"[DebugSpawnPanel] {count}개는 너무 많습니다(최대 {MaxOrbGiveCount}) — 한 프레임에 너무 많이 호출되면 버벅임/버그로 이어질 수 있어 막습니다.");
                count = MaxOrbGiveCount;
            }

            for (int i = 0; i < count; i++)
                ColorSystemManager.Instance.AddOrb(color);
        }
    }
    #endif
}
