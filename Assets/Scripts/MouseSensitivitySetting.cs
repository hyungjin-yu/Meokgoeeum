using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// MouseSensitivitySetting (마우스 감도 설정)
///
/// Cinemachine의 Gain(20)은 디자이너가 튜닝해둔 "기본 감도"이고 건드리지 않습니다.
/// 그 위에 Look 입력 액션의 ScaleVector2 프로세서를 배율(곱셈)로 걸어서,
/// 이 배율만 플레이어가 설정에서 바꿀 수 있게 분리했습니다.
///
/// 나중에 설정 UI(슬라이더 등)가 생기면 SetSensitivity()만 호출하면 됩니다.
/// </summary>
public class MouseSensitivitySetting : MonoBehaviour
{
    private const string PrefsKey = "MouseSensitivity";
    private const float DefaultSensitivity = 1f;
    private const float MinSensitivity = 0.1f;
    private const float MaxSensitivity = 3f;

    private PlayerInputActions inputActions;

    private void Awake()
    {
        inputActions = new PlayerInputActions();

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
    /// Look 액션의 ScaleVector2 프로세서 파라미터(x, y)를 실시간으로 덮어씁니다.
    /// (프로세서 자체는 PlayerInputActions.inputactions에 미리 등록돼 있어야 합니다 — 지금 있음)
    /// </summary>
    private void ApplySensitivity(float sensitivity)
    {
        inputActions.Player.Look.ApplyParameterOverride("scaleVector2:x", sensitivity);
        inputActions.Player.Look.ApplyParameterOverride("scaleVector2:y", sensitivity);
    }
}
