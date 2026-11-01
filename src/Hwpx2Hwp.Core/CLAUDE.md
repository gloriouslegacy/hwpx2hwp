# src/Hwpx2Hwp.Core/
역할: 변환 흐름 규칙(수집·출력 경로·암호 사전 검사·일괄 실행·로그 형식)이나 도메인 타입이 바뀔 때 수정된다
공개 진입점: IConversionEngine, InputCollector, OutputPathPlanner.Reserve, HwpxProbe.Inspect, BatchRunner.RunAsync, RunLog.Create, AppPaths.LogDirectory, FailureReasonText
의존: 들어오는 것 App/·HwpCom/·tests/ · 나가는 것 없음(BCL 만) · 금지: ProjectReference·PackageReference, WinForms, COM(dynamic·HWPFrame), 레지스트리
| 폴더 | 역할 |
|---|---|
| Model/ | ConversionRoute·InputItem·ConversionJob·ConversionOutcome·FailureReason·JobState·BatchReport(BatchFailure) |
| Engine/ | IConversionEngine·EngineStart (엔진 계약, V1) |
| Collect/ | F2 입력 수집·중복 제거(경로 OrdinalIgnoreCase), 읽을 수 없는 경로는 항목 단위로 건너뜀 |
| Paths/ | F3 출력 경로 계산·이름_N 예약(D1) |
| Probe/ | hwpx zip 읽기 전용 검사 → Ok/NotHwpx/Encrypted |
| Batch/ | F4 일괄 실행·진행 이벤트(BatchEvent)·옵션(BatchOptions)·취소·실패 격리 |
| Logging/ | F5 RunLog(탭 구분 1파일 1줄, 쓰기 실패는 WriteFailed 로만)·FailureReasonText·로그 폴더 경로 |
테스트: dotnet test tests/Hwpx2Hwp.Core.Tests/Hwpx2Hwp.Core.Tests.csproj
규칙: 확장자는 하드코딩하지 않고 ConversionRoute 로 받는다. 원본 파일에 쓰기·삭제 금지(zip 은 FileAccess.Read·FileShare.Read).
규칙: 파일 단위 실패는 예외가 아니라 ConversionOutcome. BatchRunner 는 엔진 예외도 잡아 Unexpected 로 기록하고 계속한다.
규칙: 출력 쪽 실패(예약·출력 폴더 생성·저장 직전 재확인)는 SaveFailed, Probe 의 NotHwpx·읽기 예외는 OpenFailed. 로그 END 줄은 모든 경로에서 남긴다.
규칙: 비동기는 ConfigureAwait(false). RunAsync 는 호출 스레드를 막지 않는다. 시각은 TimeProvider 주입만(DateTime.Now 금지).
규칙: 실패 이유 문구는 FailureReasonText 한 곳에서만 만든다.
ADR: docs/design/baseline.md (D-3, D-4, D-5, D-11)
