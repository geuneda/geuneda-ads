# Geuneda Ads

보상형·전면 광고 서비스입니다. 표시 요청마다 결과를 한 번만 정하고, 미디에이션 SDK 가 닫힘 콜백을 주지 않아도 시간 초과로 끝내 다음 광고가 막히지 않게 합니다.

- **결과 한 번**: 표시 요청은 성공(보상형은 보상 받음, 전면은 닫힘) 또는 실패 코드 하나로 끝납니다. 어떤 경로(닫힘, 표시 실패, 시간 초과, 정리)로 끝나도 호출자는 반드시 결과를 받습니다.
- **시간 초과 판정**: 앱이 앞에 있는 시간으로 판정합니다. 표시 콜백이 20초 안 오면, 광고가 앱을 멈추는 미디에이션(MAX 는 Android·iOS)에서 앱이 돌아온 뒤 20초 동안 닫힘 콜백이 없으면, 표시 요청부터 5분이 지나면 끝냅니다.
- **늦은 보상**: 결과를 실패로 정한 뒤(시간 초과, 보상 없이 닫힘의 대기 뒤) 온 보상은 `LateRewarded` 로 넘겨 게임이 지급을 정합니다. 끝까지 보고도 닫힘 콜백이 늦거나 빠져 시간 초과가 난 사용자도 보상을 잃지 않습니다.
- **앞 광고의 늦은 콜백 거르기**: 결과를 정한 최근 광고 4개의 늦은 콜백을 광고 식별 값으로 가려내 다음 광고의 결과에 섞이지 않게 합니다. 같은 위치(placement)를 연달아 써도 됩니다.
- **로드 재시도**: 실패하면 2초부터 두 배씩 30초까지 기다렸다 다시 불러옵니다. 기다리는 동안의 표시 요청은 대기를 건너뛰지 않습니다. SDK 가 로드에 답하지 않으면 60초 뒤 실패로 봅니다.
- **수익**: 노출 수익을 이벤트 하나(`RevenuePaid`)로 모읍니다. analytics 가 있으면 `AdRevenueLogger` 가 광고 노출 표준 이벤트(`ad_impression`)로 기록합니다.

미디에이션 SDK 는 어댑터 패키지로 붙입니다. AppLovin MAX 는 `com.geuneda.ads.max` 입니다. SDK 초기화(동의·개인정보 설정 포함)는 게임이 합니다.

## 설치

`Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.geuneda.core": "https://github.com/geuneda/geuneda-core.git",
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
    "com.geuneda.ads": "https://github.com/geuneda/geuneda-ads.git",
    "com.geuneda.ads.max": "https://github.com/geuneda/geuneda-ads-max.git"
  }
}
```

- 의존성: `com.geuneda.core` 0.4.0 이상, UniTask 2.5.10 이상. 최소 Unity 2021.3.
- 선택: `com.geuneda.analytics` 0.2.0 이상이 있으면 `Geuneda.Ads.Analytics`(노출 수익 기록) 어셈블리가 켜집니다(asmdef `versionDefines`).

## 사용 예

### 부트스트랩

```csharp
using Geuneda.Ads;
using Geuneda.Ads.Analytics;
using Geuneda.Ads.Max;
using Geuneda.Services;

// 1. 게임이 SDK 를 초기화합니다(동의 → 개인정보 → 뮤트 → 초기화)
MaxSdk.SetHasUserConsent(consent);
MaxSdk.SetMuted(muted);
MaxSdk.InitializeSdk();

// 2. 어댑터와 서비스. 초기화가 끝나면 서비스가 광고를 불러옵니다
var mediation = new MaxAdMediation(new MaxAdMediationOptions
{
#if UNITY_IOS
	RewardedAdUnitId = "ios-rewarded-unit",
	InterstitialAdUnitId = "ios-interstitial-unit"
#else
	RewardedAdUnitId = "android-rewarded-unit",
	InterstitialAdUnitId = "android-interstitial-unit"
#endif
});
var ads = new AdService(mediation);
var revenueLogger = new AdRevenueLogger(ads, analytics);   // analytics 가 있을 때
MainInstaller.Bind<IAdService>(ads);

// 종료할 때: revenueLogger.Dispose(); MainInstaller.CleanDispose<IAdService>(); mediation.Dispose();
```

