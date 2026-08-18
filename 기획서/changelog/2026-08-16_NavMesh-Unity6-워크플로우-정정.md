#changelog #먹괴음 #NavMesh

# 2026-08-16 — NavMesh 베이크 가이드를 Unity 6 워크플로우로 정정

## 배경

[[logic/먹괴음 - 평 씬 설정]]에 예전 Unity 버전 기준(Window → AI → Navigation의 Object/Bake 탭)으로 안내했는데, 이 프로젝트는 AI Navigation 패키지(Unity 6)를 쓰고 있어서 실제로는 해당 탭이 없음 — 사용자가 직접 확인해줘서 발견.

## 한 일

- [[logic/먹괴음 - 평 씬 설정]] 1단계를 `NavMeshSurface` 컴포넌트 기반 워크플로우로 정정
  - `Ground`에 `Add Component → Nav Mesh Surface` → 컴포넌트 안의 `Bake` 버튼
  - Navigation 창(Agents/Areas 탭)은 에이전트 종류/영역 비용 정의용일 뿐, 베이크 자체는 컴포넌트에서

## 다음 세션이 알아야 할 것

- 앞으로 NavMesh 관련 안내할 때 **`NavMeshSurface` 컴포넌트 기준으로 안내할 것** (Window → AI → Navigation의 Bake 탭은 이 프로젝트에서 존재하지 않음/쓰지 않음)
- [[13 먹괴음 AI 설계]]가 언급하는 "씬 로드 시 런타임 빌드"는 지금(v0.1 단일 씬)은 필요 없고, 나중에 큐브 면 전환 붙일 때(⑤ 이후) 적용
