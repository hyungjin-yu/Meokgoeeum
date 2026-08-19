#로직 #먹괴음 #v0.3 #조명 #APV

# APV 조명 설정 (Adaptive Probe Volumes)

> [[10 씬 구조 & 기술 스펙]] "APV(Adaptive Probe Volumes) — 구역별 베이크드 GI, 색 복원 후
> Light Probe 갱신" 항목. 이건 대부분 **Unity 에디터 라이팅 작업**이라 코드로 대신할 수 있는
> 부분이 거의 없습니다 — 지금은 절차만 정리해두고, 실제 레벨(면 콘텐츠)이 어느 정도 갖춰진
> 뒤에 착수하는 걸 권장합니다.

## 지금 왜 코드가 아니라 이 문서만 있는지

APV는 "씬의 조명을 미리 계산해서 저장(베이크)해두는" 시스템이라, **베이크할 실제 레벨 지오메트리(벽/바닥/오브젝트)가 있어야 의미가 있습니다.** 지금은 테스트용 평면 하나뿐이라 베이크해봐야 확인할 게 없어서, 실제 면 콘텐츠(레벨 디자인)가 어느 정도 만들어진 뒤에 하는 게 효율적입니다.

`PaintableObject.Paint()`에는 이미 "조명 변화 연출 훅 자리" 주석을 남겨뒀습니다 — 나중에 실제로 조명을 갱신하는 코드가 필요해지면 그 자리에 넣으면 됩니다.

## 나중에 착수할 때 — Unity 에디터 절차 (요약)

1. `Edit` → `Project Settings` → `Graphics` (또는 `URP Global Settings`)에서 **Adaptive Probe Volumes 활성화** 확인 (URP 프로젝트는 기본적으로 지원, 켜져 있는지만 확인)
2. 각 면 씬에 `Probe Volume` 컴포넌트를 가진 오브젝트 배치 (Hierarchy 우클릭 → `Light` → `Probe Volume`) → 씬 전체를 덮도록 크기 조절
3. `Window` → `Rendering` → `Lighting` 창에서 `Generate Lighting` (또는 `Bake`) 눌러서 베이크
4. "색 복원 후 조명 변화" 연출이 필요하면: `PaintableObject.Paint()`의 훅 자리에서 근처 라이트의 밝기/색을 서서히 바꾸는 코드 추가 (또는 `LightProbes.Tetrahedralize()` 등 런타임 갱신 API 검토)

## 관련 노트

- [[10 씬 구조 & 기술 스펙]]
- [[11 셰이더 설계 - 색 복원 파동]]
