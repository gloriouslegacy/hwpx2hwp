# 로드맵 (spec: docs/spec/convert.md · 구조: docs/design/baseline.md)

## 1단계 뼈대·검사 — 완료 (2026-10-21)
- 솔루션·4개 프로젝트·global.json·Directory.Build.props·.gitignore, ArchitectureTests(D-10 a·b)
- 완료 기준: 맥에서 `dotnet build Hwpx2Hwp.sln` 경고 0, `dotnet test` 통과(아키텍처 테스트 포함)

## 2단계 Core 흐름 (맥 자동 테스트)
- 2a 모델·엔진 인터페이스·가짜 엔진 (F1 T1.4) — 완료 2026-11-01 (JobState·BatchReport 는 2e 로)
- 2b InputCollector (F2) · 2c OutputPathPlanner (F3) · 2d HwpxProbe (암호 사전 검사, D3)
- 2e BatchRunner (F4 T4.1·T4.2) · 2f RunLog·FailureReasonText (F5)
- 완료 기준: F2·F3·F4·F5 의 자동 T 전부 통과

## 3단계 HwpCom (맥 빌드, Windows 수동)
- 3a StaDispatcher·HwpSession·HwpFormatMap·HwpComEngine (F1) · 3b SecurityModule (F6)
- 완료 기준: 맥 빌드 통과, docs/test/windows-manual.md 의 T1.x·T6.1 체크리스트 준비

## 4단계 App·배포
- MainFormLayout·MainForm·Program, publish 로 hwpx2hwp.exe 생성
- 완료 기준: win-x64 publish 성공, Windows 수동 T4.3 포함 전체 체크리스트를 사용자가 확인
