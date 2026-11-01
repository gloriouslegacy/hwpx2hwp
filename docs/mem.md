# 세션 메모 (갱신 2026-11-02 세션 종료 · 1.0.0 릴리스 공개, 레포 이름 hwpx2hwp, 이 메모와 같은 커밋, origin/main 푸시)
## 끝낸 것 (최근)
- 2~4단계 완료: Core(테스트 71/71)·HwpCom·App. F4 재실행은 실패·대기만, F7 중복 실행 차단
- F8: 아이콘(tools/make_icon.py → src/Hwpx2Hwp.App/app.ico), 버전 1.0.0(csproj `<Version>` 하나 기준), `python3 tools/package.py` → artifacts/hwpx2hwp-1.0.0-win-x64.zip
- GitHub Release v1.0.0 공개(zip sha256 5ecc8de7…7c27). 원격 레포 이름 hwpxTohwp → hwpx2hwp(옛 URL 리다이렉트, 1.0.0 zip 안 README 는 옛 URL)
- 2026-11-02 사용자 Windows 확인: T7.1 성공, 10개 변환 성공(T1.1), 1.0.0 창 제목 `hwpx2hwp 1.0.0`·제목줄 아이콘 보임(T8.1 일부), DLL 없음 안내 뜸·변환 계속(T6.2)
## 열린 것
- Windows 수동 미확인: T8.1 나머지(탐색기 아이콘·파일 속성 버전·README 한글), T1.2·T1.3·T4.3·T4.4·T6.1·X1~X3, 체크리스트 "확인할 사실" 15행(SetMessageBoxMode·Open 인자·반환형·보안 DLL 이름·사망 HRESULT·.tmp.hwp 잔존)
- 보안 DLL 안내가 변환 끝난 화면 위에 뜸(스크린샷) — 엔진 시작 때 안내라 표시 시점·재표시(Notices 실행마다) 확인 필요
- F7 추정: 관리자 권한 첫 인스턴스는 창 못 찾음, 포그라운드 잠금으로 깜빡임만 가능
- 낮음 남음: RegisterModule catch 로그 없음, RunAsync 예외 문서 누락, crash 중 RunLog ERROR 미구현(D-11), 재실행 요약은 이번 실행분만, package.py zipfile 쓰기 중 OSError traceback, ApplyTitleAndIcon 이 MainFormLayout 에 있음
## 다음에
- 체크리스트 결과 오면 SecurityModule.DllNames·HwpComEngine 메시지 모드 상수 수정 후 1.0.1
- 다음 릴리스: csproj Version·README 버전 올림 → `python3 tools/package.py` → 태그 → `gh release create`(올리기 전 사용자 확인)