### 보상형 광고

```csharp
var ads = MainInstaller.Resolve<IAdService>();
var result = await ads.ShowRewardedAsync("revive");

if (result.IsSuccess)
{
	GrantRevive();                       // 성공 = 보상 조건을 채움
}
else if (result.Code == AdsResultCodes.NotReady)
{
	ShowToast("광고를 불러오는 중입니다");
}
else if (result.Code == ResultCodes.Timeout)
{
	ShowToast("광고 결과를 확인하지 못했습니다");   // 늦게 보상이 오면 LateRewarded
}
```

### 늦은 보상

```csharp
ads.LateRewarded += reward =>
{
	// 호출자는 이미 실패를 받았습니다. 위치마다 지금 줄 수 있는 보상인지 게임이 정합니다
	if (reward.Placement == "stamina")
	{
		GrantStamina();
	}
};
```

### 광고 동안 실시간 연결의 핑 멈추기

```csharp
// 광고 동안 앱이 멈춰(MAX 는 Android·iOS) 핑 시간 초과로 끊길 수 있습니다(레퍼런스는 광고 동안 핑퐁을 멈춤)
ads.ShowingChanged += showing => realtime.SetPingPaused(showing);
```

### 분석 이벤트

```csharp
ads.ShowSettled += report => analytics.LogEvent("ad_result", new Dictionary<string, object>
{
	{ "format", report.Format.ToString() },
	{ "placement", report.Placement },
	{ "code", report.Result.Code },          // 0 성공, 8005 보상 없음, 3 시간 초과 ...
	{ "reason", report.Reason.ToString() },  // Closed, DisplayFailed, DisplayTimeout, ForegroundTimeout, AbsoluteTimeout ...
	{ "displayed", report.Displayed },
	{ "network", report.Ad.NetworkName }
});
ads.Clicked += click => analytics.LogEvent("ad_click", ...);       // 표시마다 한 번
ads.LoadFailed += failure => analytics.LogEvent("ad_load_failed", ...);
```

## 결과 판정

| 콜백 순서 | 보상형 | 전면 |
|---|---|---|
| 표시 → 보상 → 닫힘 | 성공 | 성공(닫힘에서) |
| 표시 → 닫힘 → (2초 안) 보상 | 성공. SDK 는 두 콜백의 순서를 보장하지 않음 | - |
| 표시 → 닫힘 → 2초 동안 보상 없음 | `NotRewarded` | - |
| 표시 실패 | `DisplayFailed` (보상을 먼저 받았으면 성공) | `DisplayFailed` |
| 닫힘 콜백 없음 | 시간 초과 판정. 그 전에 보상을 받았으면 성공, 아니면 `Timeout` | `Timeout` |
| 표시 중 `Dispose` | 보상을 받았으면 성공, 아니면 `Cancelled` | `Cancelled` |

- 보상 대기(2초)는 앱이 앞에 있는 시간만 셉니다. 닫힌 직후 앱이 뒤로 가면 돌아온 뒤 다시 셉니다.
- 결과를 실패로 정한 뒤 온 보상은 `LateRewarded` 로 한 번만 갑니다.

## 시간 초과 판정

닫힘(또는 표시 실패) 콜백이 오지 않으면 매 프레임 판정합니다. 앱이 앞에 온 지 `ForegroundGrace`(20초)가 지나기 전에는 끝내지 않습니다. 광고가 앱을 멈추는 동안 SDK 콜백이 큐에 쌓였다가 복귀 뒤 처리되고, Android 는 닫힌 광고 화면의 정리를 OS 가 최대 10초 늦출 수 있기 때문입니다.

