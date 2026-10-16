using System;
using System.Text.RegularExpressions;
using Geuneda.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace Geuneda.Ads.Tests
{
	public sealed class AdServiceShowTest : AdServiceTestBase
	{
		[Test]
		public void Rewarded_RewardThenHidden_Succeeds()
		{
			var ad = Ad("a");
			LoadReady(AdFormat.Rewarded, ad);

			var result = ShowRewarded("revive");

			Assert.That(Service.IsShowing, Is.True);
			Assert.That(Mediation.Shows, Is.EqualTo(new[] { (AdFormat.Rewarded, "revive") }));
			Assert.That(Showing, Is.EqualTo(new[] { true }));

			Mediation.RaiseDisplayed(AdFormat.Rewarded, ad);
			Mediation.RaiseRewarded(AdFormat.Rewarded, ad);
			Assert.That(result.Done, Is.False, "the result waits for the close");

			Mediation.RaiseHidden(AdFormat.Rewarded, ad);

			Assert.That(result.Value.IsSuccess, Is.True);
			Assert.That(Service.IsShowing, Is.False);
			Assert.That(Showing, Is.EqualTo(new[] { true, false }));
			Assert.That(Reports, Has.Count.EqualTo(1));
			Assert.That(Reports[0].ShowId, Is.EqualTo(1));
			Assert.That(Reports[0].Placement, Is.EqualTo("revive"));
			Assert.That(Reports[0].Reason, Is.EqualTo(AdSettleReason.Closed));
			Assert.That(Reports[0].Displayed, Is.True);
			Assert.That(Reports[0].Ad, Is.SameAs(ad));
			Assert.That(DisplayedEvents, Has.Count.EqualTo(1));
			Assert.That(DisplayedEvents[0].IsSettled, Is.False);
		}

		[Test]
		public void Rewarded_HiddenBeforeReward_WaitsForTheRewardCallback()
		{
			LoadReady(AdFormat.Rewarded);
			var result = ShowRewarded();
			Mediation.RaiseDisplayed(AdFormat.Rewarded);
			Mediation.RaiseHidden(AdFormat.Rewarded);

			Advance(RewardGrace - TimeSpan.FromMilliseconds(100));
			Assert.That(result.Done, Is.False);

			Mediation.RaiseRewarded(AdFormat.Rewarded);

			Assert.That(result.Value.IsSuccess, Is.True);
			Assert.That(Reports[0].Reason, Is.EqualTo(AdSettleReason.Closed));
			Assert.That(LateRewards, Is.Empty);
		}

		[Test]
		public void Rewarded_HiddenWithoutReward_FailsAfterTheGrace_AndALaterRewardIsReportedOnce()
		{
			LoadReady(AdFormat.Rewarded);
			var result = ShowRewarded("shop");
			Mediation.RaiseDisplayed(AdFormat.Rewarded);
			Mediation.RaiseHidden(AdFormat.Rewarded);

			Advance(RewardGrace);

			Assert.That(result.Value.Code, Is.EqualTo(AdsResultCodes.NotRewarded));
			Assert.That(Reports[0].Reason, Is.EqualTo(AdSettleReason.Closed));

			Mediation.RaiseRewarded(AdFormat.Rewarded);
			Mediation.RaiseRewarded(AdFormat.Rewarded);

			Assert.That(LateRewards, Has.Count.EqualTo(1));
			Assert.That(LateRewards[0].ShowId, Is.EqualTo(1));
			Assert.That(LateRewards[0].Placement, Is.EqualTo("shop"));
			Assert.That(LateRewards[0].SettledResult.Code, Is.EqualTo(AdsResultCodes.NotRewarded));
		}

		[Test]
		public void Rewarded_ZeroRewardGrace_FailsAtTheClose()
		{
			Service.Dispose();
			Service = Create(options => options.RewardGrace = TimeSpan.Zero);
			LoadReady(AdFormat.Rewarded);
			var result = ShowRewarded();

			Mediation.RaiseHidden(AdFormat.Rewarded);

			Assert.That(result.Value.Code, Is.EqualTo(AdsResultCodes.NotRewarded));
		}

		[Test]
		public void RewardGrace_CountsOnlyWhileTheAppIsInFront()
		{
			LoadReady(AdFormat.Rewarded);
			var result = ShowRewarded();
			Mediation.RaiseHidden(AdFormat.Rewarded);

			// 닫힌 직후 앱이 뒤로 갔다가 돌아옵니다. 쌓인 보상 콜백은 복귀 뒤에 처리됩니다
			PauseAndResume(10);
			Tick();
			Assert.That(result.Done, Is.False);

			Mediation.RaiseRewarded(AdFormat.Rewarded);

			Assert.That(result.Value.IsSuccess, Is.True);
		}

		[Test]
		public void Interstitial_SucceedsAtTheClose()
		{
			LoadReady(AdFormat.Interstitial);
			var result = ShowInterstitial("stage_end");
			Mediation.RaiseDisplayed(AdFormat.Interstitial);

			Mediation.RaiseHidden(AdFormat.Interstitial);

			Assert.That(result.Value.IsSuccess, Is.True);
			Assert.That(Reports[0].Format, Is.EqualTo(AdFormat.Interstitial));
		}

		[Test]
		public void DisplayFailed_FailsWithTheSdkError()
		{
			LoadReady(AdFormat.Rewarded);
			var result = ShowRewarded();

			Mediation.RaiseDisplayFailed(AdFormat.Rewarded, null, -23, "Already showing");

			Assert.That(result.Value.Code, Is.EqualTo(AdsResultCodes.DisplayFailed));
			Assert.That(result.Value.Message, Does.Contain("Already showing"));
			Assert.That(Reports[0].Reason, Is.EqualTo(AdSettleReason.DisplayFailed));
			Assert.That(Reports[0].Error?.Code, Is.EqualTo(-23));
			Assert.That(Showing, Is.EqualTo(new[] { true, false }));
		}

		[Test]
		public void DisplayFailedInsideShow_IsHandled()
		{
			LoadReady(AdFormat.Rewarded);
			Mediation.OnShow = (format, placement) => Mediation.RaiseDisplayFailed(format);

			var result = ShowRewarded();

			Assert.That(result.Value.Code, Is.EqualTo(AdsResultCodes.DisplayFailed));
			Assert.That(Service.IsShowing, Is.False);
		}

		[Test]
		public void ShowThatThrows_FailsAsDisplayFailedAndLoadsAgain()
		{
			LoadReady(AdFormat.Rewarded);
			Mediation.ShowThrows = new InvalidOperationException("adapter bug");
			LogAssert.Expect(LogType.Error, new Regex(@"Show\(Rewarded\) threw"));
			LogAssert.Expect(LogType.Exception, new Regex("adapter bug"));

			var result = ShowRewarded();

			Assert.That(result.Value.Code, Is.EqualTo(AdsResultCodes.DisplayFailed));
			Assert.That(Service.IsShowing, Is.False);
			Assert.That(Mediation.LoadCount(AdFormat.Rewarded), Is.EqualTo(2));
		}

		[Test]
		public void Rejections_AreReportedWithTheirCodes()
		{
			Mediation.Available.Remove(AdFormat.Interstitial);

			Assert.That(ShowInterstitial().Value.Code, Is.EqualTo(AdsResultCodes.Unavailable));
			Assert.That(ShowRewarded().Value.Code, Is.EqualTo(AdsResultCodes.NotReady));

			LoadReady(AdFormat.Rewarded);
			var showing = ShowRewarded();
			LoadReady(AdFormat.Rewarded);
			Assert.That(ShowRewarded().Value.Code, Is.EqualTo(ResultCodes.InProgress));
			Assert.That(showing.Done, Is.False);

			Assert.That(Reports, Has.Count.EqualTo(3));
			Assert.That(Reports.TrueForAll(r => r.Reason == AdSettleReason.Rejected && r.ShowId == 0), Is.True);
			Assert.That(Showing, Is.EqualTo(new[] { true }), "rejections do not open the screen");
		}

		[Test]
		public void NotInitialized_IsRejected()
		{
			Service.Dispose();
			Mediation = new FakeMediation { IsInitialized = false };
			Mediation.Ready.Add(AdFormat.Rewarded);
			Service = Create();

			Assert.That(ShowRewarded().Value.Code, Is.EqualTo(AdsResultCodes.NotInitialized));
			Assert.That(Mediation.Shows, Is.Empty);
		}

		[Test]
		public void InterstitialWhileRewardedShows_IsRejected()
		{
			LoadReady(AdFormat.Rewarded);
			LoadReady(AdFormat.Interstitial);
			ShowRewarded();

			Assert.That(ShowInterstitial().Value.Code, Is.EqualTo(ResultCodes.InProgress));
		}

		[Test]
		public void Clicks_AreReportedOncePerShow()
		{
			LoadReady(AdFormat.Rewarded);
			ShowRewarded("shop");

			Mediation.RaiseClicked(AdFormat.Rewarded);
			Mediation.RaiseClicked(AdFormat.Rewarded);
			Mediation.RaiseClicked(AdFormat.Rewarded);

			Assert.That(ClickedEvents, Has.Count.EqualTo(1));
			Assert.That(ClickedEvents[0].Placement, Is.EqualTo("shop"));
		}

		[Test]
		public void Revenue_IsReportedWithThePlacement_AndInvalidAmountsBecomeUnknown()
		{
			var ad = Ad("a");
			LoadReady(AdFormat.Rewarded, ad);
			ShowRewarded("shop");

			Mediation.RaiseRevenue(AdFormat.Rewarded, ad, 0.0123);
			Mediation.RaiseRevenue(AdFormat.Rewarded, ad, null);
			LogAssert.Expect(LogType.Warning, new Regex("not a valid amount"));
			Mediation.RaiseRevenue(AdFormat.Rewarded, ad, -1);

			Assert.That(Revenues, Has.Count.EqualTo(3));
			Assert.That(Revenues[0].ShowId, Is.EqualTo(1));
			Assert.That(Revenues[0].Placement, Is.EqualTo("shop"));
			Assert.That(Revenues[0].Platform, Is.EqualTo("FakeMax"));
			Assert.That(Revenues[0].Amount, Is.EqualTo(0.0123));
			Assert.That(Revenues[0].Currency, Is.EqualTo("USD"));
			Assert.That(Revenues[0].Precision, Is.EqualTo("exact"));
			Assert.That(Revenues[1].Amount, Is.Null);
			Assert.That(Revenues[2].Amount, Is.Null);
		}

		[Test]
		public void RevenueWithNoShow_IsReportedWithoutAShow()
		{
			Mediation.RaiseRevenue(AdFormat.Interstitial, null, 0.5);

			Assert.That(Revenues[0].ShowId, Is.Zero);
			Assert.That(Revenues[0].Placement, Is.Null);
			Assert.That(Revenues[0].Ad, Is.SameAs(AdDetails.Unknown));
		}

		[Test]
		public void CallbacksWithNoShow_AreIgnored()
		{
			Mediation.RaiseDisplayed(AdFormat.Rewarded);
			Mediation.RaiseRewarded(AdFormat.Rewarded);
			Mediation.RaiseHidden(AdFormat.Rewarded);

			Assert.That(DisplayedEvents, Is.Empty);
			Assert.That(LateRewards, Is.Empty);
			Assert.That(Reports, Is.Empty);
		}

		[Test]
		public void ShowIds_IncreaseAndRejectionsUseZero()
		{
			LoadReady(AdFormat.Interstitial);
			ShowInterstitial();
			Mediation.RaiseHidden(AdFormat.Interstitial);
			ShowInterstitial();
			LoadReady(AdFormat.Interstitial);
			ShowInterstitial();
			Mediation.RaiseHidden(AdFormat.Interstitial);

			Assert.That(Reports.ConvertAll(r => r.ShowId), Is.EqualTo(new long[] { 1, 0, 2 }));
		}

		[Test]
		public void EmptyPlacement_IsNull()
		{
			LoadReady(AdFormat.Rewarded);

			ShowRewarded("");

			Assert.That(Mediation.Shows[0].Placement, Is.Null);
		}
	}
}
