# 에이전트·게이트 이슈 (최신이 위)
- 2026-11-01 · gate-auditor log · self_protect 1건 오탐 · scratchpad 로 `curl … dotnet-install.sh -o dotnet-install.sh`(내려받기만)가 설치기 이름 규칙으로 DENY → 사용자가 `!` 로 직접 설치해 해결. 제안: install*.sh 는 실행(bash·sh·./)만 막고 내려받기·읽기는 통과(probe 로 규칙 위치 확인 필요, 패키지 쪽 수정)
- 2026-11-01 · gate-auditor log · 브리핑 게이트 문턱 초과(DENY 403/WARN 694)는 전역 합산 탓, 이 프로젝트 DENY 는 2건(위 오탐 1·mem_shell 정상 1) → 브리핑 집계를 프로젝트별로 나누는 개선 제안(패키지 쪽)
