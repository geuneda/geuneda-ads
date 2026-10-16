using System;

// ReSharper disable once CheckNamespace

namespace Geuneda.Ads
{
	/// <summary>
	/// 미디에이션 SDK 를 감싸는 어댑터 계약입니다. AppLovin MAX 어댑터는 <c>com.geuneda.ads.max</c> 패키지에 있습니다.
	/// 서비스(<see cref="AdService"/>)가 로드 재시도, 표시 한 번에 결과 한 번, 시간 초과, 늦은 콜백 거르기를 맡으므로 어댑터는 SDK 호출과 형식 변환만 합니다.
	/// </summary>
	/// <remarks>
	/// <para>SDK 초기화는 게임이 합니다(동의·개인정보 설정 뒤). 어댑터는 초기화가 끝났는지만 알립니다.</para>
	/// <para>이벤트는 어느 스레드에서 올려도 됩니다. 메인 스레드가 아니면 서비스가 메인 스레드로 넘깁니다. 메서드는 서비스가 메인 스레드에서 부릅니다.</para>
	/// <para>
	/// 광고 정보(<see cref="AdDetails"/>)의 <see cref="AdDetails.Identity"/> 는 불러온 광고 하나를 가리키는 값이어야 합니다. 로드 성공(만료된 광고를 SDK 가 다시 불러온 경우 포함)과
	/// 그 광고의 표시·표시 실패·보상·닫힘 콜백에서 같은 값을 주세요(SDK 가 콜백마다 다른 값을 주면 어댑터가 로드 때의 값으로 되살림).
	/// 보장할 수 없으면 null 을 줍니다(그러면 늦은 콜백은 지금 표시 중인 광고의 것으로 봅니다).
	/// </para>
	/// </remarks>
	public interface IAdMediation
	{
		/// <summary>미디에이션 이름(분석의 광고 플랫폼, 예: AppLovin)</summary>
		string Platform { get; }

		/// <summary>SDK 초기화가 끝났는지</summary>
		bool IsInitialized { get; }

		/// <summary>
		/// 전체 화면 광고를 표시하는 동안 앱(Unity 플레이어 루프)이 멈추는지. true 면 서비스는 앱이 앞으로 돌아온 지
		/// <see cref="AdServiceOptions.ForegroundGrace"/> 가 지나도록 닫힘 콜백이 없을 때 광고가 닫혔다고 보고 끝냅니다.
		/// 표시 중에도 앱이 돌면 false 를 줍니다(그러면 표시된 광고는 절대 상한으로만 끝남). <see cref="AdServiceOptions.AdPausesApp"/> 로 덮어쓸 수 있습니다
		/// </summary>
		bool PausesApp { get; }

		/// <summary>SDK 초기화가 끝났습니다. 이미 끝난 뒤 만든 어댑터는 올리지 않아도 됩니다(서비스가 <see cref="IsInitialized"/> 를 먼저 봄)</summary>
		event Action Initialized;

		/// <summary>광고를 불러왔습니다. SDK 가 만료된 광고를 스스로 다시 불러온 경우에도 새 광고 정보로 올립니다</summary>
		event Action<AdFormat, AdDetails> Loaded;

		event Action<AdFormat, AdError> LoadFailed;

		event Action<AdFormat, AdDetails> Displayed;

		event Action<AdFormat, AdError, AdDetails> DisplayFailed;

		/// <summary>보상형 광고의 보상 조건을 채웠습니다. 닫힘(<see cref="Hidden"/>)보다 늦게 올 수 있습니다</summary>
		event Action<AdFormat, AdDetails> Rewarded;

		event Action<AdFormat, AdDetails> Clicked;

		/// <summary>광고가 닫혔습니다</summary>
		event Action<AdFormat, AdDetails> Hidden;

		/// <summary>노출 수익이 정해졌습니다</summary>
		event Action<AdFormat, AdDetails, AdPaid> RevenuePaid;

		/// <summary>이 플랫폼에 그 형식의 광고 단위가 설정됐는지</summary>
		bool IsAvailable(AdFormat format);

		/// <summary>불러온 광고가 있어 바로 표시할 수 있는지</summary>
		bool IsReady(AdFormat format);

		/// <summary>광고를 불러옵니다. 결과는 <see cref="Loaded"/> 나 <see cref="LoadFailed"/> 로 옵니다</summary>
		void Load(AdFormat format);

		/// <summary>
		/// 불러온 광고를 표시합니다. 결과는 <see cref="Displayed"/> → (<see cref="Rewarded"/>) → <see cref="Hidden"/>, 또는 <see cref="DisplayFailed"/> 로 옵니다.
		/// 이 호출 안에서 바로 올려도 됩니다
		/// </summary>
		/// <param name="placement">게임 안 광고 위치. SDK 대시보드의 위치별 통계에 씁니다. 없으면 null</param>
		void Show(AdFormat format, string placement);
	}
}
