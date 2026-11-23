# 누리닷 Windows 1.0.7 화면 검증과 남은 작업

2026년 11월 23일 기준. Windows x64, 96 DPI에서 APP_UI의 129개 프레임을 검토했다. 114개 프레임은 대응 화면의 외곽 크기가 일치했다. 리본이나 상태 모음은 구성요소별로 측정했다. 이 문서의 백분율은 지정한 영역에서 RGB 세 값이 정확히 같은 픽셀의 비율이다. 화면마다 문서·계정·선택 상태가 달라 전체 앱을 하나의 백분율로 합산하지 않았다.

## 다음에 확인할 화면

| 화면 | 확인한 범위 | 남은 작업 |
|---|---|---|
| 편집기 C1·C2·C3·C10 | 리본 단추 42개의 아이콘·캡션 84곳, 검토 줄 표본 31곳, 패널 머리와 스크롤 | 같은 문서와 선택 상태에서 본문 글자·점자 기준선과 패널 경계의 픽셀 차이를 줄인다. 실패·재시도 화면은 실제 변환 실패 자료로 다시 확인한다. |
| 디자인 기준표 M1·M4 | 색상 38개 중 31개, 치수 20개 중 19개 | 보조 글자색 ‘세째’, 강조 올림색, 덮개색, 정상 상태 전경·배경, 경고색, 메모 배경색을 해당 상태에서 확인한다. 32 DIP 쪽 막대의 실제 사용처를 확인한다. C1의 쪽 이동 단추는 시안에서 24 DIP다. |
| 아이콘 M8·구성요소 모음 M10 | M8 아이콘 85종과 설치 리소스의 윤곽을 대조했다. M10은 일부 구성요소만 대조했다. | 단추별 상태·색·회전과 M10의 나머지 구성요소를 확인한다. ‘대체 초안’은 시안에서 기본 굵기다. 초안이 있을 때 더 굵게 표시하는 별도 QA 요청은 미반영이다. |
| 마이페이지 E1~E6·기관 관리자 F1 | 요금제·크레딧·청구서·계정 메뉴와 기관 관리자 창의 크기, 일부 배경 포함 화면 | 개인 결제 수단 선택·승인·취소는 결제대행사 결정과 서버 연동이 필요하다. 청구서 세액·수신인, 기관 권한과 잠금 상태는 실제 운영 자료와 대조한다. |
| 로그인·회원가입·약관·문의 | 계정 창과 문서 열기, 문의 입력 흐름 | 소셜 로그인 직후의 창 전환을 운영 계정에서 확인한다. 약관·방침 본문과 새 문의 창의 최종 시안이 확정되면 다시 대조한다. |
| 점자 규정 찾아보기·적용 규정 | 목차·검색·빈 결과를 설치본에서 대조했다. 실제 조항을 띄운 목차 화면은 74.18%, 검색 화면은 79.26%였다. | 같은 조항·검색어로 줄바꿈, 표, 긴 예문을 확인하고 운영 서버의 Markdown 목차 응답을 검증한다. |
| 검토 표시·대체초안·수식·점자 인쇄 | 표시와 선택, 수식 입력·미리 보기, 인쇄 설정의 앱 동작 | 정상 요소의 자동 검토 표시 판정, 서버가 처음 만드는 초안의 유형 필터, 지정 해설 PDF의 기대 점자 ID, 실물 점자 출력은 추가 확인이 필요하다. |
| 메시지창 Case 1~30·메뉴 | 메시지창의 단추 위치, 메뉴와 전체 창의 RGB 표본 | 아이콘과 한글 글자의 가장자리, 키보드 포커스, DPI 배율별 줄바꿈을 확인한다. |

11월 21일 QA의 현재 범위 33개 중 20개를 확인했고, 7개는 일부 반영, 5개는 미반영, 1개는 미확인이다. 추후 과제 4개는 별도다. 홈페이지 요금제·가입·영문 화면과 운영 서버 변경은 Windows 릴리스 이후 별도 배포가 필요하다.

## 화면별 대조 결과

‘외곽 크기 일치’로 기록한 화면은 크기까지 확인했다. 백분율이 있는 화면에는 측정 범위를 함께 적었다. 운영체제 공용 창은 동작을 확인했다. 각 화면 이름은 해당 Figma 원본으로 연결된다.

### 디자인 기준과 리본

