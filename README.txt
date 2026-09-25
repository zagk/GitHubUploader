GitHub 업로더 v1.14 (C#, git.exe 의존, 브라우저 로그인)
============================================================

[v1.14 변경점]
- Commit 칸에 원본 이름 자동 입력 (폴더면 폴더명, 파일이면 파일명+확장자)
- 그 외 v1.13과 동일

[로그인 2가지]
A. 브라우저 로그인 (권장): '브라우저로 로그인' 클릭
   - 필요: GitHub CLI(gh) 설치 (https://cli.github.com/)
B. PAT 직접 입력: GitHub 웹 > Settings > Developer settings >
   Personal access tokens > 권한 repo 체크 > ghp_... 붙여넣기
   (DPAPI 암호화 저장, token.dat)

[우클릭 등록]
앱 하단 '탐색기 우클릭 메뉴 등록' 체크박스 (체크=등록, 해제=삭제)
