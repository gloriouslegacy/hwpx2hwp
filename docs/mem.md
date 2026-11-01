# 세션 메모 (갱신 2026-11-01 · 2b~2d 커밋 기준 — 이 메모와 같은 커밋, origin/main 푸시)
## 끝낸 것 (최근)
- 2a 커밋·푸시(16a4738) — git log 참고. WSL 개발 환경(~/.dotnet SDK 10.0.402) 추가
- 2b InputCollector·2c OutputPathPlanner·2d HwpxProbe + 보강 r2(링크 미추적·출력 폴더 밖 거부·manifest 1MiB 상한·이름 변형/중복 NotHwpx). 빌드 경고 0, 테스트 36/36, reviewer·security-reviewer 높음 0
- spec F2 에 T2.5·링크/숨김 미추적 줄 추가. gate-auditor log → docs/issue/agent-issue.md 2건(패키지 쪽 제안)
## 열린 것
- 2e BatchRunner 발주에 넣을 것: 저장 직전 출력 존재 재확인(TOCTOU) · Probe 의 IOException·UnauthorizedAccessException 을 파일 실패로 번역 · 가짜 엔진 ct 처리(T4.2)·FailAt 단일값 · Add 는 UI 밖 스레드에서
- 낮음 미처리: `..foo` 폴더 회귀 테스트·Length 위조 zip·`\` manifest 테스트·루트 폴더 소실 테스트·EngineContractTests 클래스 단위 CA1859 억제
- Windows 확인 대기: baseline 열린 문제 1~4(암호 manifest 형식 포함) · 정션 동작 · 1단계 reviewer 낮음 3건
## 다음에
- 2e BatchRunner(F4 T4.1·T4.2, JobState·BatchReport 포함) → 2f RunLog·FailureReasonText(F5)
- 발주 Note: WSL dotnet 실행법, Spec 은 docs/spec 경로 필수(lessons)