| 화면·상태 | 시안 크기 | 확인 범위 |
|---|---:|---|
| [M1 색 — 변수 「색/…」](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-1452) | 1120×702 | 동일 위치에서 31/38색 확인 |
| [M2 검토 상태 — 색만으로 알리지 않는다(아이콘 · 글 같이)](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-1652) | 980×166 | 검토·메모 상태 5종 대조 |
| [M3 글자 — Windows 타입 램프](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-1712) | 980×692 | 글자 14종 대조, 점자 대체 글꼴 사용 |
| [M4 치수 — 변수 「간격/…」 · 「둥글기/…」](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-1772) | 980×450 | 19/20개 치수 일치. 쪽 막대 32 DIP의 적용 화면 확인 필요 |
| [M5 단추 — 컴포넌트 「단추」 갈래 셋](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-1845) | 980×166 | 단추 상태 3종·위험 동작 6종 대조 |
| [M6 칸과 고르기 — 컴포넌트 「입력/…」](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-1857) | 980×170 | 입력·선택 상태 10종 대조 |
| [M7 작은 조각 — 리본 · 편집기 부품](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-1906) | 980×318 | 리본 부품 8종·패널 머리 3종 대조 |
| [M8 아이콘 — Fluent System Icons(20 regular) 공식 SVG](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-2086) | 980×702 | 85종 중 29종의 벡터 경로 일치, 78종에서 윤곽 겹침 85% 이상 |
| [M9 전체 화면 · 모달이 뜨는 자리](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-6564) | 1440×1000 | 조판 창과 뒤쪽 편집기 포함 82.51% |
| [D0 리본 6탭 전체](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-9772) | 1520×1130 | 6개 탭 전체 92.14~97.30%, 단추 내부 84곳의 위치·크기 일치 |
| [D0-1 리본 드롭다운 펼침](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-9787) | 2402×332 | 펼친 메뉴 9종 본체 93.14~97.27% |
| [M10 컴포넌트 — 리본 · 창 · 편집기 · 아이콘](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-89) | 1600×910 | 전체 구성요소와 상태별 대응 미완료 |

### 계정·마이페이지·기관 관리

| 화면·상태 | 시안 크기 | 확인 범위 |
|---|---:|---|
| [E1 마이페이지](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-2749) | 1440×1000 | 외곽 크기 일치 |
| [E1-1 마이페이지 · 상세 내역](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-2885) | 1440×1000 | 외곽 크기 일치 |
| [E1-2 마이페이지 · 결제 내역](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-3100) | 1440×1000 | 외곽 크기 일치 |
| [E1-3 마이페이지 · 청구서](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-3289) | 1440×1000 | 배경 포함 88.40% |
| [E1-5 마이페이지 · 계정 메뉴](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-3520) | 1440×1000 | 배경 포함 96.83% |
| [E2 요금제 업그레이드](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-3673) | 1440×1000 | 배경 포함 92.00% |
| [E3 추가 크레딧 구매](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-3872) | 1440×1000 | 배경 포함 92.28% |
| [E4 이메일 변경](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-4061) | 1440×1000 | 배경 포함 94.47% |
| [E5 비밀번호 변경](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-4226) | 1440×1000 | 배경 포함 94.55% |
| [E6 회원 탈퇴](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-4390) | 1440×1000 | 배경 포함 94.13% |
| [F1 기관 관리자 메인 화면](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-5242) | 1440×1000 | 외곽 크기 일치 |
| [계정 발급 요청](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-5506) | 640×318 | 외곽 크기 일치 |
| [크레딧 추가 요청](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-5544) | 600×250 | 외곽 크기 일치 |
| [계정 상세 · kblib02](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-5591) | 560×462 | 외곽 크기 일치 |
| [계정 상세 · kblib03](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-5647) | 560×462 | 외곽 크기 일치 |
| [공지 · 크레딧 잔여 안내](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-5703) | 560×206 | 외곽 크기 일치 |
| [주문 상세](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-5721) | 560×382 | 외곽 크기 일치 |
| [계정 메뉴](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-6071) | 220×135 | 전체 94.49% |
| [A1 로그인](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-11589) | 1440×1000 | 외곽 크기 일치 |
| [A3 로그인 › 문의하기 창](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-11664) | 820×510 | 외곽 크기 일치 |
| [A2 회원가입](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-11732) | 1440×1000 | 외곽 크기 일치 |
| [누리닷 서비스 이용약관](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-11821) | 640×522 | 전체 88.56% |
| [누리닷 개인정보 처리방침](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-11842) | 640×522 | 전체 89.00% |
| [광고성 정보 이메일 수신 동의](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-11861) | 520×418 | 전체 95.56% |

### 게시판·도움말·규정

