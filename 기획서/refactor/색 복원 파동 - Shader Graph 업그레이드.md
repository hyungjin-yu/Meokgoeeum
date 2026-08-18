#refactor #먹괴음 #셰이더 #색복원

# 색 복원 파동 — Shader Graph 업그레이드

> 2026-08-16 작성 (아직 착수 안 함, 우선순위 낮음)

## 현재 문제

v0.1의 `ColorWaveEffect`/`PaintableObject`는 [[11 셰이더 설계 - 색 복원 파동]] 원안(Shader Graph 거리 마스크 + VFX Graph)이 아니라 **C# 코루틴으로 오브젝트별 색 Lerp**하는 방식으로 구현돼 있습니다 (→ [[logic/색 복원 파동 씬 설정]] "왜 Shader Graph가 아니라 C#인가"). 이유는 안전성(Shader Graph 에셋을 텍스트로 직접 만드는 위험 회피)이었지 성능/비주얼이 더 나아서가 아닙니다.

**한계:**
- 오브젝트 단위로만 칠해짐 (Ground처럼 큰 단일 메시는 픽셀 그라데이션이 아니라 오브젝트 전체가 한 번에 바뀜)
- 오브젝트가 많아지면(예: 12층 전체) `Physics.OverlapSphere` + 정렬 비용이 커질 수 있음 (지금은 파동 1회당 1번만 하는 구조라 아직 큰 문제 아님)
- VFX Graph 파티클 연출이 아예 없음 (파동 경계 이펙트 없이 색만 바뀜)

## 방향

원안대로 Shader Graph 거리 마스크 방식으로 교체:
1. `_WaveOrigin`, `_WaveRadius`, `_TargetColor`, `_EdgeSoftness`, `_GrayTex`, `_ColorTex` 파라미터를 가진 Shader Graph 작성 (→ [[11 셰이더 설계 - 색 복원 파동]] 파라미터 구조 그대로)
2. `PaintableObject.Paint()`를 "머티리얼 파라미터 애니메이션"으로 내부 교체 (바깥 API는 유지 — 호출하는 쪽인 `ColorWaveEffect`는 안 고쳐도 됨)
3. VFX Graph로 파동 경계 파티클 추가

## 예상 범위

- Unity 에디터에서 Shader Graph 노드 작업 (텍스트로 대신 못 함, 에디터 수작업 필수)
- `PaintableObject.cs` 내부 구현 교체 (Renderer.material.color Lerp → Shader Graph 파라미터 SetFloat/SetVector)
- 기존 씬에 배치된 `PaintableObject`들의 머티리얼을 새 셰이더로 교체 필요

## 우선순위

**낮음.** v0.1 프로토타입 완성이 우선이고, 이건 비주얼 폴리시 단계(→ [[29 개발 리스크 진단 & 스코프 컷 계획]] MoSCoW 기준으로는 SHOULD/COULD 급). 코어 루프가 다 돌아가고 나서, 아트 방향이 어느 정도 확정된 뒤에 착수하는 게 낫습니다 — 지금 셰이더부터 만들면 나중에 아트 스타일이 바뀔 때 다시 손대야 할 수도 있음.

## 관련 노트

- [[11 셰이더 설계 - 색 복원 파동]]
- [[logic/색 복원 파동 씬 설정]]