MAX 는 Android(광고 액티비티)와 iOS(플러그인이 표시 때 `UnityPause(YES)`, 닫힘 때 `UnityPause(NO)`, 8.6.3 네이티브 소스로 확인) 모두 광고 동안 플레이어 루프를 멈춥니다. 일시정지·포커스 메시지가 오지 않아도 1초 넘게 프레임이 돌지 않았으면 멈췄다 돌아왔을 수 있다고 보고, 복귀 뒤 닫힘 대기(`ForegroundTimeout`)를 그때부터 셉니다. 그 뒤 1초 안에 시간 초과·보상 없음 조건이 생기면 바로 끝내지 않고, 조건이 1초와 2프레임 더 이어질 때 끝냅니다(멈췄던 동안 SDK 가 보낸 콜백이 복귀 뒤 늦게 큐에 들어와도 먼저 끝내지 않음). 앱이 앞에 온 시각은 메시지로만 정하고 확인 시작 시각도 이후 간격으로 다시 잡지 않아, 느린 프레임이 이어져도 표시 대기·보상 대기·절대 상한은 끝납니다. 긴 프레임이 20초보다 짧은 간격으로 반복되면 복귀 뒤 닫힘 대기는 끝나지 않을 수 있지만 절대 상한이 끝냅니다.

| 이유(`AdSettleReason`) | 조건 | 플랫폼 |
|---|---|---|
| `AbsoluteTimeout` | 표시 요청부터 `AbsoluteTimeout`(5분) | 모두 |
| `DisplayTimeout` | 앱이 앞에 있는 동안 표시 콜백이 `ForegroundGrace` 동안 오지 않음 | 모두 |
| `ForegroundTimeout` | 앱이 앞으로 돌아온 뒤 `ForegroundGrace` 동안 닫힘 콜백이 오지 않음 | `AdPausesApp`(기본은 미디에이션의 `PausesApp`, MAX 는 Android·iOS 기기) |

광고 중에도 앱이 도는 미디에이션(`PausesApp` false, MAX 에디터 스텁 등)에서는 표시된 광고를 절대 상한으로만 끝냅니다. 상한 뒤 사용자가 광고를 닫으며 보상 콜백이 오면 `LateRewarded` 로 갑니다(MAX 의 Google 어댑터는 닫을 때 보상 바로 뒤에 닫힘을 보냅니다). iOS 실기에서 MAX 의 `UnityPause` 가 Unity 수명 주기 메시지를 보내는지, 보낸다면 어떤 순서인지는 확인하지 않았습니다(프레임 간격 판정은 메시지가 없어도 동작).

## 앞 광고의 늦은 콜백

결과를 정한 표시를 최근 4개까지 들고 있습니다(표시 실패로 바로 끝난 표시가 앞의 시간 초과 표시를 밀어내지 않게). 같은 형식의 콜백이 오면 다음 규칙으로 어느 표시의 것인지 정합니다.

1. 콜백의 광고 식별 값(`AdDetails.Identity`)이 지금 표시와 같으면 지금 표시, 결과를 정한 표시 중 하나와 같으면(최신부터) 그 표시의 것입니다.
2. 식별 값을 모르거나 아무것과도 맞지 않으면 지금 표시의 것입니다(지금 광고의 콜백을 잃지 않는 쪽).
3. 지금 표시가 없으면 가장 최근 표시의 것입니다. 식별 값이 둘 다 있는데 다르면 다른 광고라 버립니다.
4. 이미 표시 콜백을 받은 지금 표시에 온 표시 실패는 그 표시의 실패로 보지 않습니다(표시된 광고는 표시에 실패할 수 없음).

식별 값은 어댑터가 불러온 광고 하나마다 정합니다(로드 성공 때 받은 값을 표시에 붙임). MAX iOS 는 표시 실패 이벤트의 로드 지연을 오류의 지연으로 바꿔 보내므로 MAX 어댑터가 로드 때의 값으로 되살립니다.

## 로드와 재시도

- SDK 초기화가 끝나면(`IAdMediation.Initialized`, 또는 서비스를 만들 때 이미 끝났으면 바로) 단위가 있는 형식을 모두 불러옵니다.
- 닫힘·표시 실패·시간 초과 뒤 다음 광고를 불러옵니다(광고는 한 번 표시하면 다시 쓸 수 없음).
- 실패하면 `RetryDelay`(2초)부터 두 배씩 `MaxRetryDelay`(30초)까지 기다립니다. 성공하면 처음으로 돌아갑니다.
- 기다리는 동안의 표시 요청은 `NotReady` 이고 대기를 건너뛰지 않습니다. 대기도 로드도 없는데 준비되지 않았으면(SDK 가 만료된 광고를 버림 등) 그때 불러옵니다.
- SDK 가 `LoadTimeout`(60초) 동안 답하지 않으면 실패로 보고 재시도합니다. 그 뒤 늦게 온 성공은 받아들입니다. 재시도를 시작하기 전에 온 늦은 실패는 세지 않지만, 재시도를 시작한 뒤에 온 앞 요청의 실패는 SDK 가 요청을 구분해 주지 않아(MAX 는 광고 단위만 줌) 지금 요청의 실패로 셉니다.

