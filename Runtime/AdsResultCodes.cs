// ReSharper disable once CheckNamespace

namespace Geuneda.Ads
{
	/// <summary>
	/// ads 패키지의 결과 코드(8000 블록)입니다. 다른 광고가 표시 중이면 <see cref="Geuneda.Core.ResultCodes.InProgress"/>,
	/// 종료 콜백 없이 시간이 지나면 <see cref="Geuneda.Core.ResultCodes.Timeout"/>, 표시 중에 서비스가 정리되면 <see cref="Geuneda.Core.ResultCodes.Cancelled"/> 입니다.
	/// </summary>
	public static class AdsResultCodes
	{
		/// <summary>미디에이션 SDK 초기화가 끝나지 않았습니다(게임이 SDK 를 초기화합니다)</summary>
		public const int NotInitialized = 8001;

		/// <summary>불러온 광고가 없습니다(불러오는 중이거나 실패 뒤 다시 시도를 기다리는 중)</summary>
		public const int NotReady = 8002;

		/// <summary>이 플랫폼에 그 형식의 광고 단위가 설정되지 않았습니다</summary>
		public const int Unavailable = 8003;

		/// <summary>SDK 가 표시 실패를 알렸습니다</summary>
		public const int DisplayFailed = 8004;

		/// <summary>보상형 광고를 보상 조건을 채우기 전에 닫았습니다</summary>
		public const int NotRewarded = 8005;
	}
}
