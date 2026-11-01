# 로드맵 (spec: docs/spec/convert.md · 구조: docs/design/baseline.md)

## 1단계 뼈대·검사 — 완료 (2026-10-21)
- 솔루션·4개 프로젝트·global.json·Directory.Build.props·.gitignore, ArchitectureTests(D-10 a·b)
- 완료 기준: 맥에서 `dotnet build Hwpx2Hwp.sln` 경고 0, `dotnet test` 통과(아키텍처 테스트 포함)

## 2단계 Core 흐름 (맥 자동 테스트) — 완료 (2026-11-01, 테스트 66/66)
- 2a 모델·엔진 인터페이스·가짜 엔진 (F1 T1.4) — 완료 2026-11-01 (JobState·BatchReport 는 2e 로)
- 2b InputCollector (F2) · 2c OutputPathPlanner (F3) · 2d HwpxProbe (암호 사전 검사, D3) — 완료 2026-11-01
- 2e BatchRunner (F4 T4.1·T4.2) · 2f RunLog·FailureReasonText (F5) — 완료 2026-11-01
- 완료 기준: F2·F3·F4·F5 의 자동 T 전부 통과

## 3단계 HwpCom (맥 빌드, Windows 수동) — 완료 2026-11-01 (WSL 빌드·체크리스트 준비, Windows 수동 미확인)
- 3a StaDispatcher·HwpSession·HwpFormatMap·HwpComEngine (F1) · 3b SecurityModule (F6)
- 완료 기준: 맥 빌드 통과, docs/test/windows-manual.md 의 T1.x·T6.1 체크리스트 준비

## 4단계 App·배포 — 구현 완료 2026-11-01 (WSL win-x64 publish 성공 → hwpx2hwp.exe 1개 약 52MB, Windows 수동 체크리스트 사용자 확인 대기)
- MainFormLayout·MainForm·Program, publish 로 hwpx2hwp.exe 생성
- 4c 중복 실행 차단 (F7, 2026-11-02 사용자 결정, 수동 T7.1) — 구현 완료
- 완료 기준: win-x64 publish 성공, Windows 수동 T4.3 포함 전체 체크리스트를 사용자가 확인
- 2026-11-02 사용자 확인: T7.1 성공, 10개 파일 변환 성공(T1.1)

## 5단계 배포 1.0.0 (F8, 2026-11-02 사용자 결정)
- 5a 아이콘·버전(tools/make_icon.py, app.ico, csproj, 창 제목) · 5b 패키지 스크립트(tools/package.py → zip) · 5c GitHub Release v1.0.0(올리기 전 사용자 확인)
- 완료 기준: T8.2 통과, Windows T8.1 사용자 확인
