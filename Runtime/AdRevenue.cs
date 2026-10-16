// ReSharper disable once CheckNamespace

namespace Geuneda.Ads
{
	/// <summary>광고 노출 수익입니다(<see cref="IAdService.RevenuePaid"/>). 노출마다 SDK 가 한 번 알립니다.</summary>
	public sealed class AdRevenue
	{
		internal AdRevenue(long showId, AdFormat format, string placement, string platform, AdDetails ad, double? amount, string currency,
			string precision)
		{
			ShowId = showId;
			Format = format;
			Placement = placement;
			Platform = platform;
			Ad = ad;
			Amount = amount;
			Currency = currency;
			Precision = precision;
		}

		/// <summary>수익이 속한 표시(<see cref="AdShowReport.ShowId"/>). 어느 표시인지 모르면 0</summary>
		public long ShowId { get; }

		public AdFormat Format { get; }

		/// <summary>게임 안 광고 위치. 모르면 null</summary>
		public string Placement { get; }

		/// <summary>미디에이션 이름(<see cref="IAdMediation.Platform"/>, 예: AppLovin)</summary>
		public string Platform { get; }

		public AdDetails Ad { get; }

		/// <summary>수익. 0 이상의 유한한 수이고, SDK 가 모른다고 했거나 값이 잘못됐으면 null 입니다</summary>
		public double? Amount { get; }

		/// <summary>ISO 4217 통화 코드. 모르면 null</summary>
		public string Currency { get; }

		/// <summary>금액의 정확도. 모르면 null</summary>
		public string Precision { get; }
	}
}
