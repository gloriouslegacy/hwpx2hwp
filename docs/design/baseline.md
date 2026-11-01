# 0001 구조 기준선 — hwpx→hwp 변환기
상태: 확정 (2026-10-21 사용자 승인)      날짜: 2026-10-21

## 조건
- S1~S4, Q1~Q4, C1~C5, V1~V3, D1~D3 (docs/requirements/convert.md), F1~F6 (docs/spec/convert.md)
- 요약: 한/글 2014 COM 으로 hwpx 를 hwp 로 일괄 저장하는 Windows WinForms 단일 exe. 개발·빌드·자동 테스트는 macOS(.NET SDK 10.0.402), 실행·COM 검증은 Windows.
- 경계 기준: 변경 축 V1(엔진 추가)·V2(CLI 추가)·V3(형식 추가), 외부 I/O 4개(한/글 COM, 레지스트리, 파일 시스템, 시계).

## 결정

### D-1 레포 레이아웃 (지금 도입)
```
Hwpx2Hwp.sln                 솔루션(루트)
global.json                  SDK 10.0.402 고정, rollForward latestFeature
Directory.Build.props        공통 속성(D-9)
.editorconfig  .gitignore    (bin/ obj/ artifacts/ docs/map.md)
src/Hwpx2Hwp.Core/           net10.0         — 변환 흐름·도메인 타입·엔진 인터페이스(UI·COM 없음)
src/Hwpx2Hwp.HwpCom/         net10.0-windows — 한/글 COM 엔진(F1)·보안 모듈 등록(F6)
src/Hwpx2Hwp.App/            net10.0-windows WinForms — 화면·조립 지점
tests/Hwpx2Hwp.Core.Tests/   net10.0 xUnit   — 맥에서 도는 자동 테스트
artifacts/                   publish 출력(커밋 안 함)
```
- 모듈 = 프로젝트 하나. Core 만 기능별 하위 폴더를 둔다. `src/` 바로 아래에는 파일을 두지 않는다.

### D-2 의존 방향 (지금 도입)
```
App ──▶ HwpCom ──▶ Core        App ──▶ Core        tests/Core.Tests ──▶ Core
```
- Core 는 BCL 외 참조 없음(ProjectReference 0, PackageReference 0).
- HwpCom 은 Core 의 `IConversionEngine` 을 구현할 뿐 Core 흐름(BatchRunner 등)을 호출하지 않는다.
- App 이 HwpCom 에서 쓰는 것은 `HwpComEngine.Create(exeDir)` 하나. `dynamic`·ProgID·레지스트리는 App 에 나오지 않는다.
- 강제: Core 가 net10.0 이라 net10.0-windows 프로젝트를 참조하면 빌드 실패(구조적). 나머지는 D-10 아키텍처 테스트.

### D-3 엔진 인터페이스 (지금 도입 — V1·외부 I/O 경계)
Core `Engine/IConversionEngine.cs`:
```
public interface IConversionEngine : IAsyncDisposable {
    Task<EngineStart> StartAsync(CancellationToken ct);   // 설치 확인·보안 모듈·인스턴스 생성. 여러 번 불러도 실제 시작은 1회
    Task<ConversionOutcome> ConvertAsync(ConversionJob job, CancellationToken ct); // 파일 1개. 파일 단위 실패는 예외가 아니라 Outcome
}
public sealed record EngineStart(bool Available, string? UnavailableMessage, IReadOnlyList<string> Notices);
```
- 한/글 미설치(T1.3) → `Available=false` + 안내 문구. BatchRunner 는 파일을 하나도 처리하지 않고 끝낸다.
- 보안 모듈 없음/등록 실패(F6) → `Notices` 에 "승인 팝업이 뜰 수 있음" 1건. 엔진 인스턴스가 앱 수명 동안 1개이므로 안내도 앱 실행당 1회.
- 엔진 능력 질의(`Supports(route)` 등): **도입 조건: 두 번째 엔진(V1) 또는 두 번째 경로(V3)가 생길 때**.

