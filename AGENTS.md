# Geuneda.Ads - AI 에이전트 가이드

> **관련 파일**: `CLAUDE.md`는 이 파일을 감쌉니다 — `CLAUDE.md`가 아니라 `AGENTS.md`를 편집하세요. `README.md`는 사용자 대상 문서입니다.

## 1. 패키지 개요
- **패키지**: `com.geuneda.ads`
- **Unity**: 2021.3+
- **의존성**: `com.geuneda.core` 0.4.0(결과 코드, 로거, 단조 시계, 메인 스레드 디스패처), `com.cysharp.unitask` 2.5.10. analytics 는 선택(asmdef `versionDefines` 로 `Geuneda.Ads.Analytics` 를 켬)
- **역할**: 보상형·전면 광고의 표시 결과를 한 번만 정하고, SDK 콜백이 빠져도 시간 초과로 끝내며, 로드를 백오프로 다시 시도합니다. SDK 호출은 어댑터(`IAdMediation`, MAX 는 `com.geuneda.ads.max`)가 합니다
- **레퍼런스**: Percent `PercentAds`·`PercentAdsMax`(재로드 2초→30초 백오프, show 단위 구독, Hidden 에서 결과 확정)와 SpellDefense `AppLovinAdService`·`RewardedAdSession`·`RewardedAdWatchdog`(세션당 한 번 확정, 닫힘 뒤 2초 보상 대기, Android 복귀 뒤 20초, 절대 300초). 분석은 lucky-defense 레포 `data/analysis/tech/geuneda-roadmap.md` 의 "4-3 시작 전 메모"

## 2. 구조

| 파일 | 역할 |
|------|------|
| `Runtime/IAdService.cs`, `Runtime/AdService.cs` | 서비스 계약과 구현(표시, 결과 확정 `Settle`, 시간 판정 `TickSession`, 수명 주기) |
| `Runtime/AdService.Callbacks.cs` | 미디에이션 이벤트 처리, 메인 스레드 넘기기, 콜백이 속한 표시 찾기(`Route`) |
| `Runtime/AdService.Loading.cs` | 형식별 로드 상태, 백오프, 로드 제한 시간 |
| `Runtime/IAdMediation.cs` | 어댑터 계약 |
| `Runtime/Internal/AdSession.cs`, `AdLoadLane.cs` | 표시 하나의 상태, 형식 하나의 로드 상태 |
| `Runtime/Internal/AdLifecycle.cs` | 숨은 컴포넌트(`Update` → `Tick`, 일시정지·포커스) |
| `Runtime/Analytics/AdRevenueLogger.cs` | 선택 어셈블리. 노출 수익 → `LogAdImpression` |

## 3. 규칙

