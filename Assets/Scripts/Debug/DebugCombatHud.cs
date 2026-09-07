using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// DebugCombatHud (테스트 전용 — 플레이어/보스 HP 화면 표시)
    /// 2026-09-02: "피가 안 줄어든다"는 리포트를 받았는데, 아직 HP바 UI가 없어서(v0.1 단계
    /// 콘솔 로그가 유일한 확인 수단) 실제로 안 줄어드는 버그인지 그냥 눈에 안 보여서 그런
    /// 건지 판단이 안 됨. 정식 HP바(아트 필요) 대신 OnGUI로 화면 왼쪽 위에 숫자만 바로
    /// 찍어서 즉시 확인할 수 있게 하는 디버그 전용 도구입니다.
    ///
    /// `FindObjectOfType`을 매 프레임 대신 일정 간격으로만 다시 찾습니다(최적화 원칙) — 씬
    /// 전환/보스 스폰 등으로 참조가 끊길 수 있어서 한 번만 캐싱하지 않고 주기적으로 갱신.
    /// </summary>
    public class DebugCombatHud : MonoBehaviour
    {
        public float refreshInterval = 1f;

        private PlayerHealth player;
        private BossHealth boss;
        private float timer;

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer < refreshInterval) return;
            timer = 0f;

            if (player == null) player = FindObjectOfType<PlayerHealth>();
            if (boss == null) boss = FindObjectOfType<BossHealth>();
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(10, 10, 260, boss != null ? 70 : 45), "");

            GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white } };

            string playerLine = player != null
                ? $"플레이어 HP: {player.CurrentHP:F0} / {player.maxHP:F0}"
                : "플레이어 HP: (못 찾음)";
            GUI.Label(new Rect(20, 15, 240, 25), playerLine, style);

            if (boss != null)
            {
                string bossLine = boss.IsDead
                    ? "보스 HP: 처치됨"
                    : $"보스 HP: {boss.CurrentHP:F0} / {boss.maxHP:F0} ({boss.HpRatio:P0})";
                GUI.Label(new Rect(20, 40, 240, 25), bossLine, style);
            }
        }
    }
}
