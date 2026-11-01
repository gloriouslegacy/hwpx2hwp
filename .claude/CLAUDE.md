# hwpx2hwp — 프로젝트 규칙

hwpx → hwp 일괄 변환 Windows 프로그램. 한/글 2014+ COM 자동화, C# .NET 10 WinForms, self-contained 단일 exe.

## 문서
- 요구사항: docs/requirements/convert.md · 기능명세: docs/spec/convert.md (F1~F6, 테스트 T)
- 구조: docs/structure.md · 설계 결정: docs/design/baseline.md · 로드맵: docs/roadmap.md

## 개발 환경
- 개발은 macOS 또는 WSL Ubuntu, .NET SDK 10.0.402. Windows 전용(WinForms·COM) 실행 확인은 사용자가 Windows PC에서 한다.
- WSL: SDK 는 `~/.dotnet` (PATH 에 없음). 명령 앞에 `export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH &&` 를 붙인다. Windows 쪽 dotnet.exe(SDK 9)는 쓰지 않는다.
- 맥·WSL 에서 확인할 수 있는 건 빌드와 Core 자동 테스트까지다. Windows 수동 테스트 항목은 "미확인"으로 보고한다.

## 명령 (레포 루트에서)
- 빌드: `dotnet build Hwpx2Hwp.sln` (경고 0 유지, TreatWarningsAsErrors)
- 테스트: `dotnet test tests/Hwpx2Hwp.Core.Tests/Hwpx2Hwp.Core.Tests.csproj`
- 배포: `dotnet publish src/Hwpx2Hwp.App/Hwpx2Hwp.App.csproj -c Release -r win-x64 -o artifacts/publish` → hwpx2hwp.exe 하나

## 규칙
- 원본 hwpx 는 절대 수정·삭제하지 않는다. 출력 파일도 덮어쓰지 않는다(F3).
- 한/글 2014에 없는 COM API는 쓰지 않는다.
