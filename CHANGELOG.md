# Changelog
이 패키지의 모든 주요 변경사항은 이 파일에 기록됩니다.

이 형식은 [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)를 기반으로 하며,
이 프로젝트는 [Semantic Versioning](http://semver.org/spec/v2.0.0.html)을 따릅니다.

## [0.1.0] - 2026-10-17

**New**:
- `IAdService`/`AdService`: 보상형(`ShowRewardedAsync`)·전면(`ShowInterstitialAsync`) 광고. 표시 요청마다 결과 하나(`Result`: 성공은 보상 받음·닫힘, 실패는 결과 코드). 전체 화면 광고는 한 번에 하나(`ResultCodes.InProgress`)
- 결과 판정: 보상 콜백 뒤 닫힘이면 성공, 보상 전에 닫히면 `RewardGrace`(2초) 동안 보상 콜백을 기다림(앱이 앞에 있는 시간만 셈), 표시 실패는 `DisplayFailed`
- 시간 초과 판정(닫힘 콜백이 없을 때, 앱이 앞에 온 지 `ForegroundGrace` 20초가 지난 뒤에만): 표시 콜백 없음(`DisplayTimeout`, 모든 플랫폼), 광고가 앱을 멈추는 미디에이션(`IAdMediation.PausesApp`, `AdPausesApp` 로 덮어씀)에서 복귀 뒤 닫힘 없음(`ForegroundTimeout`), 표시 요청부터 5분(`AbsoluteTimeout`). 그 전에 보상을 받았으면 성공. 1초 넘게 프레임이 돌지 않았으면(SDK 의 UnityPause) 복귀 뒤 닫힘 대기를 그때부터 세고, 그 뒤 1초 안에 생긴 시간 기준 확정은 조건이 1초·2프레임 더 이어질 때만 함(멈췄던 동안의 콜백이 늦게 큐에 들어와도 먼저 끝내지 않음, 확인 시작은 이후 간격으로 다시 잡지 않음)
- 늦은 보상: 실패로 정한 보상형 표시에서 온 보상을 `LateRewarded`(`AdLateReward`)로 한 번 넘김
- 앞 광고의 늦은 콜백 거르기: 결과를 정한 최근 표시 4개를 들고 광고 식별 값(`AdDetails.Identity`)으로 가려냄. 식별 값을 모르거나 맞지 않으면 지금 표시의 것으로 봄. 표시 콜백을 받은 지금 표시에 온 표시 실패는 무시
- 알림 순서: 처리기 안에서 생긴 알림(정리, 다음 광고)은 지금 알림이 모든 구독자에게 간 뒤 순서대로 나감. 결과 확정의 닫힘·결과 보고·호출자 완료는 함께 나감
- 로드: 초기화 뒤 단위가 있는 형식을 모두 불러오고 닫힘·표시 실패·시간 초과 뒤 다시 불러옴. 실패하면 2초부터 두 배씩 30초까지 대기(대기 중 표시 요청은 대기를 건너뛰지 않음), SDK 무응답은 `LoadTimeout`(60초) 뒤 실패로 봄
- 이벤트: `ShowingChanged`(실시간 연결 핑 멈춤용), `Displayed`, `Clicked`(표시마다 한 번), `ShowSettled`(`AdShowReport`, 거절 포함 요청마다 한 번), `LateRewarded`, `RevenuePaid`(`AdRevenue`, 모르는 금액은 null), `LoadFailed`(`AdLoadFailure`)
- 결과 코드 `AdsResultCodes`(8000 블록): `NotInitialized` 8001, `NotReady` 8002, `Unavailable` 8003, `DisplayFailed` 8004, `NotRewarded` 8005
- 미디에이션 어댑터 계약 `IAdMediation`(SDK 초기화는 게임, 어댑터는 SDK 호출과 형식 변환, 표시 중 앱을 멈추는지 `PausesApp`, 이벤트는 어느 스레드에서 올려도 서비스가 메인 스레드로 넘김)
- 선택 어셈블리 `Geuneda.Ads.Analytics`(com.geuneda.analytics 0.2.0 이상): `AdRevenueLogger` 가 노출 수익을 광고 노출 표준 이벤트(`LogAdImpression`)로 기록(모르는 금액·잘못된 통화는 건너뜀)
