#기획 #먹괴음 #changelog

# CubeSurfaceWalker — +Z 면에서 캐릭터가 완전히 굳는 버그

## 배경

사용자가 `CubeTestEnemy_Pyeong`을 확인하려고 Play하다가 스크린샷 첨부 — 캐릭터가 거대한 캡슐로 화면을 가득 채운 채 안 움직이고, 콘솔에 `Look rotation viewing vector is zero` 경고가 매 프레임 수백 개씩 쌓이고 있었음("이게 뭐야").

## 원인

`CubeSurfaceWalker.cs`의 "facingForward가 우연히 0벡터가 됐을 때 쓰는 비상 대체값"이 항상 `Vector3.ProjectOnPlane(Vector3.forward, normal)`이었는데, **하필 플레이어가 서있던 면의 법선이 정확히 `Vector3.forward`(+Z 면 — 마침 `CubeTestEnemy_Pyeong`이 있는 그 면)**였음. 자기 자신을 자기 자신의 법선에 투영하면 완전히 상쇄돼서 결과가 정확히 0벡터가 됨.

한 번 0벡터가 되면 그 뒤로 영원히 못 벗어남 — 마우스로 회전시키는 코드(`Quaternion.AngleAxis(각도, normal) * facingForward`)도 0벡터를 회전시키면 그대로 0벡터라서 회복이 안 됨. 그리고 `facingRight = Cross(normal, facingForward)`도 같이 0이 되어서 **전진(`W`)도 좌우 이동도 전부 죽어버림** — 카메라만 그 자리에 딱 붙어 안 움직이는 캐릭터를 계속 따라가는 상태로 보였던 것.

## 수정

`Assets/Scripts/Player/CubeSurfaceWalker.cs`에 `AnyTangentTo(Vector3 normal)` 헬퍼 추가 — `Vector3.up`을 먼저 시도하고, 그것도 평행해서 0이 되면(면 법선이 Y축인 경우) `Vector3.right`로 재시도. 축 정렬된 법선(큐브 6면 전부 ±X/±Y/±Z 중 하나)은 up과 right 둘 다에 동시에 평행할 수 없으므로 항상 성공함. `Start()`와 `Update()` 양쪽의 비상 대체 코드를 전부 이걸로 교체.

## 검증

리플렉션으로 정확히 버그 재현 조건(면=+Z, facingForward를 강제로 0벡터로 세팅) 후 `Update()` 1회 호출 → `facingForward=(0,1,0)`, magnitude=1로 정상 복구 확인.

## 교훈

"비상 대체값"을 고를 때 상수 하나(`Vector3.forward`)만 쓰면, 그 상수가 우연히 실제 조건과 충돌하는 특수 케이스에서 대체값 자체가 무효화될 수 있음 — 이번처럼 "면 법선이 6개 중 하나"인 경우엔 후보를 2개 이상 준비해서 순차 시도하는 게 안전함.

## 후속 — Animator "Moving" 미연동 (같은 테스트 라운드에서 이어서 발견)

버그를 고친 뒤 사용자가 재확인하다가: "움직이기는 하는데, 이전의 다른 몹처럼 걸어다니지는 않고 가만히 서서 이동만 하네." 원인은 단순 누락 — 원본 [[EnemyBase]]는 `LateUpdate()`에서 `agent.velocity` 기준으로 Animator의 "Moving" bool을 매 프레임 갱신했는데, 새로 만든 [[CubeEnemyBase]]는 이 부분을 통째로 빼먹었음(Animator 참조 자체가 없었음).

`CubeEnemyBase`에 `animator` 필드(`Start()`에서 `GetComponentInChildren<Animator>()`) + `movedThisFrame` 플래그 추가 — `MoveToward()`가 실제로 위치를 옮길 때만 true로 세팅하고, `Update()` 끝에서 `animator.SetBool("Moving", movedThisFrame)`. 리플렉션으로 확인: Chasing 중 이동할 때 `Moving=True`, 사거리 안에 들어와 Busy(공격) 상태로 전환된 순간 `Moving=False`(제자리 공격) — 둘 다 정상.
