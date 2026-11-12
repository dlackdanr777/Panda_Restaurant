# 승인 UI 스프라이트 — Unity 적용 인계

아트 상태: 사용자 최종 승인 완료. Unity 적용·실화면 검수는 미완료입니다.
승인 PNG 30개는 최초 추출본과 SHA-256이 모두 같습니다. 추가 가공·재생성하지 않았습니다.

## 이 문서가 우선합니다
원본 README와 manifest의 CANDIDATE/검수 대기는 추출 시점의 이력입니다.
아트 승인 상태는 approval_manifest.json과 이번 사용자 승인을 우선합니다.
기존 9-slice 후보와 원본 가공 설명은 유지하며 Unity 결과까지 승인된 것은 아닙니다.

## 사용
DEV_UI01_PopupStore_ApprovedSprites_Codex_FINAL.txt 전체를 Codex에 전달합니다.
sprites/에 있는 PNG만 UI 적용 소스로 사용합니다.
review/, references/ 및 원본 시트는 참고 자료이며 런타임 Sprite로 사용하지 않습니다.
원본 시안의 일시정지·별·왕관과 예시 숫자는 적용하지 않습니다.
점수 왼쪽은 기존 승인 C~SSS 보상 상자 원본, 우측은 기존 X 버튼 원본입니다.
기존 재료 32+3+3 PNG·데이터·순서·클릭/스크롤 기능, 손님과 배경은 보존합니다.
하단 외곽 프레임과 배치는 상자-봉제인형-리본, 가운데 가장 넓게 조정합니다.
버튼 WithIcon/Blank는 대안이며 겹쳐 쓰지 않습니다. 모든 30개를 억지로 표시하지 않습니다.

## 작업 경계
이번은 UI 스킨·배치·읽기 전용 등급 HUD 적용입니다.
서버 지급·보상표 변경·횟수 변경·추가 애니메이션·신규 테스트 모드는 제외합니다.
Codex는 Git 작업을 하지 않습니다. 실화면 승인 후 사용자 직접 커밋합니다.

## 보관 위치
승인 폴더: https://drive.google.com/drive/folders/18P4lgQwN6LSnfYsaoDKU6Nw6RT0sSicW
개별 PNG: https://drive.google.com/drive/folders/1jv1AhLMuaT_BUlbr3qOIc7PGQ-AChQmK
