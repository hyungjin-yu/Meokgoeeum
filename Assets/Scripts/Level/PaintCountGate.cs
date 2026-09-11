using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// PaintCountGate (색칠 개수 게이트)
    /// [[PaintableObject]] 여러 개를 등록해두고, 전부 색 복원되면 문(오브젝트)을 비활성화해서
    /// 통로를 엽니다. [[changelog/2026-09-11_플레이타임-확장-스코프결정]]의 1층 방B
    /// "색 복원 라이트 퍼즐"에서 씀 — 순서는 상관없이 전부 칠하면 열리는 단순한 형태입니다.
    ///
    /// [[HiddenOrbSpawner]]가 "복원 1개 → 구슬 1개 등장"인 것과 달리, 이건 "복원 N개 → 문 1개
    /// 열림"이라 별도로 만들었습니다. 둘 다 PaintableObject.OnPainted 이벤트를 그대로 재사용해서
    /// PaintableObject 자체는 안 건드립니다.
    /// </summary>
    public class PaintCountGate : MonoBehaviour
    {
        [Tooltip("전부 색 복원되어야 문이 열리는 대상들입니다.")]
        public PaintableObject[] triggers;

        [Tooltip("전부 칠해지면 비활성화할 문(벽) 오브젝트입니다. 여러 개면 전부 비활성화합니다.")]
        public GameObject[] doorsToOpen;

        private int paintedCount;
        private bool opened;

        private void Start()
        {
            if (triggers == null || triggers.Length == 0)
            {
                Debug.LogWarning($"[PaintCountGate] {name}: triggers가 비어있어 절대 열리지 않습니다.");
                return;
            }

            foreach (var t in triggers)
            {
                if (t != null) t.OnPainted += HandlePainted;
            }
        }

        private void HandlePainted()
        {
            if (opened) return;

            paintedCount++;
            Debug.Log($"[PaintCountGate] {name}: {paintedCount}/{triggers.Length} 복원됨");

            if (paintedCount >= triggers.Length)
                OpenGate();
        }

        private void OpenGate()
        {
            opened = true;
            Debug.Log($"[PaintCountGate] {name}: 전부 복원됨 — 문 열림");

            if (doorsToOpen == null) return;
            foreach (var door in doorsToOpen)
            {
                if (door != null) door.SetActive(false);
            }
        }
    }
}
