using System;
using Cysharp.Threading.Tasks;
using Geuneda.Core;

// ReSharper disable once CheckNamespace

namespace Geuneda.Ads
{
	/// <summary>
	/// 보상형·전면 광고 서비스입니다. 표시 요청마다 결과를 한 번만 정하고, SDK 가 닫힘 콜백을 주지 않아도 시간 초과로 끝내 다음 광고가 막히지 않게 합니다.
	/// </summary>
	/// <remarks>
	/// <para>메인 스레드에서만 부릅니다. 정상 실패는 결과 코드(<see cref="AdsResultCodes"/>, core 의 <see cref="ResultCodes"/>)로, 오용(종료 뒤 호출, 다른 스레드)은 예외로 알립니다.</para>
	/// <para>
	/// 전체 화면 광고는 한 번에 하나입니다. 표시 중에 다른 표시를 요청하면 <see cref="ResultCodes.InProgress"/> 입니다.
	/// <see cref="IDisposable.Dispose"/> 하면 표시 중인 요청은 <see cref="ResultCodes.Cancelled"/> 로 끝납니다(미디에이션 어댑터는 따로 정리).
	/// </para>
	/// </remarks>
	public interface IAdService : IDisposable
	{
		/// <summary>미디에이션 SDK 초기화가 끝났는지</summary>
		bool IsInitialized { get; }

		/// <summary>전체 화면 광고를 표시 중인지(결과가 정해지면 false)</summary>
		bool IsShowing { get; }

		/// <summary>
		/// 전체 화면 광고가 열리거나(true) 결과가 정해졌습니다(false). 광고 동안 앱이 멈춰(MAX 는 Android·iOS) 실시간 연결의 핑 시간 초과로 끊기지 않게
		/// 표시 중에만 핑 검사를 멈추는 데 씁니다(레퍼런스는 광고 동안 핑퐁을 멈춤)
		/// </summary>
		event Action<bool> ShowingChanged;

		/// <summary>광고가 표시됐습니다. 시간 초과로 끝낸 표시의 늦은 콜백도 옵니다(<see cref="AdShowEvent.IsSettled"/>)</summary>
		event Action<AdShowEvent> Displayed;

		/// <summary>광고를 클릭했습니다. 네트워크가 같은 클릭을 여러 번 알려도 표시마다 한 번만 옵니다</summary>
		event Action<AdShowEvent> Clicked;

		/// <summary>표시 요청의 결과가 정해졌습니다. 표시 전에 거절한 요청을 포함해 요청마다 한 번이고, 호출자가 결과를 받기 직전에 옵니다</summary>
		event Action<AdShowReport> ShowSettled;

		/// <summary>
		/// 실패로 정한 보상형 표시(시간 초과, 보상 없이 닫음의 대기 뒤)에서 보상 콜백이 늦게 왔습니다. 호출자는 이미 실패를 받았으므로 지급할지 게임이 정합니다.
		/// 닫힘 콜백이 늦거나 빠져 끝까지 보고도 시간 초과가 난 경우를 여기서 지급할 수 있습니다
		/// </summary>
		event Action<AdLateReward> LateRewarded;

		/// <summary>노출 수익입니다. 매출 기록은 여기서만 합니다(analytics 가 있으면 <c>Geuneda.Ads.Analytics.AdRevenueLogger</c> 가 구독)</summary>
		event Action<AdRevenue> RevenuePaid;

		/// <summary>광고 로드가 실패했고 다시 불러오기를 기다립니다</summary>
		event Action<AdLoadFailure> LoadFailed;

		/// <summary>그 형식의 광고를 바로 표시할 수 있는지</summary>
		bool IsReady(AdFormat format);

		/// <summary>
		/// 보상형 광고를 표시하고 닫힐 때까지 기다립니다. 성공이면 보상 조건을 채운 것이라 지급합니다. 결과는 반드시 옵니다(닫힘 콜백이 없으면 시간 초과).
		/// </summary>
		/// <param name="placement">게임 안 광고 위치. SDK 대시보드와 이벤트에 함께 넘어갑니다. 없으면 null</param>
		/// <returns>
		/// 성공(보상 받음), 또는 실패 코드: <see cref="AdsResultCodes.NotRewarded"/>(보상 없이 닫음), <see cref="AdsResultCodes.NotReady"/>,
		/// <see cref="AdsResultCodes.NotInitialized"/>, <see cref="AdsResultCodes.Unavailable"/>, <see cref="AdsResultCodes.DisplayFailed"/>,
		/// <see cref="ResultCodes.InProgress"/>(다른 광고 표시 중), <see cref="ResultCodes.Timeout"/>(닫힘 콜백 없음, 늦은 보상은 <see cref="LateRewarded"/>),
		/// <see cref="ResultCodes.Cancelled"/>(표시 중 서비스 정리)
		/// </returns>
		UniTask<Result> ShowRewardedAsync(string placement = null);

		/// <summary>전면 광고를 표시하고 닫힐 때까지 기다립니다. 성공이면 광고가 닫힌 것입니다. 실패 코드는 보상형과 같습니다(<see cref="AdsResultCodes.NotRewarded"/> 제외)</summary>
		/// <param name="placement">게임 안 광고 위치. 없으면 null</param>
		UniTask<Result> ShowInterstitialAsync(string placement = null);
	}
}