## 결과 코드

| 코드 | 이름 | 뜻 |
|---|---|---|
| 0 | 성공 | 보상형은 보상 받음, 전면은 닫힘 |
| 2 | `ResultCodes.Cancelled` | 표시 중 서비스 정리 |
| 3 | `ResultCodes.Timeout` | 닫힘 콜백 없이 시간 초과(늦은 보상은 `LateRewarded`) |
| 5 | `ResultCodes.InProgress` | 다른 전체 화면 광고가 표시 중 |
| 8001 | `AdsResultCodes.NotInitialized` | SDK 초기화 전 |
| 8002 | `AdsResultCodes.NotReady` | 불러온 광고 없음(불러오는 중, 재시도 대기) |
| 8003 | `AdsResultCodes.Unavailable` | 이 플랫폼에 그 형식의 광고 단위가 없음 |
| 8004 | `AdsResultCodes.DisplayFailed` | SDK 가 표시 실패를 알림 |
| 8005 | `AdsResultCodes.NotRewarded` | 보상 조건을 채우기 전에 닫음 |

정상 실패라 Error 로그를 남기지 않습니다(시간 초과는 Warning). 다른 스레드에서 부르거나 `Dispose` 뒤에 부르면 예외입니다.

## 이벤트

| 이벤트 | 언제 |
|---|---|
| `ShowingChanged(bool)` | 전체 화면 광고가 열림(true) / 결과가 정해짐(false) |
| `Displayed(AdShowEvent)` | 표시 콜백. 결과를 정한 표시의 늦은 콜백도 옴(`IsSettled`) |
| `Clicked(AdShowEvent)` | 클릭. 표시마다 한 번(네트워크가 같은 클릭을 수십 번 알리기도 함) |
| `ShowSettled(AdShowReport)` | 요청마다 한 번. 거절(`Rejected`, `ShowId` 0) 포함. 호출자가 결과를 받기 직전 |
| `LateRewarded(AdLateReward)` | 실패로 정한 보상형 표시의 늦은 보상. 표시마다 많아야 한 번 |
| `RevenuePaid(AdRevenue)` | 노출 수익. 금액을 모르면 `Amount` null |
| `LoadFailed(AdLoadFailure)` | 로드 실패(시간 초과 포함)와 다음 시도까지의 대기 |

이벤트 처리기의 예외는 Error 로그로 남기고 다음 처리기와 결과 확정을 계속합니다. `ShowSettled` 처리기나 호출자의 이어지는 코드에서 바로 다음 광고를 요청하거나 서비스를 정리해도 됩니다. 상태를 먼저 맞추고, 처리기 안에서 생긴 알림은 지금 알림이 모든 구독자에게 간 뒤 순서대로 나갑니다(구독자마다 `ShowingChanged` 를 같은 순서로 받음). 결과 확정의 닫힘·결과 보고·호출자 완료는 함께 나가, 처리기에서 연 다음 광고의 알림은 그 뒤에 옵니다.

## 설정 (`AdServiceOptions`)

| 속성 | 기본값 | 뜻 |
|---|---|---|
| `RetryDelay` | 2초 | 로드 실패 뒤 첫 대기(최소 100ms) |
| `MaxRetryDelay` | 30초 | 대기 상한(`RetryDelay` 이상, 하루 이하) |
| `LoadTimeout` | 60초 | SDK 가 로드에 답하지 않을 때 실패로 보기까지(최소 1초) |
| `ForegroundGrace` | 20초 | 앱이 앞에 있는 동안 표시·닫힘 콜백을 기다리는 시간 |
| `AbsoluteTimeout` | 5분 | 표시 요청부터의 상한(`ForegroundGrace` 이상) |
| `RewardGrace` | 2초 | 보상 전에 닫혔을 때 보상 콜백을 기다리는 시간(0 이면 닫힘에서 바로 실패) |
| `AdPausesApp` | null(미디에이션의 `PausesApp`) | 광고가 표시 중 앱을 멈추는지 |

