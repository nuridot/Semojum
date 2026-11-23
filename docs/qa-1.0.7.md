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

| 판정 | 남은 QA 항목과 다시 확인할 조건 |
|---|---|
| 일부 반영 7개 | A3 개인 결제 승인·취소, A6 운영 서버의 규정 Markdown 목차, B1 운영 홈페이지 약관·방침, B2 서버가 AI에 전달하는 시각자료 유형, B4 홈페이지 영어 화면에 남은 한글, B6 소셜 로그인 직후 잠깐 나타나는 로그인 창, B19 문의 창 최종 시안 |
| 미반영 5개 | B3 홈페이지 일반 요금제의 문의 연결, B7 홈페이지 가입 시안과 B8 운영 가입 화면의 소셜 버튼 제거, B9 정상 요소의 검토 표시 오탐, B17 대체초안이 있을 때 아이콘 굵기 구분 |
| 미확인 1개 | A1 지정 정답·해설 PDF와 기대 점자 ID의 대응표를 확보해 변환 결과와 대조 |

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

## 릴리스 이후 작업본 검증 · 2026년 11월 23일

위 표는 공개한 1.0.7의 기록이다. 다음 결과는 후속 로컬 빌드에서 확인했다.

릴리스 당시 확인한 20개에 후속으로 검증한 9개(A1, B2, B3, B4, B6, B8, B9, B17, B19)를 합해 현재 범위 33개 중 29개를 확인했다. 추후 과제 4개는 이 집계에서 제외했다.

| 남은 항목 | 현재 판정 | 완료를 확인할 조건 |
|---|---|---|
| A3 개인 결제·청구서 | 시험 주문의 견적·취소와 청구서 저장을 확인했다. | 결제대행사 선정 후 실제 승인·취소 결과와 발급 청구서의 수신인·세액을 대조한다. |
| A6 점자 규정 Markdown | 로컬 인증 서버에서 340개 조항을 열었다. 운영 명세에는 기존 규정 목록 경로만 있다. | 서버에 목차를 배포한 뒤 로그인한 Windows 앱에서 조항 본문을 확인한다. |
| B1 홈페이지 약관·개인정보 처리방침 | 개인 탈퇴 후 5일 이내 파기가 미충족이다. 이번 QA에서는 이 사실을 기록했다. | 처리방침 게시 전 파기 실행 결과와 해외 처리업체의 실제 보유 조건 확인이 남았다. |
| B7 홈페이지 가입 시안 | 홈페이지 작업본의 한국어·영어 데스크톱과 한국어 모바일 가입 화면에는 소셜 버튼이 없다. 원본 시안에는 버튼이 남아 있다. | 홈페이지 Figma 원본 편집 권한으로 세 가입 프레임을 수정하고 다시 대조한다. |

### Windows 1.0.7 선택 업데이트 등록 정보

