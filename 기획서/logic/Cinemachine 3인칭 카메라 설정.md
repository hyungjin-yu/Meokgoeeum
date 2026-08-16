#로직 #먹괴음 #Cinemachine #카메라 #Unity에디터작업

# Cinemachine 3인칭 카메라 설정 (수동, Unity 에디터에서 진행)

> 2026-08-16 작성 — v0.1 목표 ①의 마지막 단계
> Cinemachine 3.x는 컴포넌트를 씬 파일(YAML)에 직접 손으로 써넣기엔 필드가 많고 실수 위험이 커서, **Editor에서 직접 클릭 몇 번**으로 진행하는 걸 권장합니다. 코드/입력 쪽 준비는 이미 끝나 있어서 이 문서만 따라하면 됩니다.

## 사전 준비 (이미 완료됨)

- [x] `PlayerController.cs` — 카메라 방향 기준 이동으로 수정 완료 (`cameraTransform` 필드 존재)
- [x] `PlayerInputActions.inputactions`에 `Look` 액션 추가 완료 (`<Mouse>/delta` 바인딩)
- [x] `Assets/Scenes/SampleScene.unity`의 Player 오브젝트 태그를 `Player`로 수정 완료
- [ ] **Unity 에디터를 처음 열면**: `PlayerInputActions.inputactions`가 자동으로 재임포트되면서 `PlayerInputActions.cs`(자동생성 파일)에 `Look` 액션이 반영됨 — 별도 작업 불필요, 그냥 한 번 열기만 하면 됨

## 설정 순서

1. **Cinemachine 카메라 생성**
   - 메뉴: `GameObject → Cinemachine → Camera`
   - 씬에 "CM Camera"라는 오브젝트가 생기고, `Main Camera`에 `CinemachineBrain` 컴포넌트가 자동으로 붙습니다 (안 붙어 있으면 Main Camera 선택 → `Add Component → Cinemachine Brain`)

2. **추적 대상 지정**
   - "CM Camera" 선택 → Inspector 상단의 `Tracking Target`에 씬의 `Player` 오브젝트를 드래그

3. **위치 제어 — Orbital Follow 추가**
   - "CM Camera" 선택 → `Add Component → Cinemachine Orbital Follow`
   - `Tracking Target Offset` — 캐릭터 어깨 높이 정도로 (예: X 0.5 / Y 1.5 / Z 0, 3인칭 숄더뷰이므로 살짝 옆으로)
   - `Radius`(카메라 거리) — 4~5 정도로 시작 (너무 멀면 전투 타격감이 죽음)

4. **조준 제어 — Rotation Composer 추가**
   - 같은 오브젝트에 `Add Component → Cinemachine Rotation Composer`
   - 별도 설정 없이 기본값으로 시작해도 됨 (Tracking Target을 그대로 바라봄)

5. **마우스 입력 연결 — Input Axis Controller 추가**
   - 같은 오브젝트에 `Add Component → Cinemachine Input Axis Controller`
   - `Controllers` 리스트에 Orbital Follow의 Pan/Tilt 축이 자동으로 나타남 (안 보이면 리스트 아래 `+`로 추가)
   - 각 항목의 `Input Action Reference`에 `Assets/Input/PlayerInputActions.inputactions` 안의 **`Player/Look`** 액션을 드래그해서 연결

6. **확인**
   - Play 버튼 → 마우스를 움직이면 카메라가 캐릭터 주위로 회전하는지
   - WASD를 누르면 **카메라가 보는 방향 기준**으로 캐릭터가 이동하고, 이동 방향으로 캐릭터가 자연스럽게 회전하는지 확인

## 잘 안 될 때 체크리스트

- 카메라가 안 움직임 → `Cinemachine Input Axis Controller`의 Input Action Reference가 비어있거나 잘못된 액션(`Move`)에 연결됐는지 확인
- 캐릭터가 이동은 하는데 카메라 기준이 아니라 월드 기준으로 움직임 → `PlayerController`의 `Camera Transform` 필드가 비어있는데 `Camera.main`도 못 찾는 경우 (Main Camera에 `MainCamera` 태그가 붙어 있는지 확인)
- 카메라가 바닥을 뚫고 들어감 → Orbital Follow에 `Cinemachine Deoccluder`(구 Collider) 컴포넌트 추가 필요 (v0.1에서는 생략 가능, 나중에 추가)

## 관련 노트

- [[02 플레이어 시스템]] — 카메라 방식 스펙 원본
- [[16 조작 설계]] — Camera-relative movement 요구사항
- [[09 개발 마일스톤]] — v0.1 목표 ①