| 화면·상태 | 시안 크기 | 확인 범위 |
|---|---:|---|
| [알림 목록](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-6087) | 400×354 | 전체 96.34% |
| [적용 규정](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-10446) | 900×418 | 전체 96.90% |
| [점자 규정 찾아보기 · 처음](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-10491) | 1100×760 | 전체 98.72% |
| [점자 규정 찾아보기 · 목차](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-10552) | 1100×760 | 전체 74.18% |
| [점자 규정 찾아보기 · 검색](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-10737) | 1100×760 | 전체 79.26% |
| [점자 규정 찾아보기 · 없음](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-10925) | 1100×760 | 전체 98.81% |
| [중요 공지](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-10990) | 520×238 | 전체 96.02% |
| [G7 문의하기](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-11009) | 1440×1000 | 전체 99.16% |
| [G1 게시판 · 목록](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-11083) | 1440×1000 | 전체 97.60% |
| [G1-1 게시판 · 글쓰기](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-11253) | 1440×1000 | 전체 99.11% |
| [G1-2 게시판 · 글 누름](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-11353) | 1440×1000 | 전체 98.46% |
| [G5 도움말](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-11433) | 1100×700 | 도움말 전체 97.48% |
| [누리닷 정보](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-11560) | 440×302 | 전체 97.72% |

### 메시지창

| 화면·상태 | 시안 크기 | 확인 범위 |
|---|---:|---|
| [메시지창 Case 1](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-11901) | 480×178 | 전체 94.31% |
| [메시지창 Case 2](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-11916) | 480×178 | 전체 94.41% |
| [메시지창 Case 3](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-11931) | 480×178 | 전체 95.05% |
| [메시지창 Case 4](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-11946) | 480×250 | 전체 92.82% |
| [메시지창 Case 5](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-11964) | 480×198 | 전체 94.81% |
| [메시지창 Case 7](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-11977) | 520×178 | 전체 94.48% |
| [메시지창 Case 8](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-11994) | 480×178 | 전체 95.19% |
| [메시지창 Case 9](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-12009) | 520×178 | 전체 94.29% |
| [메시지창 Case 10](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-12024) | 560×178 | 전체 93.71% |
| [메시지창 Case 11](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-12041) | 480×178 | 전체 96.17% |
| [메시지창 Case 12](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-12054) | 600×178 | 전체 92.84% |
| [메시지창 Case 13](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-12071) | 520×178 | 전체 94.35% |
| [메시지창 Case 14](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-12088) | 480×178 | 전체 94.99% |
| [메시지창 Case 15](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-12103) | 480×178 | 전체 94.71% |
| [메시지창 Case 16](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-12118) | 520×178 | 전체 93.94% |
| [메시지창 Case 17](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-12133) | 480×178 | 전체 95.37% |
| [메시지창 Case 18](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-12146) | 480×178 | 전체 95.41% |
| [메시지창 Case 19](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-12159) | 480×178 | 전체 96.27% |
| [메시지창 Case 20](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-12172) | 520×178 | 전체 94.36% |
| [메시지창 Case 21](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-12189) | 480×178 | 전체 94.90% |
| [메시지창 Case 22](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-12204) | 480×178 | 전체 95.77% |
| [메시지창 Case 23](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-12217) | 520×178 | 전체 94.63% |
| [메시지창 Case 24](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-12232) | 480×178 | 전체 94.47% |
| [메시지창 Case 25](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-12247) | 560×178 | 전체 90.94% |
| [메시지창 Case 26](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-12262) | 520×178 | 전체 94.99% |
| [메시지창 Case 27](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-12277) | 480×178 | 전체 93.05% |
| [메시지창 Case 28](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-12292) | 480×178 | 전체 96.26% |
| [메시지창 Case 29](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-12305) | 440×146 | 전체 95.17% |
| [메시지창 Case 30](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-12319) | 520×178 | 전체 90.10% |

### 편집기·문서·설정

