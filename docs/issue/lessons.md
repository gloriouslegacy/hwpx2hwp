# 발주 교훈 (최신이 위)
- implementer 발주의 `Spec:` 첫 토큰은 반드시 `docs/spec/<범위>.md F<n>` (baseline·requirements 경로만 쓰면 게이트가 명세 없음으로 거부) → 명세 T 밖 항목도 관련 F 를 적고 근거 문서는 괄호로
- WSL 개발 환경은 dotnet 이 PATH 에 없음 → 발주 Note 에 `export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH &&` 를 넣을 것(Windows dotnet.exe 는 SDK 9 라 global.json 불일치)
- 레포에 .claude/·.git 이 없으면 게이트가 docs/ 쓰기도 거부 → 새 프로젝트는 .claude/CLAUDE.md 를 먼저 만든 뒤 발주
- App csproj 에 RID 없이 SelfContained 를 켜면 맥 빌드 NETSDK1082 → SelfContained 묶음은 RuntimeIdentifier 조건부 유지
- 테스트 이름 `T번호_설명` 은 CA1707 에 걸림 → 테스트 csproj 의 NoWarn CA1707 을 지우지 말 것
- 아키텍처 테스트는 주석의 dynamic·HWPFrame 단어도 위반으로 잡음 → Core·tests 주석에 그 단어를 쓰지 말 것
