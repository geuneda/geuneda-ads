// ReSharper disable once CheckNamespace

namespace Geuneda.Ads
{
	/// <summary>미디에이션 SDK 가 알린 노출 한 번의 수익입니다(<see cref="IAdMediation.RevenuePaid"/>).</summary>
	public readonly struct AdPaid
	{
		/// <param name="amount">수익. 모르면 null(MAX 는 -1 을 줌)</param>
		/// <param name="currency">ISO 4217 통화 코드(MAX 는 항상 USD)</param>
		/// <param name="precision">금액의 정확도(MAX: <c>publisher_defined</c>, <c>exact</c>, <c>estimated</c>, <c>undefined</c>). 모르면 null</param>
		public AdPaid(double? amount, string currency, string precision)
		{
			Amount = amount;
			Currency = string.IsNullOrEmpty(currency) ? null : currency;
			Precision = string.IsNullOrEmpty(precision) ? null : precision;
		}

		public double? Amount { get; }

		public string Currency { get; }

		public string Precision { get; }
	}
}