### D-4 도메인 타입 (지금 도입, Core `Model/`)
- `ConversionRoute(SourceExtension, TargetExtension)` — 지금은 `HwpxToHwp` 하나. 수집기·경로 계산은 확장자를 하드코딩하지 않고 Route 를 받는다(V3).
- `InputItem(SourcePath, BaseFolder?)` — BaseFolder 는 끌어다 놓은 폴더(상대 구조 기준, F3). 파일 입력이면 null.
- `ConversionJob(Input, OutputPath, Route)`, `ConversionOutcome(Succeeded, FailureReason?, Detail?)`.
- `FailureReason`: `InputMissing, Encrypted, OpenFailed, SaveFailed, EngineCrashed, Unexpected`. 화면·로그 문구는 Core `FailureReasonText` 한 곳(UI·로그·CLI 공용). `Encrypted` 문구는 "암호"를 포함한다(T1.2).
- `JobState`: `Pending, Running, Succeeded, Failed`. 취소된 파일은 `Pending` 으로 남는다(T4.2).
- `BatchReport(Succeeded, Failed, Pending, Failures, LogPath, Notices, Cancelled)`.

### D-5 변환 흐름 — UI 와 분리 (지금 도입, V2)
| Core 폴더 | F | 공개 진입점 |
|---|---|---|
| `Collect/` | F2 | `InputCollector(route)` — `Add(paths)`, `Remove(item)`, `Clear()`, `Items` |
| `Paths/` | F3 | `OutputPathPlanner(route, outputFolder?)` — `Reserve(item)` |
| `Probe/` | F1 일부(D3) | `HwpxProbe.Inspect(path)` → `Ok / NotHwpx / Encrypted` |
| `Batch/` | F4 | `BatchRunner(engine, logDir, TimeProvider)` — `RunAsync(items, options, IProgress<BatchEvent>, ct)` → `BatchReport` |
| `Logging/` | F5 | `RunLog.Create(logDir, time)`, `AppPaths.LogDirectory` |
- 파일마다 순서: 원본 존재 확인 → `HwpxProbe`(암호면 엔진 호출 없이 `Encrypted`) → `Planner.Reserve`(직전 계산: 디스크 존재 + 같은 실행 예약 집합, D1) → 출력 폴더 생성 → `engine.ConvertAsync` → 로그 1줄 → 진행 이벤트.
- 중복 판정(F2): 전체 경로 정규화 후 `StringComparer.OrdinalIgnoreCase`(대상 OS 가 Windows). 확장자 비교도 대소문자 무시.
- 실패 격리(Q4): BatchRunner 는 엔진이 던진 예외까지 잡아 그 파일만 `Unexpected` 로 기록하고 계속한다.
- 취소(Q3): 파일 사이에서만 토큰을 확인한다. 진행 중 COM 호출은 끊지 않는다.
- 스레드: `RunAsync` 는 호출 스레드를 막지 않는다(시작 시 스레드 풀로 넘기고 내부는 `ConfigureAwait(false)`). 진행 보고는 호출자가 준 `IProgress<T>` 로만.
- 암호 사전 검사(승인): HWPX(OWPML zip) `META-INF/manifest.xml` 의 암호화 항목으로 판정. zip 은 읽기 전용(`FileAccess.Read`, `FileShare.Read`)으로만 연다(Q2). 엔진도 열기 인자 `suspendpassword:TRUE` 로 암호 대화상자를 막는다(이중 안전).
- Q2 불변식(Planner 가 보장): 출력 확장자는 Route.Target, 출력 경로는 기존 파일·입력 경로와 같지 않다. 원본에 쓰기·삭제하는 코드는 레포 어디에도 없다.

### D-6 한/글 COM (지금 도입, HwpCom)
- **late binding(`dynamic`)**. `Type.GetTypeFromProgID("HWPFrame.HwpObject")` 가 null 이면 미설치(T1.3). interop 어셈블리는 쓰지 않는다.
- `dynamic` 은 `HwpSession.cs` 한 파일에만. 한/글 2014 에서 쓸 API: `XHwpWindows.Item(0).Visible=false`, `RegisterModule("FilePathCheckDLL", <값 이름>)`, `SetMessageBoxMode`, `Open(path, "HWPX", "forceopen:true;suspendpassword:true;versionwarning:false")`, `SaveAs(path, "HWP", "")`, `Clear(1)`, `Quit()`. 형식 문자열 매핑은 `HwpFormatMap.cs`(V3).
- **STA**: `StaDispatcher` 가 `ApartmentState.STA` 전용 스레드 1개 + 작업 큐. 한/글 인스턴스는 그 스레드에서 첫 `StartAsync` 때 1번 만들고 `DisposeAsync`(앱 종료)까지 다시 쓴다(C4). Hwp.exe 는 out-of-proc 서버이고 이벤트를 받지 않으므로 메시지 루프는 두지 않는다.
- 덮어쓰기 방지: SaveAs 는 같은 폴더 임시 이름에 하고 `File.Move(overwrite:false)` 로 옮긴다(존재 확인~저장 사이 틈 차단). 실패 때는 우리가 만든 임시 파일만 지운다.
- 서버 사망(`RPC_E_SERVER_DIED`·`RPC_S_SERVER_UNAVAILABLE`, HRESULT 0x80010007·0x80010012·0x800706BA·0x80010108·0x800706BE) → 그 파일 `EngineCrashed`, 인스턴스 1회 재생성 후 계속. 재생성도 실패하면 남은 파일 전부 `EngineCrashed`.
- 파일마다 저장 뒤 `Clear(1)`(변경 버림)로 닫아 원본 잠금을 푼다.
- 파일별 시간 제한: **도입 조건: Windows 수동 시험에서 대화상자·멈춤이 재현되면**(그때 Hwp.exe 종료 + 재생성).

