# LevelPlay 광고 테스트 가이드

## 📋 개요

Unity LevelPlay SDK 연동 완료 후 테스트를 위한 가이드입니다.  
**콘솔 설정이 아닌 SDK 코드 레벨에서 Test Mode를 설정**하여 테스트합니다.

---

## ✅ 현재 상태

### 연동 완료 항목
- ✓ Unity LevelPlay SDK 연동
- ✓ 광고 네트워크 연결
  - ironSource Ads
  - Unity Ads
  - Google AdMob
- ✓ Ad Units 설정
  - Rewarded (보상형 광고)
  - Interstitial (전면 광고)
- ✓ API Key / Core ID 설정 완료

### 코드 수정 완료
- ✓ SDK 초기화 로그 개선
- ✓ 광고 로드/실패 상세 로그 추가
- ✓ Test Mode 설정 추가

---

## 🔧 Test Mode 설정 방법

### 1. LevelPlayBoot 설정

**파일 위치:** `Assets/Scripts/LevelPlayBoot.cs`

인스펙터에서 `LevelPlayBoot` 컴포넌트를 찾아 설정:

```
┌────────────────────────────────────┐
│ Level Play Boot (Script)           │
├────────────────────────────────────┤
│ App Key: YOUR_LEVELPLAY_APP_KEY    │
│                                    │
│ ▼ Test Mode 설정                  │
│   Enable Test Mode: ☑ (ON/OFF)   │
└────────────────────────────────────┘
```

### 2. Test Mode 상태별 동작

#### ✅ Test Mode = ON
- ⚠ **테스트 광고 표시됨**
- 실제 광고 재고가 아닌 테스트 광고 사용
- 로그에 다음과 같이 표시:
  ```
  [LevelPlayBoot] ⚠⚠⚠ TEST MODE = ON ⚠⚠⚠
  [LevelPlayBoot] 테스트 광고가 표시됩니다.
  [LevelPlayBoot] 릴리즈 빌드 전 반드시 OFF로 변경하세요!
  ```

#### ✅ Test Mode = OFF
- ✓ **실제 광고 표시됨** (프로덕션 모드)
- 실제 광고 네트워크에서 광고 수급
- 로그에 다음과 같이 표시:
  ```
  [LevelPlayBoot] ✓ TEST MODE = OFF (프로덕션 모드)
  [LevelPlayBoot] 실제 광고가 표시됩니다.
  ```

---

## 📱 테스트 절차

### Step 1: Test Mode ON 빌드
1. Unity 에디터에서 `LevelPlayBoot` 컴포넌트 선택
2. `Enable Test Mode` 체크박스 **ON** (☑)
3. 안드로이드 빌드 실행
4. 실제 단말에 설치 및 실행
5. **로그 확인** (중요!)

### Step 2: Test Mode OFF 빌드
1. Unity 에디터에서 `LevelPlayBoot` 컴포넌트 선택
2. `Enable Test Mode` 체크박스 **OFF** (☐)
3. 안드로이드 빌드 실행
4. 실제 단말에 설치 및 실행
5. **로그 확인** (중요!)

---

## 🔍 확인 요청 로그 목록

### ① SDK 초기화 확인

**로그 예시:**
```
[LevelPlayBoot] ★★★ initialization success ★★★
[LevelPlayBoot] User ID: xxxxxxxxxx
[LevelPlayBoot] Is Test Suite Enabled: True/False
```

**확인 항목:**
- [ ] `initialization success` 로그가 출력되는가?
- [ ] 초기화 실패 시 `initialization failed` 로그와 에러 코드가 표시되는가?

---

### ② 광고 네트워크 어댑터 로딩 확인

**로그 예시:**
```
adapter loaded (ironSource)
adapter loaded (UnityAds)
adapter loaded (AdMob)
```

**확인 항목:**
- [ ] 어떤 네트워크 어댑터가 로딩되는가?
- [ ] 3개 모두 로딩되는가? 일부만 로딩되는가?

> **중요:** 
> - 어댑터가 하나도 로딩 안 되면 → **네트워크 연동 문제**
> - 일부만 로딩되면 → 해당 네트워크만 사용 가능 (정상)

---

### ③ 광고 요청 결과 확인

**로그 예시:**
```
[WatchAdButton] ★★★ ad loaded ★★★ - h2fx7fmmr5w53rkr
[WatchAdButton] Ad Network: UnityAds
[WatchAdButton] Instance Id: xxxxxxxxxx
```

**또는 실패 시:**
```
[WatchAdButton] ● no fill ● - h2fx7fmmr5w53rkr (광고 재고 부족 - 정상 상황)
```

**또는:**
```
[WatchAdButton] ● ad not available ● - h2fx7fmmr5w53rkr
[WatchAdButton] Error Code: 509
```

**확인 항목:**
- [ ] 광고 로드 성공 시 어떤 네트워크에서 로드되는가?
- [ ] `no fill` 또는 `ad not available` 로그가 표시되는가?
- [ ] 에러 코드는 무엇인가?

> **참고:** 
> - **Error Code 509 = no fill** → 정상 (광고 재고 부족)
> - **Error Code 1037 = ad not available** → 광고 준비 안 됨
> - `no fill`이 계속 발생하는 것은 **문제가 아님**

---

