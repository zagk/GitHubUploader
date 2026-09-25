GitHub 업로더 v1.12 (C#, git.exe 의존, 브라우저 로그인)
============================================================

[v1.12 변경점]
- 브랜치 설명줄 원위치 (리스트 밑)
- 로그 200→150px, 기본 창 높이 760→830 (리스트 공간 추가 확보)
- 그 외 v1.11과 동일

[로그인 2가지]
A. 브라우저 로그인 (권장): '브라우저로 로그인' 클릭
   - 필요: GitHub CLI(gh) 설치 (https://cli.github.com/)
B. PAT 직접 입력: GitHub 웹 > Settings > Developer settings >
   Personal access tokens > 권한 repo 체크 > ghp_... 붙여넣기
   (DPAPI 암호화 저장, token.dat)

[우클릭 등록]
앱 하단 '탐색기 우클릭 메뉴 등록' 체크박스 (체크=등록, 해제=삭제)
