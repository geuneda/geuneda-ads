using System;
using Geuneda.Core;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace Geuneda.Ads.Tests
{
	public sealed class AdServiceTimeoutTest : AdServiceTestBase
	{
		[Test]
		public void PausingApp_NoEndCallbackAfterReturning_TimesOutOnlyAfterTheForegroundGrace()
		{
			CreatePausingApp();
			LoadReady(AdFormat.Rewarded);
			var result = ShowRewarded();

			// 광고가 앱을 가린 동안은 판정하지 않습니다
			Service.HandlePause(true);
			Advance(600);
			Assert.That(result.Done, Is.False);

			Service.HandlePause(false);
			Mediation.RaiseDisplayed(AdFormat.Rewarded);
			Advance(ForegroundGrace - TimeSpan.FromMilliseconds(100));
			Assert.That(result.Done, Is.False);

			Advance(0.1);

			Assert.That(result.Value.Code, Is.EqualTo(ResultCodes.Timeout));
			Assert.That(Reports[0].Reason, Is.EqualTo(AdSettleReason.AbsoluteTimeout), "600s passed since the show request");
			Assert.That(Mediation.LoadCount(AdFormat.Rewarded), Is.EqualTo(2), "loads the next ad");
		}

		[Test]
		public void PausingApp_ShortAdWithLostHidden_IsAForegroundTimeout()
		{
			CreatePausingApp();
			LoadReady(AdFormat.Rewarded);
			var result = ShowRewarded();
			Mediation.RaiseDisplayed(AdFormat.Rewarded);

			PauseAndResume(30);
			Advance(ForegroundGrace);

			Assert.That(result.Value.Code, Is.EqualTo(ResultCodes.Timeout));
			Assert.That(Reports[0].Reason, Is.EqualTo(AdSettleReason.ForegroundTimeout));
		}

		[Test]
		public void PausingApp_RewardReceivedBeforeTheTimeout_Succeeds()
		{
			CreatePausingApp();
			LoadReady(AdFormat.Rewarded);
			var result = ShowRewarded();
			PauseAndResume(30);
			Mediation.RaiseDisplayed(AdFormat.Rewarded);
			Mediation.RaiseRewarded(AdFormat.Rewarded);

			Advance(ForegroundGrace);

			Assert.That(result.Value.IsSuccess, Is.True);
			Assert.That(Reports[0].Reason, Is.EqualTo(AdSettleReason.ForegroundTimeout));
		}

		[Test]
		public void PausingApp_LeavingAgainRestartsTheGrace()
		{
			CreatePausingApp();
			LoadReady(AdFormat.Rewarded);
			var result = ShowRewarded();
			Mediation.RaiseDisplayed(AdFormat.Rewarded);
			PauseAndResume(30);
			Advance(15);

			PauseAndResume(30);
			Advance(15);

			Assert.That(result.Done, Is.False);
			Advance(5);
			Assert.That(result.Value.Code, Is.EqualTo(ResultCodes.Timeout));
		}

		[Test]
		public void NoDisplayCallback_TimesOutOnEveryPlatform()
		{
			LoadReady(AdFormat.Rewarded);
			var result = ShowRewarded();

			Advance(ForegroundGrace);

			Assert.That(result.Value.Code, Is.EqualTo(ResultCodes.Timeout));
			Assert.That(Reports[0].Reason, Is.EqualTo(AdSettleReason.DisplayTimeout));
			Assert.That(Reports[0].Displayed, Is.False);
		}

		[Test]
		public void AppKeepsRunning_DisplayedAd_IsNotCutBeforeTheAbsoluteTimeout()
		{
			LoadReady(AdFormat.Rewarded);
			var result = ShowRewarded();
			Mediation.RaiseDisplayed(AdFormat.Rewarded);

			Advance(AbsoluteTimeout - TimeSpan.FromSeconds(1));
			Assert.That(result.Done, Is.False, "the app keeps running during the ad, so only the absolute timeout applies");

			Advance(1);

			Assert.That(result.Value.Code, Is.EqualTo(ResultCodes.Timeout));
			Assert.That(Reports[0].Reason, Is.EqualTo(AdSettleReason.AbsoluteTimeout));
		}

		[Test]
		public void AppKeepsRunning_ShowPastTheAbsoluteTimeout_GetsTheRewardThroughLateRewarded()
		{
			var ad = Ad("a");
			LoadReady(AdFormat.Rewarded, ad);
			var result = ShowRewarded("stamina");
			Mediation.RaiseDisplayed(AdFormat.Rewarded, ad);
			Advance(AbsoluteTimeout);
			Assert.That(result.Value.Code, Is.EqualTo(ResultCodes.Timeout));

			// Google 어댑터는 닫을 때 보상 바로 뒤에 닫힘을 보냅니다
			Mediation.RaiseRewarded(AdFormat.Rewarded, ad);
			Mediation.RaiseHidden(AdFormat.Rewarded, ad);

			Assert.That(LateRewards, Has.Count.EqualTo(1));
			Assert.That(LateRewards[0].Placement, Is.EqualTo("stamina"));
			Assert.That(LateRewards[0].SettledResult.Code, Is.EqualTo(ResultCodes.Timeout));
			Assert.That(Reports, Has.Count.EqualTo(1), "the late close does not settle anything again");
		}

		[Test]
		public void PreviousAdCallbacks_DoNotSettleTheNextShow()
		{
			CreatePausingApp();
			var first = Ad("first");
			var second = Ad("second");
			LoadReady(AdFormat.Rewarded, first);
			var firstResult = ShowRewarded("first");
			Mediation.RaiseDisplayed(AdFormat.Rewarded, first);
			PauseAndResume(30);
			Advance(ForegroundGrace);
			Assert.That(firstResult.Value.Code, Is.EqualTo(ResultCodes.Timeout));

			LoadReady(AdFormat.Rewarded, second);
			var secondResult = ShowRewarded("first");
			Mediation.RaiseDisplayed(AdFormat.Rewarded, second);

			// 같은 위치에서 앞 광고의 늦은 콜백이 옵니다
			Mediation.RaiseRewarded(AdFormat.Rewarded, first);
			Mediation.RaiseHidden(AdFormat.Rewarded, first);

			Assert.That(secondResult.Done, Is.False, "the previous ad's close is not the current ad's");
			Assert.That(LateRewards, Has.Count.EqualTo(1));
			Assert.That(LateRewards[0].ShowId, Is.EqualTo(1));

			Mediation.RaiseHidden(AdFormat.Rewarded, second);
			Advance(RewardGrace);

			Assert.That(secondResult.Value.Code, Is.EqualTo(AdsResultCodes.NotRewarded), "the previous reward was not given to the current ad");
		}

		[Test]
		public void CallbacksWithUnknownIdentity_GoToTheCurrentShow()
		{
			var first = new AdDetails("unit", "applovin", null, null, null);
			LoadReady(AdFormat.Rewarded, first);
			ShowRewarded();
			Advance(ForegroundGrace);

			LoadReady(AdFormat.Rewarded, first);
			var second = ShowRewarded();
			Mediation.RaiseRewarded(AdFormat.Rewarded, first);
			Mediation.RaiseHidden(AdFormat.Rewarded, first);

			Assert.That(second.Value.IsSuccess, Is.True, "without an identity the service cannot tell the ads apart");
			Assert.That(LateRewards, Is.Empty);
		}

		[Test]
		public void CallbackWithAnIdentityOfNeitherShow_GoesToTheCurrentShow()
		{
			LoadReady(AdFormat.Rewarded, Ad("first"));
			ShowRewarded();
			Advance(ForegroundGrace);
			LoadReady(AdFormat.Rewarded, Ad("second"));
			var second = ShowRewarded();

			// 식별 값이 로드와 표시 사이에 바뀌는 SDK 라도 지금 광고의 콜백을 잃지 않습니다
			Mediation.RaiseRewarded(AdFormat.Rewarded, Ad("second-changed"));
			Mediation.RaiseHidden(AdFormat.Rewarded, Ad("second-changed"));

			Assert.That(second.Value.IsSuccess, Is.True);
		}

		[Test]
		public void LateCallbackWithNoCurrentShow_GoesToThePreviousShowOnlyWhenTheIdentityMatches()
		{
			LoadReady(AdFormat.Rewarded, Ad("first"));
			ShowRewarded();
			Advance(ForegroundGrace);

			Mediation.RaiseRewarded(AdFormat.Rewarded, Ad("other"));
			Assert.That(LateRewards, Is.Empty, "another ad's reward is not the previous show's");

			Mediation.RaiseRewarded(AdFormat.Rewarded, Ad("first"));
			Assert.That(LateRewards, Has.Count.EqualTo(1));
		}

		[Test]
		public void ExpiredAdReloadedBeforeTheShow_UsesTheNewIdentity()
		{
			LoadReady(AdFormat.Rewarded, Ad("expired"));
			LoadReady(AdFormat.Rewarded, Ad("fresh"));
			ShowRewarded();
			Advance(ForegroundGrace);
			LoadReady(AdFormat.Rewarded, Ad("next"));
			var next = ShowRewarded();

			Mediation.RaiseHidden(AdFormat.Rewarded, Ad("fresh"));
			Advance(RewardGrace);

			Assert.That(next.Done, Is.False, "the timed-out show's ad is the reloaded one");
		}

		[Test]
		public void LateCallbacksOfThePreviousShow_AreMarkedSettled()
		{
			var ad = Ad("a");
			LoadReady(AdFormat.Rewarded, ad);
			ShowRewarded();
			Advance(ForegroundGrace);

			Mediation.RaiseDisplayed(AdFormat.Rewarded, ad);

			Assert.That(DisplayedEvents, Has.Count.EqualTo(1));
			Assert.That(DisplayedEvents[0].IsSettled, Is.True);
			Assert.That(DisplayedEvents[0].ShowId, Is.EqualTo(1));
		}

		[Test]
		public void InterstitialTimeout_HasNoLateReward()
		{
			var ad = Ad("a");
			LoadReady(AdFormat.Interstitial, ad);
			var result = ShowInterstitial();
			Advance(ForegroundGrace);

			Mediation.RaiseRewarded(AdFormat.Interstitial, ad);

			Assert.That(result.Value.Code, Is.EqualTo(ResultCodes.Timeout));
			Assert.That(LateRewards, Is.Empty);
		}

		[Test]
		public void PausingApp_PlayerLoopStoppedWithoutPauseMessages_WaitsForTheQueuedClose()
		{
			// MAX iOS 는 표시 때 UnityPause(YES) 로 플레이어 루프를 멈추고 닫힘 때 풉니다. 일시정지 메시지가 오는지는 보장되지 않습니다
			CreatePausingApp();
			LoadReady(AdFormat.Rewarded);
			var result = ShowRewarded();
			Mediation.RaiseDisplayed(AdFormat.Rewarded);

			FreezeFrames(600);
			Assert.That(result.Done, Is.False, "the first frame after the stop does not beat the SDK's queued callbacks");

			Mediation.RaiseRewarded(AdFormat.Rewarded);
			Mediation.RaiseHidden(AdFormat.Rewarded);

			Assert.That(result.Value.IsSuccess, Is.True);
		}

		[Test]
		public void PausingApp_PlayerLoopStoppedAndTheCloseLost_TimesOutAfterTheGrace()
		{
			CreatePausingApp();
			LoadReady(AdFormat.Rewarded);
			var result = ShowRewarded();
			Mediation.RaiseDisplayed(AdFormat.Rewarded);
			FreezeFrames(30);

			Advance(ForegroundGrace - Frame);
			Assert.That(result.Done, Is.False);
			Advance(Frame);

			Assert.That(result.Value.Code, Is.EqualTo(ResultCodes.Timeout));
			Assert.That(Reports[0].Reason, Is.EqualTo(AdSettleReason.ForegroundTimeout));
		}

		[Test]
		public void PausingApp_StopAfterAnEarlierLongFrame_StillWaitsForTheQueuedCallbacks()
		{
			// 리뷰 3회차 재현: 앞선 긴 프레임의 상태가 다음 정지의 첫 프레임 보호를 없애면 안 됩니다
			CreatePausingApp();
			LoadReady(AdFormat.Rewarded);
			FreezeFrames(1);
			var result = ShowRewarded();
			FreezeFrames(30);

			Assert.That(result.Done, Is.False);
			Mediation.RaiseDisplayed(AdFormat.Rewarded);
			Mediation.RaiseRewarded(AdFormat.Rewarded);
			Mediation.RaiseHidden(AdFormat.Rewarded);

			Assert.That(result.Value.IsSuccess, Is.True);
			Assert.That(LateRewards, Is.Empty);
		}

		[Test]
		public void PausingApp_LongStopAfterDisplay_WaitsForCallbacksThatArriveFramesLater()
		{
			CreatePausingApp();
			LoadReady(AdFormat.Rewarded);
			var result = ShowRewarded();
			Mediation.RaiseDisplayed(AdFormat.Rewarded);

			FreezeFrames(600);
			Advance(Frame);
			Assert.That(result.Done, Is.False, "the SDK's callbacks may reach the queue a few frames after the loop resumes");

			Mediation.RaiseRewarded(AdFormat.Rewarded);
			Mediation.RaiseHidden(AdFormat.Rewarded);

			Assert.That(result.Value.IsSuccess, Is.True);
		}

		[Test]
		public void PausingApp_RepeatedStopsInOneShow_StillWaitForTheQueuedCallbacks()
		{
			CreatePausingApp();
			LoadReady(AdFormat.Rewarded);
			var result = ShowRewarded();
			Mediation.RaiseDisplayed(AdFormat.Rewarded);
			FreezeFrames(2);
			Advance(1);
			FreezeFrames(2);

			FreezeFrames(600);

			Assert.That(result.Done, Is.False);
			Mediation.RaiseRewarded(AdFormat.Rewarded);
			Mediation.RaiseHidden(AdFormat.Rewarded);
			Assert.That(result.Value.IsSuccess, Is.True);
		}

		[Test]
		public void TimeoutRightAfterAStop_EndsOnceTheConfirmationPasses()
		{
			LoadReady(AdFormat.Rewarded);
			var result = ShowRewarded();

			FreezeFrames(30);
			Assert.That(result.Done, Is.False);
			Advance(Frame);
			Assert.That(result.Done, Is.False);
			Advance(Frame);

			Assert.That(result.Value.Code, Is.EqualTo(ResultCodes.Timeout));
			Assert.That(Reports[0].Reason, Is.EqualTo(AdSettleReason.DisplayTimeout));
		}

		[Test]
		public void SlowFrames_DoNotPostponeTheTimeoutsForever()
		{
			LoadReady(AdFormat.Rewarded);
			var noDisplay = ShowRewarded();

			// 프레임마다 1초(모든 Tick 이 프레임 간격)여도 판정은 연달아 건너뛰지 않습니다
			SlowFrames(1, 22);
			Assert.That(noDisplay.Value.Code, Is.EqualTo(ResultCodes.Timeout));
			Assert.That(Reports[0].Reason, Is.EqualTo(AdSettleReason.DisplayTimeout));

			LoadReady(AdFormat.Rewarded);
			var closed = ShowRewarded();
			Mediation.RaiseDisplayed(AdFormat.Rewarded);
			Mediation.RaiseHidden(AdFormat.Rewarded);
			SlowFrames(1, 4);

			Assert.That(closed.Value.Code, Is.EqualTo(AdsResultCodes.NotRewarded), "the reward wait ends too");
		}

		[Test]
		public void PausingApp_RepeatedLongFrames_StillEndAtTheAbsoluteTimeout()
		{
			CreatePausingApp();
			LoadReady(AdFormat.Rewarded);
			var result = ShowRewarded();
			Mediation.RaiseDisplayed(AdFormat.Rewarded);

			// 20초가 되기 전에 매번 긴 프레임이 와 복귀 뒤 닫힘 대기는 끝나지 않지만 절대 상한이 끝냅니다
			for (var i = 0; i < 40 && !result.Done; i++)
			{
				Advance(10);
				FreezeFrames(2);
			}

			Assert.That(result.Value.Code, Is.EqualTo(ResultCodes.Timeout));
			Assert.That(Reports[0].Reason, Is.EqualTo(AdSettleReason.AbsoluteTimeout));
			Assert.That(Reports[0].Elapsed, Is.LessThan(AbsoluteTimeout + TimeSpan.FromSeconds(13)));
		}

		[Test]
		public void ShowThatFailsAfterATimeout_DoesNotDropTheTimedOutShowsLateCallbacks()
		{
			LoadReady(AdFormat.Rewarded, Ad("a"));
			ShowRewarded("first");
			Mediation.RaiseDisplayed(AdFormat.Rewarded, Ad("a"));
			Advance(AbsoluteTimeout);

			LoadReady(AdFormat.Rewarded, Ad("b"));
			var second = ShowRewarded("second");
			Mediation.RaiseDisplayFailed(AdFormat.Rewarded, Ad("b"));
			Assert.That(second.Value.Code, Is.EqualTo(AdsResultCodes.DisplayFailed));

			Mediation.RaiseRewarded(AdFormat.Rewarded, Ad("a"));
			Assert.That(LateRewards, Has.Count.EqualTo(1));
			Assert.That(LateRewards[0].ShowId, Is.EqualTo(1));

			LoadReady(AdFormat.Rewarded, Ad("c"));
			var third = ShowRewarded("third");
			Mediation.RaiseHidden(AdFormat.Rewarded, Ad("a"));
			Mediation.RaiseRewarded(AdFormat.Rewarded, Ad("a"));
			Advance(RewardGrace);

			Assert.That(third.Done, Is.False, "the first ad's late callbacks are not the third's");
			Assert.That(LateRewards, Has.Count.EqualTo(1));
		}

		[Test]
		public void OlderSettledShows_AreForgottenBeyondTheLimit()
		{
			LoadReady(AdFormat.Interstitial, Ad("old"));
			ShowInterstitial();
			Advance(ForegroundGrace);

			for (var i = 0; i < AdService.RetainedSettledShows; i++)
			{
				LoadReady(AdFormat.Interstitial, Ad("i" + i));
				ShowInterstitial();
				Mediation.RaiseHidden(AdFormat.Interstitial, Ad("i" + i));
			}

			LoadReady(AdFormat.Interstitial, Ad("now"));
			var current = ShowInterstitial();
			Mediation.RaiseHidden(AdFormat.Interstitial, Ad("old"));

			Assert.That(current.Value.IsSuccess, Is.True, "a forgotten show's identity matches nothing, so the close goes to the current show");
		}

		[Test]
		public void LateDisplayFailure_DoesNotEndADisplayedShow()
		{
			LoadReady(AdFormat.Rewarded, Ad("a"));
			ShowRewarded();
			Advance(ForegroundGrace);
			LoadReady(AdFormat.Rewarded, Ad("b"));
			var second = ShowRewarded();
			Mediation.RaiseDisplayed(AdFormat.Rewarded, Ad("b"));

			// 식별 값을 되살리지 못한 앞 광고의 늦은 표시 실패
			Mediation.RaiseDisplayFailed(AdFormat.Rewarded, Ad("a-unknown"));

			Assert.That(second.Done, Is.False);
			Mediation.RaiseRewarded(AdFormat.Rewarded, Ad("b"));
			Mediation.RaiseHidden(AdFormat.Rewarded, Ad("b"));
			Assert.That(second.Value.IsSuccess, Is.True);
		}

		[Test]
		public void NotFocused_DoesNotTimeOut()
		{
			LoadReady(AdFormat.Rewarded);
			var result = ShowRewarded();

			Service.HandleFocus(false);
			Advance(AbsoluteTimeout + AbsoluteTimeout);

			Assert.That(result.Done, Is.False);
		}
	}
}
