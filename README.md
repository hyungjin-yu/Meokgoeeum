# 먹괴음 (Meokgoeeum)

Unity 6 (URP) 기반 PC(Steam) 게임. 1인 개발 프로젝트.

- Unity 버전: `6000.3.16f1`
- 렌더 파이프라인: URP
- 기획 문서: [`기획서/`](./기획서) 폴더 참고

## 시작하기 전에: Git LFS 설치

이 저장소는 `.dll`, `.png`, `.jpg` 등 큰 바이너리 파일을 [Git LFS](https://git-lfs.com)로 관리합니다.
**clone하기 전에 반드시 Git LFS를 설치**하세요. 설치 없이 clone하면 해당 파일들이 실제 내용이 아니라
포인터 텍스트로만 받아져서 Unity에서 에러가 납니다.

```bash
# 1. Git LFS 설치 (최초 1회, 컴퓨터당)
# Windows: https://git-lfs.com 에서 다운로드하거나
winget install GitHub.GitLFS

# 2. Git에 LFS 등록 (최초 1회, 컴퓨터당)
git lfs install

# 3. 이후 정상적으로 clone
git clone https://github.com/hyungjin-yu/Meokgoeeum.git
```

이미 LFS 없이 clone했다면, 폴더 안에서 다음을 실행하면 됩니다.

```bash
git lfs install
git lfs pull
```

## 폴더 구조

- `Assets/` — Unity 프로젝트 에셋 (스크립트, 씬, 셰이더 등)
- `기획서/` — 게임 기획 문서 (세계관, 시스템, 밸런스, UI 등)