- **결과는 한 번**: 모든 종료 경로는 `Settle` 하나를 지납니다(`AdSession.TrySettle`). 상태(`_current`, `_settled`)를 먼저 맞추고 `ShowingChanged(false)` → `ShowSettled` → 호출자 완료를 함께 큐에 넣어 내보냅니다. 다음 광고 로드는 `Settle` 전에 요청합니다(이어지는 코드가 로드를 한 번 더 걸지 않게).
- **알림 큐(`Notify`/`Enqueue`/`Drain`)**: 모든 이벤트와 호출자 완료는 큐를 지납니다. 처리기 안에서 정리하거나 다음 광고를 열면 그 알림은 지금 알림이 모든 구독자에게 간 뒤에 나갑니다. 큐 없이 바로 올리면 뒤쪽 구독자가 `ShowingChanged` 를 거꾸로(false → true) 받습니다(2026-10-17 리뷰 1회차). 거절(`Reject`)도 완료 소스를 큐로 끝냅니다.
- **시간 판정은 앱이 앞에 있는 시간으로**: `TickSession` 은 앱이 앞에 온 지 `ForegroundGrace` 가 지나기 전에는 끝내지 않습니다. 광고 동안 앱이 멈추면 MAX 콜백이 `MaxEventExecutor` 큐에 쌓였다가 복귀 뒤 `Update` 에서 처리되므로, 복귀 첫 프레임에 판정하면 큐의 닫힘보다 먼저 끝냅니다. 보상 대기(`RewardGrace`)도 닫힘 시각과 복귀 시각 중 늦은 것부터 셉니다.
- **프레임 간격**: `Tick` 사이가 `FrameGap`(1초) 이상이면(메시지 없이 멈췄던 루프의 복귀일 수 있음) `_lastGapAt` 을 기록해 앱을 멈추는 미디에이션의 복귀 뒤 닫힘 대기를 그때부터 셉니다. 그 뒤 `ResumeSettle`(1초) 안에 시간 기준 확정(시간 초과, 보상 없음) 조건이 생기면 `Confirmed` 가 조건이 처음 보인 시각(`AdSession.TimedOutSince`)부터 1초와 `ResumeSettleFrames`(2) 프레임이 더 지날 때만 끝냅니다. 조건이 풀리면(콜백 도착) 기록을 지웁니다. 앱이 앞에 온 시각(`_foregroundSince`)은 일시정지·포커스 메시지로만 바꾸고, 확인 시작 시각은 이후 간격으로 다시 잡지 않습니다. 긴 프레임마다 앞에 온 시각을 다시 잡으면 느린 프레임이 시간 초과를 끝없이 미루고(리뷰 2회차), 전역 '직전 Tick 을 건너뛰었는지' 플래그로 첫 프레임을 보호하면 앞선 긴 프레임의 상태 때문에 다음 정지의 보호가 빠집니다(리뷰 3회차). MAX iOS 는 표시 때 `UnityPause(YES)` 를 부른 뒤 표시 이벤트를 다른 큐로 보내고, 닫힘 때 `UnityPause(NO)` 뒤 닫힘 이벤트를 보냅니다(8.6.3 `MAUnityAdManager.m`). 그때 `OnApplicationPause`·`OnApplicationFocus` 가 오는지는 확인하지 않았습니다. 테스트는 프레임(0.5초)마다 `Tick` 하고(`Advance`), 느린 프레임은 `SlowFrames`, 멈춘 루프는 `FreezeFrames` 로 흉내 냅니다.
- **SDK 표시도 큐로**: `Show` 는 `ShowingChanged(true)` 와 SDK 호출(`StartShow`)을 함께 큐에 넣습니다. 처리기 안에서 시작한 표시는 열림 알림이 나가기 전에 SDK 를 부르면 안 됩니다(열림 처리기가 정리하면 SDK 에 광고가 남음, 리뷰 2회차). `StartShow` 는 실행 때 `_disposed`, `_current`, `Settled` 를 다시 봅니다.
- **표시 대기**: 표시 콜백이 오지 않으면 모든 미디에이션에서 `DisplayTimeout` 입니다(SpellDefense 는 Android 만 판정해 iOS 에서 표시가 조용히 실패하면 5분 동안 버튼이 잠김). 광고가 앱을 멈추는지는 어댑터가 정합니다(`IAdMediation.PausesApp`, MAX 는 Android·iOS 기기에서 true). 앱이 도는 미디에이션에서 표시된 광고는 절대 상한으로만 끝내고, 상한 뒤 보상은 `LateRewarded` 로 갑니다. SpellDefense 주석과 로드맵 메모의 'iOS 는 광고 중 Unity 가 멈추지 않음'은 MAX 8.6.3 네이티브 소스와 맞지 않습니다.
- **콜백 귀속(`Route`)**: 식별 값이 있으면 지금 표시, 그다음 결과를 정한 최근 표시(`_settled`, `RetainedSettledShows` 4개, 최신부터)에서 같은 것을 찾습니다. 없거나 모르면 지금 표시, 지금 표시가 없으면 가장 최근 표시입니다(둘 다 식별 값이 있는데 다르면 버림). 앞 표시를 하나만 들면 표시 실패로 바로 끝난 다음 표시가 시간 초과 표시를 밀어내 그 늦은 보상을 잃습니다(리뷰 1회차). 식별 값이 바뀌거나 없어도 지금 광고의 콜백을 잃지 않게 하기 위한 것이므로, 일치하지 않는 콜백을 버리는 쪽으로 바꾸지 않습니다. 대신 표시 콜백을 받은 지금 표시에 온 표시 실패는 무시합니다. 식별 값은 표시 요청 때 불러와 있던 광고의 것(`AdLoadLane.LoadedAd`)이고, 만료 뒤 다시 불러온 광고도 `Loaded` 로 갱신됩니다.
- **로드 상태**: `Idle`(표시로 광고를 썼거나 처음) → `Loading` → `Loaded` / `Waiting`(백오프). 표시 요청이 준비 안 됨이면 `Waiting`·`Loading` 은 그대로 두고(레퍼런스·SpellDefense 는 요청마다 바로 로드해 대기를 건너뜀), `Idle` 이거나 SDK 가 광고를 버린 `Loaded` 면 불러옵니다. 로드 실패는 `Loading` 일 때만 셉니다(제한 시간 뒤 재시도 전의 늦은 실패를 두 번 세지 않음). 재시도를 시작한 뒤 온 앞 요청의 실패는 가려낼 수 없어 지금 요청의 실패로 셉니다(SDK 가 요청 ID 를 주지 않음, README 의 한계).
- **스레드**: 공개 메서드는 만든 스레드에서만 부릅니다(아니면 `InvalidOperationException`). 미디에이션 이벤트가 다른 스레드에서 오면 `MainThreadDispatcher.Post` 로 넘깁니다(MAX 는 `keepInBackground` 이벤트를 다른 스레드에서 부름).
- **정리 확인**: 처리기가 그 자리에서 `Dispose` 할 수 있어 `Tick` 은 단계마다, `StartLoad` 는 SDK 호출 전에 `_disposed` 를 다시 봅니다.
- **바깥 코드 예외**: 게임 처리기의 예외는 `Raise` 가 Error 로그로 남기고 계속합니다. 어댑터의 `Show`·`Load` 예외는 Error 로그 뒤 표시 실패·재시도 대기로 넘깁니다(버튼이 잠기거나 로드가 멈추지 않게). 그 밖의 예외를 결과 코드로 바꾸지 않습니다.
- **enum 기본값**: `AdFormat`, `AdSettleReason` 은 1 부터입니다(0 이 실제 값으로 보이지 않게).

