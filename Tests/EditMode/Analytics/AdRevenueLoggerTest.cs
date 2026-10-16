using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Geuneda.Ads.Analytics;
using Geuneda.Analytics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace Geuneda.Ads.Tests
{
	public sealed class AdRevenueLoggerTest : AdServiceTestBase
	{
		private RecordingAnalytics _analytics;

		[SetUp]
		public void SetUpAnalytics()
		{
			_analytics = new RecordingAnalytics();
		}

		[Test]
		public void Revenue_IsLoggedAsTheAdImpressionStandardEvent()
		{
			using (new AdRevenueLogger(Service, _analytics))
			{
				var ad = new AdDetails("unit-r", "Google AdMob", "np", "c1", "id");
				LoadReady(AdFormat.Rewarded, ad);
				ShowRewarded("revive");

				Mediation.RaiseRevenue(AdFormat.Rewarded, ad, 0.0042);

				Assert.That(_analytics.Events, Has.Count.EqualTo(1));
				var logged = _analytics.Events[0];
				Assert.That(logged.Name, Is.EqualTo(AnalyticsStandardEvents.AdImpression));
				Assert.That(logged.Sinks, Is.EqualTo(_analytics.DefaultSinks));
				Assert.That(logged.Parameters[AnalyticsStandardParameters.AdPlatform], Is.EqualTo("FakeMax"));
				Assert.That(logged.Parameters[AnalyticsStandardParameters.AdSource], Is.EqualTo("Google AdMob"));
				Assert.That(logged.Parameters[AnalyticsStandardParameters.AdFormat], Is.EqualTo("rewarded"));
				Assert.That(logged.Parameters[AnalyticsStandardParameters.AdUnitName], Is.EqualTo("unit-r"));
				Assert.That(logged.Parameters[AnalyticsStandardParameters.AdPlacement], Is.EqualTo("revive"));
				Assert.That(logged.Parameters[AnalyticsStandardParameters.Value], Is.EqualTo(0.0042));
				Assert.That(logged.Parameters[AnalyticsStandardParameters.Currency], Is.EqualTo("USD"));
			}
		}

		[Test]
		public void UnknownAmountsAndBadCurrencies_AreSkipped()
		{
			using (new AdRevenueLogger(Service, _analytics))
			{
				Mediation.RaiseRevenue(AdFormat.Interstitial, null, null);
				LogAssert.Expect(LogType.Warning, new Regex("currency 'US'"));
				Mediation.RaiseRevenue(AdFormat.Interstitial, null, 0.1, "US");
				Mediation.RaiseRevenue(AdFormat.Interstitial, null, 0.1, "usd");

				Assert.That(_analytics.Events, Has.Count.EqualTo(1));
				Assert.That(_analytics.Events[0].Parameters[AnalyticsStandardParameters.AdFormat], Is.EqualTo("interstitial"));
				Assert.That(_analytics.Events[0].Parameters[AnalyticsStandardParameters.Currency], Is.EqualTo("USD"));
				Assert.That(_analytics.Events[0].Parameters.ContainsKey(AnalyticsStandardParameters.AdPlacement), Is.False);
			}
		}

		[Test]
		public void GivenSinks_AreUsed_AndDisposeStopsLogging()
		{
			var logger = new AdRevenueLogger(Service, _analytics, AnalyticsSinks.Firebase);
			Mediation.RaiseRevenue(AdFormat.Rewarded, null, 0.1);

			logger.Dispose();
			logger.Dispose();
			Mediation.RaiseRevenue(AdFormat.Rewarded, null, 0.1);

			Assert.That(_analytics.Events, Has.Count.EqualTo(1));
			Assert.That(_analytics.Events[0].Sinks, Is.EqualTo(AnalyticsSinks.Firebase));
		}

		[Test]
		public void FormatName_RejectsUnknownFormats()
		{
			Assert.That(AdRevenueLogger.FormatName(AdFormat.Rewarded), Is.EqualTo("rewarded"));
			Assert.Throws<ArgumentOutOfRangeException>(() => AdRevenueLogger.FormatName((AdFormat) 0));
			Assert.Throws<ArgumentNullException>(() => new AdRevenueLogger(null, _analytics));
			Assert.Throws<ArgumentNullException>(() => new AdRevenueLogger(Service, null));
		}

		private sealed class RecordingAnalytics : IAnalyticsService
		{
			public List<(string Name, IReadOnlyDictionary<string, object> Parameters, AnalyticsSinks Sinks)> Events { get; } =
				new List<(string, IReadOnlyDictionary<string, object>, AnalyticsSinks)>();

			public AnalyticsSinks DefaultSinks => AnalyticsSinks.Console | AnalyticsSinks.Collector;

			public string UserId => null;

			public IReadOnlyDictionary<string, string> UserProperties => new Dictionary<string, string>();

			public void LogEvent(string name, IReadOnlyDictionary<string, object> parameters = null)
			{
				LogEvent(name, parameters, DefaultSinks);
			}

			public void LogEvent(string name, IReadOnlyDictionary<string, object> parameters, AnalyticsSinks sinks)
			{
				Events.Add((name, parameters, sinks));
			}

			public void SetUserId(string userId)
			{
			}

			public void SetUserProperty(string name, string value)
			{
			}

			public void AddSink(IAnalyticsSink sink)
			{
				throw new NotSupportedException();
			}

			public void Dispose()
			{
			}
		}
	}
}
