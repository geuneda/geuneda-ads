using System;
using Geuneda.Analytics;
using Geuneda.Core;

// ReSharper disable once CheckNamespace

namespace Geuneda.Ads.Analytics
{
	/// <summary>
	/// 광고 노출 수익(<see cref="IAdService.RevenuePaid"/>)을 analytics 의 광고 노출 표준 이벤트(<c>LogAdImpression</c>)로 기록합니다.
	/// SDK 가 노출마다 한 번 알리는 값이라 매출이 두 번 잡히지 않습니다.
	/// </summary>
	/// <remarks>
	/// <para>com.geuneda.analytics 0.2.0 이상이 있으면 켜지는 선택 어셈블리입니다. 메인 스레드에서 만들고, 끝나면 <see cref="Dispose"/> 합니다.</para>
	/// <para>금액을 모르는 노출(MAX 의 -1)은 0 으로 기록하지 않고 건너뜁니다(Debug 로그). 통화 코드가 없거나 잘못된 노출도 건너뛰고 Warning 을 남깁니다.</para>
	/// </remarks>
	public sealed class AdRevenueLogger : IDisposable
	{
		private static readonly ModuleLogger Logger = new ModuleLogger("Geuneda.Ads.Analytics");

		private readonly IAdService _ads;
		private readonly IAnalyticsService _analytics;
		private readonly AnalyticsSinks? _sinks;
		private bool _disposed;

		/// <param name="ads">수익을 받을 서비스</param>
		/// <param name="analytics">기록할 서비스</param>
		/// <param name="sinks">보낼 sink. null 이면 analytics 의 기본 sink</param>
		public AdRevenueLogger(IAdService ads, IAnalyticsService analytics, AnalyticsSinks? sinks = null)
		{
			_ads = ads ?? throw new ArgumentNullException(nameof(ads));
			_analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
			_sinks = sinks;
			_ads.RevenuePaid += OnRevenuePaid;
		}

		/// <summary>분석 이벤트의 광고 형식 값(<c>rewarded</c>, <c>interstitial</c>)</summary>
		public static string FormatName(AdFormat format)
		{
			switch (format)
			{
				case AdFormat.Rewarded:
					return "rewarded";
				case AdFormat.Interstitial:
					return "interstitial";
				default:
					throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown ad format");
			}
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			_ads.RevenuePaid -= OnRevenuePaid;
		}

		private void OnRevenuePaid(AdRevenue revenue)
		{
			if (!revenue.Amount.HasValue)
			{
				Logger.Debug($"Skipping {revenue.Format} ad revenue of unknown amount ({revenue.Ad})");
				return;
			}

			if (!IsCurrencyCode(revenue.Currency))
			{
				Logger.Warning($"Skipping {revenue.Format} ad revenue with currency '{revenue.Currency}'");
				return;
			}

			var impression = new AnalyticsAdImpression(revenue.Platform, revenue.Ad.NetworkName, FormatName(revenue.Format), revenue.Ad.UnitId,
				revenue.Amount.Value, revenue.Currency, revenue.Placement);

			if (_sinks.HasValue)
			{
				_analytics.LogAdImpression(impression, null, _sinks.Value);
			}
			else
			{
				_analytics.LogAdImpression(impression);
			}
		}

		private static bool IsCurrencyCode(string code)
		{
			if (code == null || code.Length != 3)
			{
				return false;
			}

			foreach (var c in code)
			{
				if (!(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z'))
				{
					return false;
				}
			}

			return true;
		}
	}
}