## 4. 테스트

- EditMode(`Geuneda.Ads.Tests.EditMode`): `AdServiceTestBase` 가 가짜 미디에이션(`FakeMediation`)·시계로 서비스를 만들고(`bindLifecycle` false) `Tick`·`HandlePause`·`HandleFocus` 를 직접 부릅니다. 기본은 광고 중에도 앱이 도는 미디에이션, `CreatePausingApp` 은 표시 중 앱을 멈추는 미디에이션(MAX 의 Android·iOS)입니다. `Advance` 는 0.5초 프레임마다 `Tick` 하고, `FreezeFrames` 는 플레이어 루프가 멈춘 것처럼 시간만 보냅니다.
- EditMode Analytics(`Geuneda.Ads.Analytics.Tests.EditMode`): analytics 0.2.0 이상이 있을 때만 컴파일됩니다.
- PlayMode(`Geuneda.Ads.Tests.PlayMode`): 공개 생성자의 숨은 컴포넌트가 `Update` 로 판정하는지, 다른 스레드 콜백이 메인 스레드에서 처리되는지. EditMode 에서는 `DontDestroyOnLoad` 가 예외라 수명 주기 컴포넌트를 만들지 않습니다.
- MAX 와 함께 확인(2026-10-17, 임시 프로젝트): MAX 8.6.3 에디터 스텁으로 전 과정(PlayMode), Android·iOS 플레이어 스크립트 컴파일, analytics·MAX 없는 프로젝트에서 선택 어셈블리 제외.
- 고친 동작은 되돌리면 실패하는 테스트로 확인합니다(식별 값 귀속, 복귀 뒤 보상 대기, 대기 중 표시 요청이 백오프를 건너뛰지 않음).

## 5. 문서

공개 API 를 바꾸면 `README.md`, `CHANGELOG.md`, 호스트 `claude-skills/geuneda-ads/` 스킬(ads.max 와 공유), 호스트 `claude-skills/geuneda-frameworks/` 를 함께 고칩니다. 결과 코드를 더하면 core `README.md` 의 코드 범위 표(8000 블록)도 봅니다.
