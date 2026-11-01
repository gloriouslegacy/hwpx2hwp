# tools/
역할: 개발자가 손으로 돌리는 배포 보조 스크립트(F8). 앱 실행 코드와 테스트는 두지 않는다.
의존: Python 3 표준 라이브러리만(PIL·외부 zip 명령 금지). 앱 프로젝트가 이 폴더를 참조하지 않는다.
| 파일 | 역할 |
|---|---|
| make_icon.py | src/Hwpx2Hwp.App/app.ico 생성(16·24·32·48·64·256px, 파란 문서+흰 화살표+HWP) |
| package.py | win-x64 publish → artifacts/hwpx2hwp-<버전>-win-x64.zip(exe + README.txt) |
실행: 레포 루트에서 `python3 tools/<파일>`. WSL 은 dotnet PATH 설정(.claude/CLAUDE.md) 뒤 실행.
규칙: 버전은 csproj 의 `<Version>` 하나에서 읽는다. 스크립트에 버전을 따로 적지 않는다.
규칙: 기존 산출물(zip·ico)을 묻지 않고 덮어쓰지 않는다. zip 이 있으면 멈추고 메시지를 낸다(ico 는 --force 때만).
규칙: 쓰기는 artifacts/ 와 app.ico 경로로만 한다.