| 화면·상태 | 시안 크기 | 확인 범위 |
|---|---:|---|
| [C1 메인 화면 · 3패널 편집기](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-4550) | 1440×1000 | 원문 선택 두 줄 85.92%, 연결 점자 한 줄 90.79%. 검토·현재 줄 표본 31곳 일치 |
| [C3 2패널 · TXT · 새 문서](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-4933) | 1440×1000 | 두 패널 화면과 원문·점자·원본 패널 머리 별도 대조 |
| [C4 줄번역 띠](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-5759) | 820×274 | 줄번역 첫 줄 96.72%, 다음 줄 96.59% |
| [B4 크레딧 소진 · 이어서 처리 띠](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-5802) | 1440×228 | 크레딧 소진 띠 1438×40 대조 |
| [B2 상태 표시줄 · 작업 상태](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-5942) | 1440×556 | 작업 상태 6종, 취소 메뉴 별도 대조 |
| [수식 미리보기 띠](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-6124) | 760×102 | 전체 96.82% |
| [상황 메뉴 · 텍스트 패널](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-6142) | 448×366 | 불투명 영역 93.46% |
| [상황 메뉴 · 점자 패널](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-6258) | 448×389 | 불투명 영역 94.10% |
| [상황 메뉴 · 메모 말풍선](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-6351) | 248×284 | 전체 96.11% |
| [상황 메뉴 · 원본 패널](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-6418) | 248×42 | 불투명 영역 96.56% |
| [메모 입력](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-6430) | 320×158 | 전체 94.42% |
| [메모 말풍선](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-6441) | 360×178 | 전체 93.28% |
| [대체 초안 판 · 바텀시트](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-6477) | 1140×219 | 패널 95.22% |
| [원본 패널 · 파일 올리기](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-6507) | 380×560 | 패널 94.80% |
| [점역 옵션 · 열기](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-7098) | 600×714 | 전체 96.89% |
| [쪽 다시 변환](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-7210) | 600×602 | 전체 97.25% |
| [이미지 올리기](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-7294) | 900×578 | 전체 97.82% |
| [[OS 창] 다른 이름으로 저장](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-7387) | 560×310 | Windows 공용 창. 이름·폴더·저장 형식 동작 확인 |
| [범위 지정 저장](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-7412) | 560×250 | 전체 97.36% |
| [범위 지정 저장 · 점자 기준](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-7474) | 560×250 | 전체 97.17% |
| [B7 미리 보기 · 텍스트 페이지](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-7537) | 1440×1000 | 전체 94.66% |
| [B7-1 미리 보기 · 점자 페이지](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-7642) | 1440×1000 | 전체 95.54% |
| [점자 면 미리 보기](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-7747) | 720×632 | 전체 95.33% |
| [점자 인쇄](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-7800) | 480×274 | 전체 97.93% |
| [이동](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-7863) | 480×218 | 전체 98.03% |
| [메모 목록](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-7894) | 640×316 | 전체 95.97% |
| [검토 표시 목록](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-7933) | 640×284 | 전체 96.44% |
| [특수 기호 넣기](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-7969) | 600×364 | 전체 97.34% |
| [상용구 설정](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-8067) | 760×574 | 전체 97.19% |
| [편집 화면 설정 · 문단 모양](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-8169) | 560×599 | 전체 97.34% |
| [편집 화면 설정 · 편집창 색상](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-8306) | 560×711 | 전체 96.99% |
| [찾기 · 바꾸기](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-8395) | 520×262 | 전체 97.54% |
| [찾기 · 바꾸기 · 점자로 찾기](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-8433) | 520×262 | 전체 97.37% |
| [기본 점역 옵션](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-8473) | 560×670 | 전체 96.36% |
| [조판 설정](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-8601) | 560×738 | 전체 97.00% |
| [6점자 키설정 · 1단](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-8760) | 640×326 | 전체 97.69% |
| [6점자 키설정 · 2단](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-8814) | 640×326 | 전체 97.70% |
| [6점자 키설정 · 3단](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-8868) | 640×326 | 전체 97.69% |
| [6점자 키설정 · 사용자](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-8922) | 640×326 | 전체 97.68% |
| [점역 양식](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-8976) | 560×326 | 전체 96.41% |
| [사용자 양식 설정](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-9027) | 560×446 | 전체 96.19% |
| [사용자 양식 지우기](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-9135) | 440×170 | 전체 95.10% |
| [머리말](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-9151) | 520×218 | 전체 97.22% |
| [꼬리말](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-9185) | 520×262 | 전체 97.40% |
| [꼬리말 관리](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-9233) | 720×380 | 전체 95.47% |
| [대체 초안 유형](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-9355) | 680×442 | 전체 98.34% |
| [C10 메인 화면 · 원본 패널 변환 실패](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-9433) | 1440×1000 | 오류 페이지 외곽 대조. 실제 실패 자료로 재시도 경로 추가 확인 |
| [C2 메인 화면 · 패널 모두 끔](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-10018) | 1440×1000 | 외곽 크기 일치 |
| [표 편집](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-10193) | 760×526 | 외곽 크기 일치 |
| [수식 편집기](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-10283) | 900×558 | 전체 96.68% |
| [새 판 알림](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-11886) | 560×66 | 전체 92.32% |
