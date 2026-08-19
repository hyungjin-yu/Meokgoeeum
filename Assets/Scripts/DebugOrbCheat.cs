using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// DebugOrbCheat (테스트용 — 구슬 강제 지급 + 큐브 회전 수동 트리거)
/// 색 스킬 QA를 "적을 잡아서 원하는 색이 랜덤으로 나오길" 기다리지 않고 바로 할 수 있게
/// 하는 개발용 치트입니다. F1=빨강, F2=파랑, F3=노랑 구슬을 즉시 1개 지급합니다.
/// F4는 [[CubeMapManager]]의 면 회전을 수동으로 발동합니다 — 아직 색 구슬 획득에
/// 자동으로 연결하지 않았으니(전투 흐름을 끊을 수 있어서 판단 보류 중) 지금은 이걸로 테스트.
///
/// 릴리즈 빌드엔 안 들어가도록 UNITY_EDITOR/DEVELOPMENT_BUILD로 감쌌습니다 —
/// 에디터에서 플레이할 때와 "Development Build" 체크한 빌드에서만 동작합니다.
/// </summary>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
public class DebugOrbCheat : MonoBehaviour
{
    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (ColorSystemManager.Instance != null)
        {
            if (keyboard.f1Key.wasPressedThisFrame) ColorSystemManager.Instance.AddOrb(OrbColor.Red);
            if (keyboard.f2Key.wasPressedThisFrame) ColorSystemManager.Instance.AddOrb(OrbColor.Blue);
            if (keyboard.f3Key.wasPressedThisFrame) ColorSystemManager.Instance.AddOrb(OrbColor.Yellow);
        }

        if (keyboard.f4Key.wasPressedThisFrame) CubeMapManager.Instance?.RotateRandomAxis();
    }
}
#endif