### D-7 보안 모듈 등록 F6 (지금 도입, HwpCom `SecurityModule.cs`)
- exe 폴더(`AppContext.BaseDirectory`)에서 정해진 이름의 DLL 을 찾는다(이름 목록은 상수 1곳).
- 레지스트리 범위: **`HKCU\Software\HNC\HwpAutomation\Modules` 의 문자열 값 1개**(값 이름 `FilePathCheckerModule`, 데이터 = DLL 전체 경로)만. 값이 이미 같으면 쓰지 않는다. HKLM·다른 키는 건드리지 않고 삭제하지 않는다.
- 그 뒤 엔진 시작 때 `RegisterModule("FilePathCheckDLL", "FilePathCheckerModule")`. DLL 없음·쓰기 실패·RegisterModule false → Notice 1건, 변환은 계속.
- 위험(수용): exe 폴더가 다른 사용자가 쓸 수 있는 위치면 같은 이름 DLL 이 보안 모듈로 등록되어 한/글에 로드될 수 있다 — 배포 안내로 대응, 서명 검증은 도입 조건: 실제 배포 경로가 공유 폴더일 때. 기존 값이 다른 DLL 경로면 덮어쓴다.

### D-8 App (지금 도입)
- 디자이너 파일(`*.Designer.cs`·`.resx`) 미사용. 컨트롤 생성은 `MainFormLayout.cs`, 이벤트·흐름 연결은 `MainForm.cs`.
- `Program.cs` 가 조립 지점: `HwpComEngine.Create(AppContext.BaseDirectory)` + `BatchRunner(engine, AppPaths.LogDirectory, TimeProvider.System)`. 전역 예외 처리기 등록. 종료 시 엔진 `DisposeAsync`.
- 폴더 수집(F2)도 `Task.Run` 으로 UI 스레드 밖에서.
- 고DPI: csproj `ApplicationHighDpiMode=PerMonitorV2`.

### D-9 빌드·배포·테스트 (지금 도입)
- 맥: .NET SDK 10.0.402 설치됨(런타임 10.0.13). `global.json` 이 10.0.4xx 이상 10.0 대를 고정.
- `Directory.Build.props`: `EnableWindowsTargeting=true`(맥에서 net10.0-windows 빌드), `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=10.0-recommended`.
- App csproj: `OutputType=WinExe`, `UseWindowsForms=true`, `AssemblyName=hwpx2hwp`, `SelfContained=true`, `PublishSingleFile=true`, `IncludeNativeLibrariesForSelfExtract=true`, `EnableCompressionInSingleFile=true`. `PublishTrimmed` 금지(WinForms·COM dynamic 미지원), ReadyToRun 끔(교차 컴파일 회피).
  - 구현 보정(2026-10-21): SelfContained·단일 파일 묶음은 `RuntimeIdentifier` 가 있을 때만 적용한다. RID 없이 SelfContained 를 켜면 맥 빌드가 NETSDK1082(osx-arm64 WinForms 런타임 팩 없음)로 깨진다.
  - 구현 보정(2026-10-21): Release 는 `DebugType=none`(Directory.Build.props). 배포 결과는 `hwpx2hwp.exe` 하나뿐이다(약 52MB, 사용자 결정).
