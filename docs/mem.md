# 세션 메모 (갱신 2026-11-02 · 5단계 배포 1.0.0 준비, 이 메모와 같은 커밋, origin/main 푸시)
## 끝낸 것 (최근)
- 2~4단계 완료: Core(테스트 71/71)·HwpCom·App. F4 재실행은 실패·대기만, F7 중복 실행 차단
- 2026-11-02 사용자 Windows 확인: T7.1 성공, 10개 파일 변환 성공(T1.1)
- F8(사용자 결정): 아이콘 파란 문서+흰 화살표+HWP(tools/make_icon.py → src/Hwpx2Hwp.App/app.ico), 버전 1.0.0(csproj `<Version>` 하나 기준, 창 제목 `hwpx2hwp 1.0.0`)
- 배포 스크립트 `python3 tools/package.py` → artifacts/hwpx2hwp-1.0.0-win-x64.zip(exe + docs/release/README.txt 를 BOM·CRLF 로). 기존 zip 있으면 멈춤, Version 형식 검증
- 로컬 zip 생성됨: 46,087,513B, sha256 5ecc8de7…7c27 (artifacts/ 는 gitignore)
## 열린 것
- GitHub Release v1.0.0 업로드: 사용자 확인 전 금지(F8). 태그는 아직 없음
- Windows 수동 미확인: T8.1·T1.2·T1.3·T4.3·T4.4·T6.1·X1~X3, 체크리스트 "확인할 사실" 15행(SetMessageBoxMode·Open 인자·반환형·보안 DLL 이름·사망 HRESULT·.tmp.hwp 잔존)
- F7 추정: 관리자 권한 첫 인스턴스는 창 못 찾음, 포그라운드 잠금으로 깜빡임만 가능
- 낮음 남음: RegisterModule catch 로그 없음, Notices 재표시, RunAsync 예외 문서 누락, crash 중 RunLog ERROR 미구현(D-11), 재실행 요약은 이번 실행분만, package.py zipfile 쓰기 중 OSError 는 traceback, ApplyTitleAndIcon 이 MainFormLayout 에 있음
## 다음에
- 릴리스 승인 받으면: `git tag v1.0.0` → push → `gh release create v1.0.0 artifacts/hwpx2hwp-1.0.0-win-x64.zip`
- 체크리스트 결과 오면 SecurityModule.DllNames·HwpComEngine 메시지 모드 상수 수정 후 1.0.1
