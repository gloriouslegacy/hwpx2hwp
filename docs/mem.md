# 세션 메모 (갱신 2026-11-01 · 커밋 16a4738 기준, origin/main 푸시됨)
## 끝낸 것 (최근)
- 1단계 뼈대·첫 커밋·푸시까지 — git log 참고
- 2a 완료(16a4738 푸시): Core Model/ 5개(ConversionRoute·InputItem·ConversionJob·ConversionOutcome·FailureReason)·Engine/(IConversionEngine·EngineStart)·tests Fakes/FakeConversionEngine·EngineContractTests(T1_4 8건). 빌드 경고 0, 테스트 11/11, reviewer 높음·중간 0
- 개발 환경 WSL 추가: SDK 10.0.402 를 ~/.dotnet 에 설치, .claude/CLAUDE.md 에 실행법 기록
## 열린 것
- JobState·BatchReport 는 2e 로 미룸. FailureReasonText 는 2f
- 가짜 엔진 보강은 2e 때: ct 무시(T4.2 취소 재현용 ThrowIfCancellationRequested 또는 훅), FailAt 단일값, StartAsync 호출 횟수 미집계. EngineContractTests 클래스 단위 CA1859 억제(낮음)
- Windows 확인 대기: baseline 열린 문제 1~4 · 1단계 reviewer 낮음 3건(고칠지 미정)
## 다음에
- 2b InputCollector(F2) · 2c OutputPathPlanner(F3) · 2d HwpxProbe — 파일 안 겹치면 병렬, 발주마다 implementer → reviewer
- 발주 Note 에 WSL dotnet 실행법(export DOTNET_ROOT·PATH) 포함
