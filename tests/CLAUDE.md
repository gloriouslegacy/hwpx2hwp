# tests/
역할: 맥에서 도는 자동 테스트(명세 T 항목·아키텍처 검사)가 바뀔 때 수정된다
공개 진입점: 없음(dotnet test 대상)
의존: 들어오는 것 없음 · 나가는 것 Hwpx2Hwp.Core 만 · 금지: HwpCom·App 참조(net10.0-windows), 실제 한/글 호출, 사용자 폴더·%LOCALAPPDATA% 에 쓰기
| 경로 | 역할 |
|---|---|
| Hwpx2Hwp.Core.Tests/ | net10.0 xUnit. T1.4·T2.*·T3.*·T4.1·T4.2·T5.1·T6.2·암호 사전 검사·X 항목 |
| Hwpx2Hwp.Core.Tests/Fakes/ | 가짜 엔진(지정 번째 실패·예외·미설치·Notice 반환·변환 훅 OnConvert — 취소 시점 제어) |
| Hwpx2Hwp.Core.Tests/ArchitectureTests.cs | Core 참조 0·dynamic/HWPFrame/Registry 는 HwpCom 안·WinForms 는 App 안 |
테스트: dotnet test tests/Hwpx2Hwp.Core.Tests/Hwpx2Hwp.Core.Tests.csproj
규칙: 테스트 이름에 명세 번호를 붙인다(예: T3_4_기존_a와_a_1_있으면_a_2). 명세에 없는 확인은 X_<영역>_n.
규칙: 파일 시스템 테스트는 테스트마다 새 임시 폴더를 만들고 끝나면 지운다. 시각은 고정 TimeProvider.
규칙: 암호 hwpx 는 테스트 안에서 합성 zip 으로 만든다(바이너리 샘플 커밋 안 함).
규칙: Core internal 은 Core csproj 의 InternalsVisibleTo 로만 연다(소스 안 assembly 특성 금지).
규칙: Windows 수동 항목(T1.1·T1.2 실문서·T1.3·T4.3·T6.1·X1~X3)은 여기 두지 않는다 — docs/test/windows-manual.md.
ADR: docs/design/baseline.md (D-9, D-10)