- 명령(맥에서 동작):
  - 테스트: `dotnet test tests/Hwpx2Hwp.Core.Tests/Hwpx2Hwp.Core.Tests.csproj`
  - 전체 빌드: `dotnet build Hwpx2Hwp.sln`
  - 배포: `dotnet publish src/Hwpx2Hwp.App/Hwpx2Hwp.App.csproj -c Release -r win-x64 -o artifacts/publish` → `artifacts/publish/hwpx2hwp.exe` 하나를 Windows 로 복사.
- 맥 자동: T1.4, T2.1~2.4, T3.1~3.5, T4.1, T4.2, T5.1, T6.2(가짜 엔진), 암호 사전 검사(합성 zip), 아키텍처 테스트. Windows 수동: T1.1, T1.2(실제 암호 문서), T1.3, T4.3, T6.1, X1(260자 경로)·X2(Hwp.exe 강제 종료)·X3(덮어쓰기 금지) — 체크리스트 `docs/test/windows-manual.md`.
- HwpCom·App 테스트 프로젝트: **도입 조건: Windows PC 에서 `dotnet test` 를 돌릴 수 있게 될 때**(`tests/Hwpx2Hwp.HwpCom.Tests` net10.0-windows).

### D-10 승인 검사 (2026-10-21 승인 — 구현 첫 작업으로 설정)
1. **승인**: `TreatWarningsAsErrors` + `AnalysisLevel=10.0-recommended` + Nullable (D-9).
2. **승인**: `tests/Hwpx2Hwp.Core.Tests/ArchitectureTests.cs` — ① `Hwpx2Hwp.Core.csproj` 에 ProjectReference·PackageReference 없음 ② `dynamic`·`HWPFrame`·`Microsoft.Win32.Registry` 가 `src/Hwpx2Hwp.HwpCom/` 밖에 없음 ③ `System.Windows.Forms` 가 `src/Hwpx2Hwp.App/` 밖에 없음. 레포 루트는 `Hwpx2Hwp.sln` 이 있는 상위 폴더를 찾아 정한다.
3. **보류**: `dotnet format --verify-no-changes`. 조건: 포맷 차이로 리뷰 지적이 반복될 때.

### D-11 로그 (지금 도입)
- 라이브러리 없음(BCL `StreamWriter`, UTF-8 BOM — Windows 메모장 호환). Microsoft.Extensions.Logging 은 **도입 조건: CLI(V2) 로 콘솔·파일 두 싱크가 필요할 때**.
- 파일: `%LOCALAPPDATA%\hwpx2hwp\logs\yyyyMMdd-HHmmss.log`(F5). 실행 1회 = 파일 1개. 시각은 주입된 `TimeProvider`.
- 상관 ID: 실행 ID = 파일 이름 타임스탬프, 파일 단위 = 작업 번호 `#0001`.
- 줄 형식(탭 구분, 1파일 1줄): `HH:mm:ss.fff<TAB>OK|FAIL|SKIP<TAB>#0003<TAB><원본><TAB><출력|-><TAB><이유|->`. 머리 `START`(엔진·입력 수·출력 폴더), 꼬리 `END`(성공·실패·대기 수, 취소 여부), 안내 `NOTE`. T5.1 은 `FAIL` 줄 수를 센다.
- 전역 예외는 진행 중 RunLog 가 있으면 `ERROR` 줄, 없으면 같은 폴더 `crash-yyyyMMdd-HHmmss.log`.

### D-12 그 밖의 구조 항목
- I/O 경계: COM·레지스트리 → HwpCom 안에만. 파일 시스템 → Core 직접 사용(테스트는 임시 폴더). `IFileSystem` 은 **도입 조건: 로컬 디스크가 아닌 저장소가 생기거나 임시 폴더 테스트가 불안정해질 때**. 시계 → `TimeProvider` 주입(지금).
- 데이터 소유권: 입력 목록·작업 상태 = Core(`InputCollector`·`BatchRunner`), UI 는 이벤트로 받은 사본만 표시. 한/글 인스턴스·레지스트리 값 = HwpCom. 로그 파일 = `RunLog`.
- 패턴: 전략(엔진), 진행은 `IProgress<T>`, DI 컨테이너 없음(`Program.cs` 수동 조립). DI 는 **도입 조건: 진입점이 둘(V2)이고 조립 코드가 30줄을 넘을 때**.
- 캐시: 도입 안 함(처리량 품질 속성 없음). 조건: 같은 파일 반복 변환 요구가 생길 때.
- 설정 파일·다국어: 도입 안 함. 조건: 옵션 UI 요구가 생길 때.