시간 값은 SpellDefense 운영 값입니다(벙커디펜스는 절대 상한 30초에서 정상 시청을 끊었고 300초로 정착, 복귀 대기는 2초와 15초에서도 오판이 의심돼 20초).

## 미디에이션 어댑터 만들기

`IAdMediation` 을 구현합니다. 서비스가 순서·중복·재시도·시간 초과를 맡으므로 어댑터는 SDK 호출과 형식 변환만 합니다.

- SDK 초기화는 게임이 합니다. `IsInitialized` 와 `Initialized` 로 끝났는지만 알립니다.
- `PausesApp` 은 전체 화면 광고를 표시하는 동안 플레이어 루프가 멈추는지입니다. 멈추면 true(복귀 뒤 닫힘이 없을 때 시간 초과), 표시 중에도 앱이 돌면 false(표시된 광고는 절대 상한으로만 끝남)를 줍니다.
- 설정한 광고 단위의 콜백만 올립니다. 이벤트는 어느 스레드에서 올려도 됩니다(메인 스레드가 아니면 서비스가 `MainThreadDispatcher` 로 넘김).
- 만료된 광고를 SDK 가 스스로 다시 불러오면 새 광고 정보로 `Loaded` 를 올립니다(식별 값이 바뀜).
- `AdDetails.Identity` 는 불러온 광고 하나를 가리키는 값입니다. 로드와 그 광고의 표시·닫힘 콜백에서 같아야 합니다. 보장할 수 없으면 null 을 줍니다.
- 수익을 모르면 `AdPaid.Amount` 를 null 로 줍니다. 음수·NaN 은 서비스가 모름으로 바꾸고 Warning 을 남깁니다.

## 한계

- 실제 기기·실제 광고 네트워크에서 확인하지 않았습니다. 가짜 미디에이션과 MAX 에디터 스텁으로만 확인했습니다.
- 전체 화면 광고는 한 번에 하나입니다. 배너·MREC·앱 오프닝은 다루지 않습니다.
- 전면 광고 빈도 제한(쿨다운), 광고 제거 상품 확인은 게임이 합니다.
- 동의(UMP, ATT)는 게임이 SDK 초기화 전에 처리합니다(로드맵 4-4).
- 시간 초과 뒤 앞 광고가 아직 화면에 있는 동안(앱이 도는 미디에이션의 절대 상한) 다음 광고를 불러오면 SDK 가 무시할 수 있습니다. 그 로드는 `LoadTimeout` 뒤 다시 시도됩니다.
- 광고가 앱을 멈추는 미디에이션(`PausesApp` true)에서 앱을 멈추지 않는 광고가 나오면 표시 20초 뒤 `ForegroundTimeout` 으로 끝납니다(SpellDefense 의 Android 판정과 같음, 그 뒤 보상은 `LateRewarded`). 그런 광고가 있으면 `AdPausesApp = false` 로 둡니다.
- 재시도를 시작한 뒤 온 앞 로드 요청의 실패는 지금 요청의 실패로 셉니다(SDK 가 요청을 구분해 주지 않음). 재시도 대기가 한 번 더 늘 뿐 로드가 멈추지는 않습니다.

## 테스트

- `Tests/EditMode`(`Geuneda.Ads.Tests.EditMode`): 가짜 미디에이션·시계로 로드·재시도, 결과 판정, 시간 초과, 늦은 콜백, 수명 주기.
- `Tests/EditMode/Analytics`(`Geuneda.Ads.Analytics.Tests.EditMode`, analytics 가 있을 때): `AdRevenueLogger`.
- `Tests/PlayMode`(`Geuneda.Ads.Tests.PlayMode`): 수명 주기 컴포넌트의 매 프레임 판정, 다른 스레드 콜백의 메인 스레드 처리.
