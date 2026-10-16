// ReSharper disable once CheckNamespace

namespace Geuneda.Ads
{
	/// <summary>표시 요청의 결과가 정해진 경로입니다(<see cref="AdShowReport.Reason"/>).</summary>
	public enum AdSettleReason
	{
		/// <summary>표시 전에 거절했습니다(초기화 전, 준비 안 됨, 단위 없음, 다른 광고 표시 중). 이유는 결과 코드로 봅니다</summary>
		Rejected = 1,

		/// <summary>광고가 닫혔습니다(SDK 의 닫힘 콜백)</summary>
		Closed = 2,

		/// <summary>SDK 가 표시 실패를 알렸습니다</summary>
		DisplayFailed = 3,

		/// <summary>앱이 앞에 있는 동안 표시 콜백도 실패 콜백도 오지 않았습니다</summary>
		DisplayTimeout = 4,

		/// <summary>광고가 표시 중 앱을 멈추는 미디에이션(MAX 의 Android·iOS)에서 앱이 앞으로 돌아온 뒤 닫힘 콜백이 오지 않았습니다</summary>
		ForegroundTimeout = 5,

		/// <summary>표시 요청부터 절대 상한이 지나도록 닫힘 콜백이 오지 않았습니다</summary>
		AbsoluteTimeout = 6,

		/// <summary>표시 중에 서비스가 정리됐습니다</summary>
		Disposed = 7
	}
}
