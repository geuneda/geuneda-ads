using System;

// ReSharper disable once CheckNamespace

namespace Geuneda.Ads
{
	/// <summary><see cref="AdService"/> 설정입니다. 기본값은 레퍼런스(로드 재시도)와 SpellDefense 운영 값(시간 초과)을 따릅니다.</summary>
	public sealed class AdServiceOptions
	{
		public static readonly TimeSpan MinRetryDelay = TimeSpan.FromMilliseconds(100);

		public static readonly TimeSpan MinLoadTimeout = TimeSpan.FromSeconds(1);

		/// <summary>로드 실패 뒤 첫 대기. 실패마다 두 배씩 <see cref="MaxRetryDelay"/> 까지 늘고, 성공하면 처음으로 돌아갑니다</summary>
		public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

		public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

		/// <summary>SDK 가 로드 결과를 알리지 않을 때 실패로 보고 다시 시도하기까지의 시간</summary>
		public TimeSpan LoadTimeout { get; set; } = TimeSpan.FromSeconds(60);

		/// <summary>
		/// 앱이 앞에 있는 동안 종료 콜백을 기다리는 시간입니다. 광고가 앱을 멈추는 동안(Android 광고 액티비티, MAX iOS 의 UnityPause) 콜백이 쌓였다가 복귀 뒤 처리되고,
		/// Android 는 닫힌 광고 화면의 정리를 OS 가 최대 10초 늦출 수 있어 그 두 배로 둡니다. 표시 콜백이 이만큼 오지 않으면 모든 플랫폼에서 시간 초과입니다
		/// </summary>
		public TimeSpan ForegroundGrace { get; set; } = TimeSpan.FromSeconds(20);

		/// <summary>
		/// 표시 요청부터의 절대 상한입니다. 영상, 엔드카드, 스토어 이동을 넉넉히 넘겨야 합니다(벙커디펜스는 30초에서 정상 시청을 끊었고 300초로 정착).
		/// 광고 중에도 앱이 도는 미디에이션(<see cref="IAdMediation.PausesApp"/> false)에서는 표시된 광고를 이 상한으로만 끝냅니다. 상한 뒤 온 보상은 <see cref="IAdService.LateRewarded"/> 로 갑니다
		/// </summary>
		public TimeSpan AbsoluteTimeout { get; set; } = TimeSpan.FromMinutes(5);

		/// <summary>보상형 광고가 보상 콜백 전에 닫혔을 때 보상 콜백을 기다리는 시간. SDK 는 두 콜백의 순서를 보장하지 않습니다</summary>
		public TimeSpan RewardGrace { get; set; } = TimeSpan.FromSeconds(2);

		/// <summary>
		/// 광고가 표시 중에 앱을 멈추는지입니다. true 면 앱이 앞으로 돌아온 지 <see cref="ForegroundGrace"/> 가 지나도록 닫힘 콜백이 없을 때 시간 초과로 끝냅니다.
		/// null 이면 미디에이션의 값(<see cref="IAdMediation.PausesApp"/>, MAX 는 Android·iOS 기기에서 true)입니다
		/// </summary>
		public bool? AdPausesApp { get; set; }
	}
}