2026년 11월 23일 20시 47분(한국시간), 시작 메뉴의 누리닷 1.0.6에서 쓰는 버전 비교 코드로 확인했다. 로그인 전 운영 응답이 안내한 버전은 1.0.5였고, 설치본은 업데이트 없음으로 판단했다. 아래 1.0.7 등록 후보 값을 같은 코드에 넣으면 ‘업데이트 가능’은 참, ‘필수 업데이트’는 거짓이었다. 이번 확인 범위는 등록 정보 기록이다. [운영 버전 응답](https://api.semojum.app/api/public/app-version) · [GitHub 릴리스 노트](https://github.com/nuridot/Semojum/releases/tag/v1.0.7)

| 등록 항목 | 1.0.7 후보 값 |
|---|---|
| 플랫폼 · 채널 · 대상 | Windows · 정식(STABLE) · 전체(ALL) |
| 배포 버전 · 최소 지원 버전 | 1.0.7 · 1.0.0(현재 운영 최소값 유지) |
| 파일 | [Windows x64 ZIP](https://github.com/nuridot/Semojum/releases/download/v1.0.7/Semojum-win-x64.zip) · 146,566,544바이트 |
| ZIP SHA-256 | `60b1a16bdfd39a67d86eb62b718b3917a5b8ab5b1192b8defd522bdf541aaacc` |
| 앱에 표시할 안내 문안 | 앱 시작 시 새 버전을 안내하고, 한글 입력 시 조합창이 겹쳐 보이던 문제를 수정했습니다. |
| 서명 검사 | 공개 MSI와 ZIP 안의 `Semojum.Mockup.exe` 모두 Authenticode `NotSigned` |

로그인 없이 ZIP 주소에 접속했을 때 HTTP 200과 같은 파일 크기를 확인했다. ZIP의 SHA-256도 GitHub의 업로드 값과 일치했다. 정식 채널 공개 조건에는 릴리스 노트 승인과 서명 확인이 포함된다. 서명된 파일을 새로 패키징하면 배포할 ZIP의 해시와 크기를 다시 기록해야 한다.

### 화면별 후속 검증

| 화면 | 확인한 동작 | 남은 범위 |
|---|---|---|
| 마이페이지 › 문의 내역 › 새 문의 | 최종 시안 G7을 연다. 개인 계정의 회신 주소가 채워지고, 전송 실패 시 입력을 유지하며, 성공하면 내 문의 내역을 다시 읽는다. 비밀글 목록에는 분류 이름을 쓰고 직접 입력한 제목·메일·기관명은 본문에 보관한다. | 후속 빌드 배포 |
| A1 해설지의 묵자·점자 블록 선택 | 프로젝트 첨부본 10쪽을 로컬 OCR로 변환했다. 본문 680개와 점자 ID가 모두 대응했다. Windows에서 새 점역과 저장 점자 복원 모두 블록 680개를 양방향으로 선택했고 4쪽은 47개 모두 통과했다. 저장된 8점 문자 `⣿`의 6점 BRF 대체로 모눈 전체가 비던 문제를 수정했다. | 후속 Windows 빌드 배포. 시연 당시 파일의 지문은 녹취에 없다. [시연 녹취](https://app.notion.com/p/41a43813bc2e80339120d440bfcd45cd#41a43813bc2e80ba9853e8e40727db25) · [점검한 PDF의 출처](https://app.notion.com/p/3ca43813bc2e80daa542c2907a181770) |
| 대체 초안·검토 표시 | 사용할 초안이 있을 때 리본 아이콘이 굵어진다. 설정한 유형과 기본안을 서버 요청과 묵자·점자 결과에 연결했다. 일반 품질 안내만 있는 줄은 경고를 만들지 않으며 수동 표시는 보존한다. | Windows 및 변환 서비스 배포 |
| 수식 적용 규정 | 해설지 첫 쪽에서 옮긴 수식 표본 5개를 점역했다. 분수·근호·거듭제곱·극한이 적용 규정 ID를 반환했고 단순식에는 구조 규정이 붙지 않았다. 수정 전후 점자 칸과 줄바꿈 후보가 같았다. | 후속 빌드 배포 |
| 점자 규정 찾아보기 | Windows 앱을 로컬 인증 서버에 연결해 세 문서의 Markdown 조항 340개를 확인했다. 640 DIP 창에서 제33항 검색 카드를 펼치면 긴 첫 문단 끝까지 나오고, 접으면 원래 높이로 돌아왔다. 규정 검사 62개 통과. | 운영 서버 신규 목차 배포와 로그인 검증 |
| 크레딧 구매 E3 | 할인 표지에 M1 정상 글자색과 배경색을 적용했다. 모의 견적으로 확인했다. | 결제대행사 선정과 실결제 승인·취소 |
| 개인 결제 내역·청구서 E1-2·E1-3 | 최근 12개월의 결제·환불은 결제일, 미결제 주문안은 생성일을 기준으로 조회한다. 13개월 전 주문에 최근 결제 시각과 시험 청구서를 등록했다. Windows 마이페이지에서 ‘PDF 내려받기’를 눌러 저장을 취소하고 다시 저장했다. 저장한 파일에서 시험 수신인·공급가액 4,680원·부가세 468원·합계 5,148원 등 7개 항목을 읽었다. 파일이 서버 표본과 바이트 단위로 일치했다. | 상세 창에는 주문에서 받은 결제일·품목·합계가 나온다. 청구 번호·받는 곳·공급가액·부가세·결제 수단은 응답에 없어 ‘—’로 표시한다. 결제대행사의 실제 승인·취소, 발급 자료와의 대조, 후속 배포가 남았다. |
| 메시지창 29종 | WPF 레이아웃을 100·125·150·200%로 렌더링한 116개 조합을 검사했다. 이 PC의 150% 모니터에서 100% 모니터로 옮겼다가 되돌린 29개 창도 처음 표시와 왕복 87회 모두 Windows와 WPF의 배율이 일치했다. 본문과 단추가 잘리거나 겹친 경우는 0건이었다. | 125·200%는 오프스크린 렌더링으로 검증 |
| M10 | 원본 아이콘 크기 변형 154개와 Windows 리소스의 윤곽을 대조했다. 화면 부품 31종 중 24종은 RGB, 4종은 외곽까지 대조했다. | 쪽 번호 단추 3종의 적용처와 M4의 32 DIP 쪽 막대, 문서 상태에 따른 편집기 본문 차이 |

점자 규정 API는 서버 `dev`의 `a66560e`에서 분리한 후속 로컬 커밋 `322cfa1`로 다시 검사했다. 서버·연동 테스트 22개가 통과했고, 인증된 Windows 앱에서 목차의 340개 조항과 화면 동작 62개를 확인했다. 운영 배포 및 로그인 확인이 남았다.

화면 가장자리에서 왼쪽으로 펼쳐지는 ‘글자 크기’ 하위 메뉴와 부모 메뉴 사이의 간격을 같은 위치에서 측정했다. 처음에는 10픽셀 떨어졌고, 하위 메뉴 자신의 배율로 그림자 여백을 계산한 뒤에는 0픽셀이었다. 표·메모 화면에서는 WPF가 미리보기 창을 처음 표시하며 배율을 조정할 때 창이 활성화되어 편집기 포커스를 가져갔다. 후속 수정으로 마우스만 올린 네 편집 위치 모두 입력 포커스가 유지되고, 포인터가 떠나면 약 0.3초 뒤 창이 닫혔다. 답글 입력란을 직접 선택하면 계속 쓸 수 있었다. 일반 실행 배율의 포인터·키보드 검사까지 포함한 표·메모 검사 81개가 통과했다.

원문 편집기에서 메모·검토 아이콘의 선 사이를 클릭하면 아무 반응이 없는 경우도 재현했다. 각 아이콘의 16 DIP 영역 전체에 클릭을 받도록 수정한 뒤 원문·점자 모눈 모두 메모와 검토 콜백을 각각 한 번씩 호출했다. 일반 실행 배율에서 편집기 검사 291개가 통과했다. 한글 입력기·글꼴 검사 40개에서도 ‘ㅎ → 하 → 한’의 인라인 조합과 포커스 이동 시 원래 편집기에 한 번만 확정되는 동작을 확인했다.

최종 후속 Windows 빌드의 통합 검사 2,876개가 통과했고 실패와 건너뜀은 0개였다. 실제 마우스와 키보드로 원문·점자 표시 클릭, 메모 포커스, 창 단추 조작, 한글 조합 중 창 전환을 확인했다. 이 수치는 같은 동작을 재검사한 영역별 결과와 일부 겹친다.

개인 결제 DB 변경의 번호가 서버에 머지된 문의 접수 경로(V99)와 겹쳐 앱 서버 기동이 실패하는 경우를 재현했다. 미배포 개인 결제 변경을 V104로 옮긴 후, 운영 스키마 스냅샷 기반 임시 DB에서 두 변경의 적용 순서와 Windows 청구서 조회 등 통합 검사 5개가 통과했다.

현행 서버 `dev` 커밋 `a66560e`에 후속 변경 전체를 적용한 로컬 검증본은 `0efda2a`이다. 임시 DB에서 V99~V104가 순서대로 적용됐고, 이 서버에 로그인한 Windows 앱에서 규정 화면 검사 62개가 통과했다. 서버 회귀 검사 1,313개 중 1,306개가 통과했다. Windows에서 POSIX 파일 권한 설정에 실패한 HWP 사례 10개는 동일한 빌드로 Linux에서 통과했다. 실파일 점검·진단·벤치마크 등 7개는 건너뛰었다.

개인정보 처리방침 제3조의 개인 회원 탈퇴 후 5일 이내 파기 요건은 이번 QA에 미충족으로 기록한다. 임시 DB에서 실제 탈퇴 후 탈퇴일을 6일 전으로 설정해 확인했다. 로그인 이메일과 앱 이벤트는 지워졌고, 작업 접속 정보·원본 파일 참조·쪽 PDF 참조·작업 메모는 각각 1건 남았다. 담당자가 별도의 운영 파기 작업도 없다고 확인했다. 다른 회원이 원본을 사본으로 저장한 경우, 원본 회원이 탈퇴해도 두 작업이 같은 쪽 PDF와 미리보기 경로를 참조했다. 저장소의 파일 자체와 백업은 검사 범위 밖이다.

홈페이지 작업본에서도 개인 요금제 4종의 문의 연결, 영문 전환, 가입 화면의 소셜 버튼 제거를 확인했다. 기존 회원·신규 회원·오류의 소셜 로그인 복귀 경로는 모의 응답으로 검사했다. 서버 작업본에서 개인 회원의 탈퇴 처리 로그에 이전 로그인 ID가 남던 문제를 고쳤고 소셜 계정 통합 검사 61개를 통과했다. 개인정보 처리방침의 파기·국외 처리 조건과 홈페이지 Figma 원본 수정은 남아 있다. 로컬에서 검증한 규정 목차의 파일 지문은 GitHub 정본과 같았고, 운영 공개 명세에는 기존 규정 목록만 등재돼 있었다. [점자 규정 정본](https://github.com/nuridot/braille-reference/tree/34720bddd770be144f2db7c4ae5865e3b6b5ad97) · [운영 OpenAPI](https://api.semojum.app/v3/api-docs)