## 근거
- V1: 엔진이 Core 인터페이스 뒤 → 직접 변환 엔진은 새 프로젝트 1개 + `Program.cs` 1줄. Core·HwpCom 수정 0.
- V2: 흐름이 전부 Core(net10.0) → CLI 는 새 프로젝트 `Hwpx2Hwp.Cli`(Core+HwpCom 참조) 1개. App 수정 0.
- V3: 확장자·형식 문자열이 `ConversionRoute`·`HwpFormatMap` 두 곳 → 새 형식 1개 = Core `Model/ConversionRoute.cs` + HwpCom `HwpFormatMap.cs` + App 형식 선택 UI = 3파일(선택 UI 는 어차피 필요).
- C3: WindowsDesktop 런타임이 없는 맥에서 테스트가 돌려면 흐름 코드가 net10.0 프로젝트에 있어야 한다 — Core 분리의 직접 이유.
- COM `dynamic`: 맥 `dotnet` CLI 는 `COMReference` 를 해석하지 못하고, 한/글 2014 타입 라이브러리를 맥에서 import 할 수 없다.

## 검토한 대안
- **대안 A — 단일 App 프로젝트 + 폴더 경계**(테스트는 Core 폴더 소스를 `<Compile Include>` 링크): csproj 가 적다. 경계가 관례뿐이라 Core 폴더에 WinForms 를 써도 App 빌드는 통과하고, CLI(V2) 때 결국 분리해야 한다.
- **대안 B — Core + App 2개(COM 은 App 안)**: 지금 비용 최소. CLI(V2) 가 COM 엔진을 쓰려면 WinForms 프로젝트를 참조하거나 COM 파일 5~6개를 옮겨야 한다. V2 가 확정 축이라 기각.
- **COM interop 어셈블리**: 컴파일 타임 타입 검사. Windows+한/글 환경에서만 생성 가능해 맥 빌드 불가, 다른 버전 tlb 로 2014 에 없는 API 를 쓸 위험. 기각.
- **흐름 전체를 STA 에서 실행**: 단순하지만 맥에서 `SetApartmentState` 가 예외라 가짜 엔진 테스트 불가. 기각 — STA 는 엔진 내부로 숨긴다.

## 결과
- 영향 모듈: Core, HwpCom, App, tests (docs/structure.md).
- 줄 수 상한: 파일 500줄, 메서드 50줄·인자 4개·중첩 4단(린터 없음, 리뷰에서 확인). 생성 파일 없음.
- `FABLE_REQUIRE_FOLDER_MD`: **ON 유지**(승인). `src/<프로젝트>/CLAUDE.md` 3장 + `tests/CLAUDE.md` 1장 + `docs/CLAUDE.md`.
- 루트 파일: `*.sln`·`global.json`·`Directory.Build.props` 허용(승인, `.claude/settings.json` `FABLE_ROOT_ALLOW`).
- 구현 첫 작업: D-9 뼈대(sln·global.json·props·4 csproj) + D-10 승인 검사 1·2.
- 명세 인터페이스 칸 보강용: F1 → `IConversionEngine`/`HwpComEngine.Create`, F2 → `InputCollector`, F3 → `OutputPathPlanner.Reserve`, F4 → `BatchRunner.RunAsync`, F5 → `RunLog`·`BatchReport`, F6 → `HwpComEngine.StartAsync` 의 `Notices`.

## 열린 문제
1. 보안 모듈 DLL 파일 이름과 비트 수: Hwp.exe(한/글 2014)와 같은 비트여야 한다. 우리 exe(x64)와는 무관(out-of-proc). Windows 에서 확인.
2. HWPX 암호 문서의 manifest 표기를 실제 샘플로 확인. 사전 검사가 못 잡으면 `OpenFailed` 로 나와 T1.2 가 어긋난다.
3. 한/글 2014 에서 `Open` 형식 문자열 `"HWPX"`, 인자 `suspendpassword`·`versionwarning`, `SetMessageBoxMode` 지원 여부 Windows 확인. `SetMessageBoxMode` 값(0x00224121)의 비트 의미 미확인.
4. 260자 넘는 경로에서 `SaveAs` 동작 미확인 — 실패 시 `SaveFailed` 로 기록되는 것만 보장.
