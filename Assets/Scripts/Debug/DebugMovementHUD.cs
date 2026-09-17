using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Meokgoeeum
{
    /// <summary>
    /// DebugMovementHUD (테스트용 — 플레이어 이동 거리 + 흡과의 실시간 거리 표시)
    /// 사용자가 "흡한테 꼈다"고 리포트했을 때, [[CubeEnemyHeup]]의 흡수-해제 로직(1.5유닛
    /// 이상 벌어지면 놓아줌) 자체는 리플렉션 테스트로 정상 동작 확인했지만, 실제 플레이 중에
    /// 정말로 그만큼 벌어졌는지는 서로 말로만 우겨서는 확인이 안 됨 — 화면에 실시간 숫자로
    /// 띄워서 눈으로 직접 보고 판단하기 위해 추가.
    ///
    /// 그 버그([[changelog/2026-09-17_큐브몹-이동오버슈트버그]])는 이미 찾아서 고쳤고, 이제
    /// 실제 게임 HUD([[PlayerHUD]])가 화면 우측 상단을 가리므로 기본은 꺼둠 — [[DebugSpawnPanel]]과
    /// 동일하게 토글 키(F6)로 필요할 때만 켬.
    ///
    /// 릴리즈 빌드엔 안 들어가도록 UNITY_EDITOR/DEVELOPMENT_BUILD로 감쌌습니다 —
    /// [[DebugOrbCheat]]/[[DebugSpawnPanel]]과 동일한 관례.
    /// </summary>
    #if UNITY_EDITOR || DEVELOPMENT_BUILD
    public class DebugMovementHUD : MonoBehaviour
    {
        [Tooltip("비워두면 Awake()에서 씬의 CubeSurfaceWalker를 자동으로 찾습니다.")]
        public CubeSurfaceWalker target;

        public Key toggleKey = Key.F6;

        private bool visible;
        private Vector3 lastPosition;
        private float totalDistance;
        private GUIStyle labelStyle;

        private void Awake()
        {
            if (target == null)
                target = FindFirstObjectByType<CubeSurfaceWalker>();

            if (target != null)
                lastPosition = target.transform.position;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
                visible = !visible;

            if (target == null) return;

            // 면이 바뀌는 순간(모서리를 넘을 때)은 좌표계가 재정렬되면서 실제로 안 움직였는데도
            // 거리가 튀는 걸 막기 위해, 이번 프레임 실제로 이동한 거리가 비정상적으로 크면
            // (예: 한 프레임에 5유닛 이상) 면 전환으로 보고 누적에서 제외합니다.
            Vector3 current = target.transform.position;
            float delta = Vector3.Distance(current, lastPosition);
            if (delta < 5f) totalDistance += delta;
            lastPosition = current;
        }

        private void EnsureStyle()
        {
            if (labelStyle != null) return;
            Font koreanFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 13);
            labelStyle = new GUIStyle(GUI.skin.label) { font = koreanFont, normal = { textColor = Color.white } };
        }

        private void OnGUI()
        {
            if (!visible || target == null) return;
            EnsureStyle();

            GUILayout.BeginArea(new Rect(Screen.width - 280, 10, 270, 200), GUI.skin.box);
            GUILayout.Label($"누적 이동 거리: {totalDistance:F2}", labelStyle);
            GUILayout.Label($"현재 좌표: {target.transform.position:F2}", labelStyle);

            // 큐브 면 위의 모든 흡(Heup)과의 실시간 거리 + 상태 — 이 값이 흡수-해제 판정의
            // 실제 근거(absorbRadius=1.5)이므로, 이걸 보면 "정말 안 벌어졌는지" 바로 확인됩니다.
            var heups = FindObjectsByType<CubeEnemyHeup>(FindObjectsSortMode.None);
            if (heups.Length == 0)
            {
                GUILayout.Label("흡: 없음", labelStyle);
            }
            else
            {
                foreach (var heup in heups)
                {
                    float dist = Vector3.Distance(heup.transform.position, target.transform.position);
                    GUILayout.Label($"흡 거리: {dist:F2} (absorbRadius={heup.absorbRadius})", labelStyle);
                }
            }

            GUILayout.EndArea();
        }
    }
    #endif
}
