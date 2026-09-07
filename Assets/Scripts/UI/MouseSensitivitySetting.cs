using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

namespace Meokgoeeum
{
    /// <summary>
    /// MouseSensitivitySetting (마우스 감도 설정)
    ///
    /// ⚠️ 2026-08-25 재작성: 원래는 `new PlayerInputActions()`로 자체 인스턴스를 만들어서 그 위의
    /// Look 액션에 `ApplyParameterOverride`로 배율을 걸었지만, 실제로 카메라를 돌리는 건 [[Cinemachine]]의
    /// `CinemachineInputAxisController`가 **자기 자신의 `InputActionReference`로 원본 .inputactions
    /// 에셋을 직접 읽는 것**이었음 — 이 스크립트가 만든 인스턴스도, `PlayerController`가 만든 인스턴스도
    /// 전부 별개의 복제본이라 카메라가 실제로 읽는 값과 완전히 무관했음(슬라이더는 움직이는데 감도
    /// 체감이 전혀 없던 버그의 원인). 그래서 Input Action 쪽을 건드리는 대신, Cinemachine이 실제로
    /// 매 프레임 곱하는 `Controller.Input.Gain` 값 자체를 직접 조절하는 방식으로 바꿈 — 이건 카메라가
    /// 읽는 "최종 값"이라 인스턴스 불일치 문제가 원천적으로 생길 수 없음.
    /// </summary>
    public class MouseSensitivitySetting : MonoBehaviour
    {
        public static MouseSensitivitySetting Instance { get; private set; }

        private const string PrefsKey = "MouseSensitivity";
        private const float DefaultSensitivity = 1f;
        private const float MinSensitivity = 0.1f;
        private const float MaxSensitivity = 3f;

        private CinemachineInputAxisController axisController;

        // 디자이너가 Cinemachine 인스펙터에서 튜닝해둔 "기준 Gain"(예: 20, -20)을 컨트롤러별로 기억해뒀다가,
        // 감도 배율을 곱해서 적용합니다. Controller는 class라 참조로 계속 같은 인스턴스를 가리킵니다.
        private readonly Dictionary<object, float> baseGains = new Dictionary<object, float>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning("[MouseSensitivitySetting] 이미 인스턴스가 존재합니다. 중복 오브젝트를 파괴합니다.");
                Destroy(gameObject);
                return;
            }

            axisController = FindFirstObjectByType<CinemachineInputAxisController>();
            if (axisController == null)
            {
                Debug.LogWarning("[MouseSensitivitySetting] CinemachineInputAxisController를 씬에서 못 찾았습니다 — 감도 설정이 아무 효과가 없습니다.");
                return;
            }

            foreach (var controller in axisController.Controllers)
                baseGains[controller] = controller.Input.Gain;

            // 저장된 감도 값을 불러옵니다. 저장된 적 없으면 기본값(1 = 100%) 사용.
            float savedSensitivity = PlayerPrefs.GetFloat(PrefsKey, DefaultSensitivity);
            ApplySensitivity(savedSensitivity);
        }

        /// <summary>
        /// 설정 UI에서 호출할 함수입니다. 슬라이더 값(0.1~3.0)을 그대로 넘기면 됩니다.
        /// </summary>
        public void SetSensitivity(float sensitivity)
        {
            sensitivity = Mathf.Clamp(sensitivity, MinSensitivity, MaxSensitivity);
            PlayerPrefs.SetFloat(PrefsKey, sensitivity);
            ApplySensitivity(sensitivity);
        }

        public float GetCurrentSensitivity()
        {
            return PlayerPrefs.GetFloat(PrefsKey, DefaultSensitivity);
        }

        /// <summary>
        /// [[CinemachineInputAxisController]]의 각 Controller(Look Orbit X/Y 등)의 Gain을
        /// "기준 Gain × 배율"로 실시간 갱신합니다. 카메라가 매 프레임 실제로 곱하는 값이라
        /// 여기서 바꾸면 즉시 체감됩니다.
        /// </summary>
        private void ApplySensitivity(float sensitivity)
        {
            if (axisController == null) return;

            foreach (var controller in axisController.Controllers)
            {
                if (!baseGains.TryGetValue(controller, out float baseGain)) continue;
                controller.Input.Gain = baseGain * sensitivity;
            }
        }
    }
}
