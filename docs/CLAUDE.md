# docs/
역할: 요구사항·명세·설계·로드맵·테스트 절차·배포 동봉 문서. 코드는 두지 않는다.
루트 파일: mem.md(세션 메모, 메인만) · map.md(자동 생성, 수정 금지) · structure.md · roadmap.md · CLAUDE.md
| 폴더 | 내용 |
|---|---|
| requirements/ | 요구사항(S·Q·C·V·D 번호) |
| spec/ | 기능명세(F·T 번호) |
| design/ | 설계 결정(baseline.md 등) |
| test/ | 자동화되지 않는 수동 테스트 절차(windows-manual.md) |
| release/ | 배포 zip 에 넣는 README.txt(F8, tools/package.py 가 BOM·CRLF 로 넣음) |
| issue/ | agent-issue.md·lessons.md(메인만) |
규칙: 명세에 없는 확인 항목은 T 번호 대신 X 번호를 쓴다.
규칙: 모든 .md 는 500줄 이하.
규칙: release/README.txt 의 버전·기능 설명은 spec 과 맞춘다(버전 바꿀 때 같이 고친다).
