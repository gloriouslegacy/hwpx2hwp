# 세션 메모 (갱신 2026-11-01 · 로드맵 2~4단계 구현 완료 — 이 메모와 같은 커밋, origin/main 푸시)
## 끝낸 것 (최근)
- 2단계 Core 완료: BatchRunner·RunLog·FailureReasonText·AppPaths, T4.1·T4.2·T5.1 + X 항목. 테스트 71/71
- 3단계 HwpCom: StaDispatcher·HwpSession·HwpFormatMap·HwpComEngine(임시 이름 저장→File.Move 덮어쓰기 차단, 사망 HRESULT 5종)·SecurityModule(HKCU 값 1개)
- 4단계 App: MainFormLayout·MainForm·Program(crash 로그·종료 시 엔진 정리). WSL win-x64 publish → hwpx2hwp.exe 1개(약 52MB, 레포 밖 scratchpad)
- 모든 발주 reviewer·security-reviewer 높음 0. windows-manual.md 에 X2(Hwp.exe 강제 종료)·X3(덮어쓰기) 추가, baseline D-6·D-7 위험 기록
## 열린 것
- Windows 수동 전부 미확인: T1.1~T1.3·T4.3·T6.1·X1~X3, 체크리스트 "확인할 사실" 15행(SetMessageBoxMode 0x00224121·Open 인자·반환형·보안 DLL 이름·사망 HRESULT·.tmp.hwp 잔존)
- 결정 대기: 변환 뒤 다시 [변환]하면 이미 성공한 파일도 다시 변환(덮어쓰기는 없고 a_1.hwp 생성) — 그대로 둘지
- 낮음 남음: RegisterModule 감싸기 catch 로그 없음, Notices 가 실행마다 다시 뜰 수 있음, BatchRunner.RunAsync 예외 문서에 UnauthorizedAccessException 누락, crash 중 RunLog ERROR 줄 미구현(D-11)
## 다음에
- 사용자가 Windows 에서 체크리스트 실행 → 결과로 열린 문제 1~4 확정·상수 수정
- 첫 배포: `dotnet publish src/Hwpx2Hwp.App/Hwpx2Hwp.App.csproj -c Release -r win-x64 -o artifacts/publish`