## 📊 로그 기준 판단 가이드

| 상황 | 판단 | 조치 |
|------|------|------|
| `initialization success` 없음 | SDK 설정 문제 | appKey, 콘솔 설정 확인 |
| `adapter loaded` 안 나옴 | 네트워크 연동 문제 | 네트워크별 SDK 설정 확인 |
| `adapter loaded` + `no fill` | **정상** (재고 부족) | 대기 또는 다른 네트워크 확인 |
| `ad loaded` 성공 | 해당 네트워크 수요 존재 | **정상 작동** |

---

## ⚠ 주의사항

### 1. 릴리즈 빌드 전 필수 확인
```
┌─────────────────────────────────────────┐
│ ⚠ 릴리즈 빌드 전 체크리스트             │
├─────────────────────────────────────────┤
│ [ ] Test Mode = OFF로 변경              │
│ [ ] 실제 단말에서 광고 로드 테스트 완료 │
│ [ ] 로그에서 "TEST MODE = OFF" 확인    │
└─────────────────────────────────────────┘
```

### 2. no fill이 정상인 이유
- LevelPlay는 Mediation 플랫폼입니다
- 여러 광고 네트워크를 순회하며 광고를 요청합니다
- 특정 시점에 광고 재고가 없을 수 있습니다 (= no fill)
- **다른 네트워크에서 광고를 가져오면 정상 작동**

### 3. 로그 수집 방법
- Android: `adb logcat -s Unity` 명령어 사용
- 또는 Android Studio Logcat 사용
- 필터: `Unity` 태그로 필터링

---

## 📝 로그 샘플 (정상 동작 예시)

### 초기화 성공
```
[LevelPlayBoot] === SDK 초기화 시작 ===
[LevelPlayBoot] ⚠⚠⚠ TEST MODE = ON ⚠⚠⚠
[LevelPlayBoot] SetAdaptersDebug(true) 설정 완료
[LevelPlayBoot] ValidateIntegration() 호출 완료
[LevelPlayBoot] 초기화 이벤트 등록 완료
[LevelPlayBoot] ★★★ initialization success ★★★
[LevelPlayBoot] User ID: xxxxxxxxxx
adapter loaded (UnityAds)
adapter loaded (AdMob)
```

### 광고 로드 성공
```
[AdManager] Preload 시작: h2fx7fmmr5w53rkr (Reward)
[WatchAdButton] ★★★ ad loaded ★★★ - h2fx7fmmr5w53rkr
[WatchAdButton] Ad Network: UnityAds
```

### 광고 로드 실패 (no fill)
```
[WatchAdButton] LoadFailed - h2fx7fmmr5w53rkr: LevelPlayAdError: 509
[WatchAdButton] ● no fill ● - h2fx7fmmr5w53rkr (광고 재고 부족 - 정상 상황)
[WatchAdButton] 3초 후 재시도 (1/3)
```

---

## 🎯 테스트 결과 정리 양식

### Test Mode ON 빌드

```
날짜: ____________________
빌드 버전: ________________

[ ] initialization success 확인
    - User ID: ________________
    - Test Suite Enabled: ______

[ ] adapter loaded 확인
    - ironSource: O / X
    - UnityAds: O / X
    - AdMob: O / X

[ ] 광고 로드 테스트
    - Rewarded Ad: 성공 / 실패 (에러코드: ____)
    - Interstitial Ad: 성공 / 실패 (에러코드: ____)
    - 로드 성공한 네트워크: ______________

비고:
_________________________________________
_________________________________________
```

### Test Mode OFF 빌드

```
날짜: ____________________
빌드 버전: ________________

[ ] initialization success 확인
    - User ID: ________________
    - Test Suite Enabled: False

[ ] adapter loaded 확인
    - ironSource: O / X
    - UnityAds: O / X
    - AdMob: O / X

[ ] 광고 로드 테스트
    - Rewarded Ad: 성공 / 실패 (에러코드: ____)
    - Interstitial Ad: 성공 / 실패 (에러코드: ____)
    - 로드 성공한 네트워크: ______________

비고:
_________________________________________
_________________________________________
```

---

## 📞 문제 발생 시

### Case 1: SDK 초기화 실패
- 로그에서 `initialization failed` 확인
- 에러 코드와 메시지 복사
- LevelPlay 콘솔에서 App Key 재확인

### Case 2: 어댑터가 하나도 로딩 안 됨
- 각 네트워크의 SDK 설정 확인
- Dependencies 파일 확인 (EDM4U)
- 콘솔에서 네트워크 활성화 상태 확인

### Case 3: 광고가 계속 실패함 (no fill 외)
- 에러 코드 확인 (509 외의 코드)
- 로그 전체 복사하여 전달
- Ad Unit ID 확인

---

## 📌 체크리스트 요약

- [ ] Test Mode ON 빌드 완료
- [ ] Test Mode ON 로그 수집 완료
- [ ] Test Mode OFF 빌드 완료  
- [ ] Test Mode OFF 로그 수집 완료
- [ ] `initialization success` 로그 확인
- [ ] `adapter loaded` 로그 확인 (어떤 네트워크든)
- [ ] `ad loaded` 또는 `no fill` 로그 확인
- [ ] 테스트 결과 정리 완료

---

**작성일:** 2026-02-04  
**버전:** 1.0
