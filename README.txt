GitHub 업로더 v1.7 (C#, git.exe 의존, 브라우저 로그인)
============================================================

[v1.7 변경점]
- 폴더 선택이 commit 입력 중에도 유지됨 (포커스 이동해도 하이라이트 유지)
- commit 바로 위에 선택 중인 대상 경로 표시 (굵게, 예: zagk/레포/폴더)
- 그 외 v1.6과 동일

[로그인 2가지]
A. 브라우저 로그인 (권장): '브라우저로 로그인' 클릭
   - 필요: GitHub CLI(gh) 설치 (https://cli.github.com/)
B. PAT 직접 입력: GitHub 웹 > Settings > Developer settings >
   Personal access tokens > 권한 repo 체크 > ghp_... 붙여넣기
   (DPAPI 암호화 저장, token.dat)

[우클릭 등록]
앱 하단 '탐색기 우클릭 메뉴 등록' 체크박스 (체크=등록, 해제=삭제)
