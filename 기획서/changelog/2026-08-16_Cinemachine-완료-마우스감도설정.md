#changelog #먹괴음 #v0.1

# 2026-08-16 — Cinemachine 3인칭 카메라 완료 + 마우스 감도 설정 시스템

## 배경

[[logic/Cinemachine 3인칭 카메라 설정]] 가이드로 사용자가 직접 Unity 에디터에서 카메라를 설정하는 과정에서 여러 시행착오가 있었고, 최종적으로 실제 작동 확인까지 완료.

## 겪은 문제와 원인 (다음에 또 안 겪게 기록)

1. **"No cameras rendering"** — Main Camera 없이 Cinemachine 카메라부터 만들어서 Brain이 혼자 떨어진 오브젝트로 생성됨. → Main Camera를 먼저 만들고 그 위에 Brain을 `Add Component`로 직접 추가해야 함
2. **CinemachineCamera/Brain이 Player의 자식으로 잘못 들어감 (두 번 발생)** — Hierarchy에서 뭔가 선택된 채로 새 오브젝트를 만들면 Unity가 자동으로 그 자식으로 넣음. → 생성 전 항상 빈 곳 클릭해서 선택 해제
3. **Game 뷰에서 선을 드래그하면 카메라가 움직여서 "되는 줄" 착각** — 그건 Cinemachine이 아니라 Unity 에디터의 이동 툴 기즈모를 손으로 드래그한 것
4. **Play 모드에서 마우스 움직여도 반응 없음** — Game 뷰에 포커스가 안 잡혀있었음 (Inspector 등 다른 패널에 포커스)
5. **마우스는 반응하는데 너무 조금씩 움직임** — `Input Axis Controller`의 `Gain` 기본값이 너무 낮음 → `Look Orbit X`/`Y` 둘 다 `Gain = 20`으로 확정

## 마우스 감도를 설정 가능하게 분리

사용자 요청: "저거(Gain) 할 때, 설정으로 바꿀 수 있게 하고 싶어"

- Cinemachine `Gain=20`은 디자이너 튜닝값으로 고정
- `PlayerInputActions.inputactions`의 `Look` 액션에 `ScaleVector2` 프로세서 추가 (기본 x=1,y=1)
- `MouseSensitivitySetting.cs` 신규 작성 — `PlayerPrefs`로 감도 저장/로드, `SetSensitivity(float)` 공개 메서드로 나중에 설정 UI 슬라이더와 바로 연결 가능
- 최종 감도 = Cinemachine Gain(고정) × 플레이어 배율(설정 가능)

## 수정/생성한 파일

- `Assets/Input/PlayerInputActions.inputactions` — Look 바인딩에 scaleVector2 프로세서 추가
- `Assets/Scripts/MouseSensitivitySetting.cs` — 신규
- [[logic/Cinemachine 3인칭 카메라 설정]] — 실제 겪은 트러블슈팅 반영해 전면 재작성
- [[logic/마우스 감도 설정]] — 신규

## 다음 세션이 할 일

- v0.1 목표 ①(이동+카메라) 완료. **다음은 ② 붓 기본 공격 — 3타 콤보**
- 씬에 `MouseSensitivitySetting` 컴포넌트가 실제로 어떤 오브젝트에 붙었는지 확인 필요 (가이드엔 "아무 오브젝트나"로 되어있어 다음 세션이 헷갈릴 수 있음 — Player에 붙이는 걸 기본으로 가정할 것)
