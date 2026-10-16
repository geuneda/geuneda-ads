// ReSharper disable once CheckNamespace

namespace Geuneda.Ads
{
	/// <summary>표시 중인 광고의 표시·클릭 알림입니다(<see cref="IAdService.Displayed"/>, <see cref="IAdService.Clicked"/>).</summary>
	public sealed class AdShowEvent
	{
		internal AdShowEvent(long showId, AdFormat format, string placement, AdDetails ad, bool isSettled)
		{
			ShowId = showId;
			Format = format;
			Placement = placement;
			Ad = ad;
			IsSettled = isSettled;
		}

		/// <summary>표시 번호(<see cref="AdShowReport.ShowId"/>)</summary>
		public long ShowId { get; }

		public AdFormat Format { get; }

		/// <summary>게임 안 광고 위치. 없으면 null</summary>
		public string Placement { get; }

		public AdDetails Ad { get; }

		/// <summary>결과가 이미 정해진(시간 초과로 끝낸) 표시의 늦은 콜백인지</summary>
		public bool IsSettled { get; }
	}
}
