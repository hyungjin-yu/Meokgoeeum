#로직 #먹괴음 #Cinemachine #카메라 #Unity에디터작업

# Cinemachine 3인칭 카메라 설정 (수동, Unity 에디터에서 진행)

> 2026-08-16 작성, 같은 날 실제 설정 과정에서 겪은 시행착오 반영해 재작성
> v0.1 목표 ① 완료 — 이 절차로 실제 작동 확인됨

## 사전 준비 (이미 완료됨, 코드/데이터 쪽)

- [x] `PlayerController.cs` — 카메라 방향 기준 이동 + 이동 방향 회전 완료 (`cameraTransform` 필드 존재)
- [x] `PlayerInputActions.inputactions`에 `Look` 액션 추가 완료 (`<Mouse>/delta` 바인딩)
- [x] `Assets/Scenes/SampleScene.unity`의 Player 오브젝트 태그를 `Player`로 수정 완료

## ⚠️ 시작 전 딱 하나만 기억할 것

**뭔가 새로 만들기 전엔 항상 Hierarchy 빈 곳을 클릭해서 선택을 해제할 것.** Unity는 뭔가 선택된 상태로 오브젝트를 생성하면 그 자식으로 넣어버린다. 이걸 안 지켜서 `CinemachineCamera`가 `Player` 자식으로 두 번이나 잘못 들어갔었음.

## 설정 순서

1. **Hierarchy 빈 곳 클릭 → 선택 해제**

2. **Main Camera 먼저 만들기** (Cinemachine 메뉴보다 먼저!)
   - Hierarchy 우클릭 → `Camera`
   - 이름 `Main Camera`로, Tag가 `MainCamera`인지 확인
   - ⚠️ 원래 가이드에선 "Cinemachine이 자동으로 Main Camera에 Brain을 붙여준다"고 했는데 **틀렸다** — 이 프로젝트의 `SampleScene`엔 애초에 Main Camera가 없어서, 그 상태로 Cinemachine 카메라부터 만들면 Brain만 혼자 떨어진 오브젝트로 생성돼버림. **Main Camera를 먼저 만들어야 한다.**

3. **그 위에 Brain 붙이기**
   - `Main Camera` 선택 → Inspector `Add Component` 클릭 → 검색창에 "Cinemachine Brain" 입력해서 추가
   - (메뉴의 `GameObject → Cinemachine → Brain` 같은 항목을 쓰면 선택된 오브젝트의 **자식으로 새 오브젝트가 생성**되는 경우가 있어 위험함. 항상 `Add Component` 검색창으로 기존 오브젝트에 직접 추가할 것.)

4. **Hierarchy 빈 곳 클릭 → 선택 해제** (또!)

5. **CM 카메라 생성**
   - 메뉴: `GameObject → Cinemachine → Camera`
   - 최상위(`SampleScene` 바로 아래)에 `CinemachineCamera`가 생겼는지 Hierarchy에서 확인

6. **추적 대상 지정**
   - `CinemachineCamera` 선택 → Inspector `Tracking Target`에 `Player` 드래그

7. **위치 제어 — Orbital Follow**
   - `Add Component → Cinemachine Orbital Follow`
   - `Target Offset`: X 0.5 / Y 1.5 / Z 0
   - `Orbit Style`: Sphere (기본값 유지)
   - `Radius`: 5

8. **조준 제어 — Rotation Composer**
   - `Add Component → Cinemachine Rotation Composer` (기본값 그대로: Screen Position 0,0 / Center On Activate 체크)

9. **마우스 입력 연결 — Input Axis Controller**
   - `Add Component → Cinemachine Input Axis Controller`
   - `Look Orbit X`, `Look Orbit Y` 두 줄 모두 → Input Action Reference를 `PlayerInputActions`의 **`Player/Look`**로 교체 (기본값은 Cinemachine 자체 샘플 액션인 `CM Default/Look...`로 채워져 있으니 반드시 바꿔야 함)
   - `Orbit Scale` 줄은 체크박스 꺼서 비활성화 (스크롤 줌은 이번 기획에 없음)
   - **각 줄 왼쪽의 화살표(▸)를 펼치면 `Gain`(감도) 필드가 있음 — 기본값이 너무 낮아서 마우스를 움직여도 카메라가 거의 안 돎. `Look Orbit X`, `Look Orbit Y` 둘 다 `Gain = 20`으로 설정 (실측 확정값, 2026-08-16 — 이 값은 "디자이너 기본값"으로 고정, 플레이어가 바꾸는 감도는 별도 시스템으로 분리함 → [[logic/마우스 감도 설정]])**

10. **(선택) 마우스 감도 설정 스크립트 붙이기**
    - `MouseSensitivitySetting.cs`를 씬의 아무 오브젝트에나(Player 등) 드래그해서 컴포넌트로 추가
    - 별도 설정 없이 바로 동작함 (기본 배율 1.0 = 방금 맞춘 Gain=20 감도 그대로) — 상세 → [[logic/마우스 감도 설정]]

11. **확인**
    - ▶ Play 버튼으로 재생 시작
    - **Game 뷰 안을 한 번 클릭해서 포커스를 준 다음** (Inspector를 보다가 바로 테스트하면 마우스 입력이 안 먹음) 마우스를 움직여서 카메라가 도는지 확인
    - WASD로 카메라 기준 이동 + 이동 방향으로 캐릭터 회전 확인

## 트러블슈팅 (실제로 겪은 문제들)

| 증상 | 원인 | 해결 |
|------|------|------|
| Game 뷰에 "No cameras rendering" | Main Camera 없이 Cinemachine 카메라부터 만들어서 Brain이 혼자 떨어진 오브젝트로 생성됨 | Main Camera를 먼저 만들고 그 위에 Brain 추가 |
| `CinemachineCamera`/`Brain`이 `Player`나 다른 오브젝트의 자식으로 들어감 | 오브젝트 생성 시 Hierarchy에서 뭔가 선택된 상태였음 | 생성 전 항상 빈 곳 클릭해서 선택 해제 |
| Game 뷰에서 파란/노란 선을 드래그하면 카메라가 움직임 → "되는 줄" 착각 | 그건 Cinemachine이 아니라 **Unity 에디터의 이동 툴 기즈모**를 손으로 드래그한 것 (오브젝트가 선택된 상태에서 Gizmos가 켜져 있으면 보임) | 실제 테스트는 Play 모드에서 아무것도 선택 안 한 채로 마우스만 움직여서 확인 |
| Play 모드인데 마우스 움직여도 반응 없음 | Game 뷰에 포커스가 없음 (Inspector 등 다른 패널에 포커스가 가있음) | Game 뷰 안을 한 번 클릭 후 테스트 |
| 마우스는 반응하는데 카메라가 너무 조금씩 움직임 | `Input Axis Controller`의 `Gain` 기본값이 너무 낮음 | `Look Orbit X`/`Y` 둘 다 `Gain = 20`으로 설정 |

## 관련 노트

- [[02 플레이어 시스템]] — 카메라 방식 스펙 원본
- [[16 조작 설계]] — Camera-relative movement 요구사항
- [[09 개발 마일스톤]] — v0.1 목표 ①
