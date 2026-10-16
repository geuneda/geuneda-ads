using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Geuneda.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace Geuneda.Ads.Tests
{
	public sealed class AdServiceLifecycleTest : AdServiceTestBase
	{
		[Test]
		public void Dispose_EndsTheShowAsCancelled_AndUnsubscribes()
		{
			LoadReady(AdFormat.Rewarded);
			var result = ShowRewarded();

			Service.Dispose();

			Assert.That(result.Value.Code, Is.EqualTo(ResultCodes.Cancelled));
			Assert.That(Reports[0].Reason, Is.EqualTo(AdSettleReason.Disposed));
			Assert.That(Showing, Is.EqualTo(new[] { true, false }));
			Assert.That(Mediation.HasSubscribers, Is.False);
			Assert.Throws<ObjectDisposedException>(() => Service.ShowRewardedAsync());
			Assert.Throws<ObjectDisposedException>(() => Service.IsReady(AdFormat.Rewarded));
			Assert.DoesNotThrow(() => Service.Dispose());
		}

		[Test]
		public void Dispose_AfterTheReward_StillSucceeds()
		{
			LoadReady(AdFormat.Rewarded);
			var result = ShowRewarded();
			Mediation.RaiseRewarded(AdFormat.Rewarded);

			Service.Dispose();

			Assert.That(result.Value.IsSuccess, Is.True);
		}

		[Test]
		public void TickAfterDispose_DoesNothing()
		{
			Service.Dispose();
			var loads = Mediation.Loads.Count;

			Advance(LoadTimeout + LoadTimeout);

			Assert.That(Mediation.Loads, Has.Count.EqualTo(loads));
		}

		[Test]
		public void HandlerExceptions_AreLogged_AndTheCallerStillGetsTheResult()
		{
			Service.ShowSettled += _ => throw new InvalidOperationException("game bug");
			Service.ShowingChanged += _ => throw new InvalidOperationException("game bug");
			LoadReady(AdFormat.Interstitial);
			LogAssert.Expect(LogType.Error, new Regex("ShowingChanged handler threw"));
			LogAssert.Expect(LogType.Exception, new Regex("game bug"));
			var result = ShowInterstitial();

			LogAssert.Expect(LogType.Error, new Regex("ShowingChanged handler threw"));
			LogAssert.Expect(LogType.Exception, new Regex("game bug"));
			LogAssert.Expect(LogType.Error, new Regex("ShowSettled handler threw"));
			LogAssert.Expect(LogType.Exception, new Regex("game bug"));
			Mediation.RaiseHidden(AdFormat.Interstitial);

			Assert.That(result.Value.IsSuccess, Is.True);
			Assert.That(Reports, Has.Count.EqualTo(1), "other handlers still run");
		}

		[Test]
		public void SettledHandler_CanShowTheNextAdRightAway()
		{
			LoadReady(AdFormat.Rewarded);
			LoadReady(AdFormat.Interstitial);
			Captured<Result> next = null;
			Service.ShowSettled += report =>
			{
				if (report.Format == AdFormat.Rewarded)
				{
					next = ShowInterstitial();
				}
			};
			var first = ShowRewarded();
			Mediation.RaiseRewarded(AdFormat.Rewarded);

			Mediation.RaiseHidden(AdFormat.Rewarded);

			Assert.That(first.Value.IsSuccess, Is.True);
			Assert.That(next.Done, Is.False);
			Assert.That(Service.IsShowing, Is.True, "the interstitial opened inside the handler");
			Assert.That(Showing, Is.EqualTo(new[] { true, false, true }));

			Mediation.RaiseHidden(AdFormat.Interstitial);
			Assert.That(next.Value.IsSuccess, Is.True);
		}

		[Test]
		public void CallerContinuation_SeesTheNextLoadAlreadyRequested()
		{
			LoadReady(AdFormat.Rewarded);
			Captured<Result> retry = null;
			var first = Service.ShowRewardedAsync();
			first.ContinueWith(_ => retry = ShowRewarded()).Forget();
			Mediation.RaiseRewarded(AdFormat.Rewarded);

			Mediation.RaiseHidden(AdFormat.Rewarded);

			Assert.That(retry.Value.Code, Is.EqualTo(AdsResultCodes.NotReady));
			Assert.That(Mediation.LoadCount(AdFormat.Rewarded), Is.EqualTo(2), "one load for the next ad, not two");
		}

		[Test]
		public void ShowingChangedHandlerThatDisposes_EndsTheShowWithoutCallingTheSdk()
		{
			LoadReady(AdFormat.Rewarded);
			Service.ShowingChanged += showing =>
			{
				if (showing)
				{
					Service.Dispose();
				}
			};

			var result = ShowRewarded();

			Assert.That(result.Value.Code, Is.EqualTo(ResultCodes.Cancelled));
			Assert.That(Mediation.Shows, Is.Empty);
		}

		[Test]
		public void ShowStartedInASettledHandler_IsNotSentToTheSdkWhenItsOpenHandlerDisposes()
		{
			LoadReady(AdFormat.Rewarded);
			LoadReady(AdFormat.Interstitial);
			Captured<Result> next = null;
			Service.ShowSettled += report =>
			{
				if (report.Format == AdFormat.Rewarded)
				{
					next = ShowInterstitial();
				}
			};
			Service.ShowingChanged += showing =>
			{
				if (showing && Service.IsShowing && next != null)
				{
					Service.Dispose();
				}
			};
			ShowRewarded();
			Mediation.RaiseRewarded(AdFormat.Rewarded);

			Mediation.RaiseHidden(AdFormat.Rewarded);

			Assert.That(next.Value.Code, Is.EqualTo(ResultCodes.Cancelled));
			Assert.That(Mediation.Shows.ConvertAll(s => s.Format), Is.EqualTo(new[] { AdFormat.Rewarded }), "the interstitial never reached the SDK");
		}

		[Test]
		public void ShowStartedFromTheCallerContinuation_IsNotSentToTheSdkWhenItsOpenHandlerDisposes()
		{
			LoadReady(AdFormat.Rewarded);
			LoadReady(AdFormat.Interstitial);
			Captured<Result> next = null;
			var first = Service.ShowRewardedAsync();
			first.ContinueWith(_ => next = ShowInterstitial()).Forget();
			Service.ShowingChanged += showing =>
			{
				if (showing && next != null)
				{
					Service.Dispose();
				}
			};
			Mediation.RaiseRewarded(AdFormat.Rewarded);

			Mediation.RaiseHidden(AdFormat.Rewarded);

			Assert.That(next.Value.Code, Is.EqualTo(ResultCodes.Cancelled));
			Assert.That(Mediation.Shows.ConvertAll(s => s.Format), Is.EqualTo(new[] { AdFormat.Rewarded }));
		}

		[Test]
		public void CallsFromAnotherThread_Throw()
		{
			var error = Task.Run(() =>
			{
				try
				{
					Service.ShowRewardedAsync();
					return null;
				}
				catch (Exception exception)
				{
					return exception;
				}
			}).Result;

			Assert.That(error, Is.TypeOf<InvalidOperationException>());
		}

		[Test]
		public void InvalidOptions_AreRejected()
		{
			Assert.Throws<ArgumentNullException>(() => new AdService(null, null, Clock, false, true));
			AssertRejected(o => o.RetryDelay = TimeSpan.FromMilliseconds(99));
			AssertRejected(o => o.MaxRetryDelay = TimeSpan.FromSeconds(1));
			AssertRejected(o => o.MaxRetryDelay = TimeSpan.FromDays(2));
			AssertRejected(o => o.LoadTimeout = TimeSpan.FromMilliseconds(500));
			AssertRejected(o => o.ForegroundGrace = TimeSpan.Zero);
			AssertRejected(o => o.AbsoluteTimeout = TimeSpan.FromSeconds(10));
			AssertRejected(o => o.RewardGrace = TimeSpan.FromSeconds(-1));
		}

		[Test]
		public void DefaultOptions_FollowTheReferenceValues()
		{
			var options = new AdServiceOptions();

			Assert.That(options.RetryDelay, Is.EqualTo(TimeSpan.FromSeconds(2)));
			Assert.That(options.MaxRetryDelay, Is.EqualTo(TimeSpan.FromSeconds(30)));
			Assert.That(options.ForegroundGrace, Is.EqualTo(TimeSpan.FromSeconds(20)));
			Assert.That(options.AbsoluteTimeout, Is.EqualTo(TimeSpan.FromMinutes(5)));
			Assert.That(options.RewardGrace, Is.EqualTo(TimeSpan.FromSeconds(2)));
			Assert.That(options.AdPausesApp, Is.Null);
			Assert.DoesNotThrow(() => new AdService(new FakeMediation(), null, Clock, false, true).Dispose());
		}

		[Test]
		public void AdPausesApp_OverridesTheMediation()
		{
			Service.Dispose();
			Service = Create(options => options.AdPausesApp = true);
			LoadReady(AdFormat.Rewarded);
			var result = ShowRewarded();
			Mediation.RaiseDisplayed(AdFormat.Rewarded);

			Advance(ForegroundGrace);

			Assert.That(Reports[0].Reason, Is.EqualTo(AdSettleReason.ForegroundTimeout));
			Assert.That(result.Value.Code, Is.EqualTo(ResultCodes.Timeout));

			CreatePausingApp(options => options.AdPausesApp = false);
			LoadReady(AdFormat.Rewarded);
			var kept = ShowRewarded();
			Mediation.RaiseDisplayed(AdFormat.Rewarded);
			Advance(ForegroundGrace + ForegroundGrace);

			Assert.That(kept.Done, Is.False, "the option turns the mediation's pause off");
		}

		[Test]
		public void DisposeInAShowingChangedHandler_LaterSubscribersSeeTheChangesInOrder()
		{
			LoadReady(AdFormat.Rewarded);
			Service.ShowingChanged += showing =>
			{
				if (showing)
				{
					Service.Dispose();
				}
			};
			var later = new System.Collections.Generic.List<bool>();
			Service.ShowingChanged += later.Add;

			ShowRewarded();

			Assert.That(later, Is.EqualTo(new[] { true, false }));
			Assert.That(Showing, Is.EqualTo(new[] { true, false }));
		}

		[Test]
		public void ShowInAShowingChangedHandler_LaterSubscribersSeeTheChangesInOrder()
		{
			LoadReady(AdFormat.Rewarded);
			LoadReady(AdFormat.Interstitial);
			var order = new System.Collections.Generic.List<string>();
			Captured<Result> next = null;
			Service.ShowingChanged += showing =>
			{
				if (!showing && next == null)
				{
					next = ShowInterstitial();
				}
			};
			Service.ShowingChanged += showing => order.Add(showing ? "open" : "close");
			Service.ShowSettled += report => order.Add("settled " + report.Format);
			var first = Service.ShowRewardedAsync();
			first.ContinueWith(_ => order.Add("done")).Forget();
			Mediation.RaiseRewarded(AdFormat.Rewarded);

			Mediation.RaiseHidden(AdFormat.Rewarded);

			Assert.That(order, Is.EqualTo(new[] { "open", "close", "settled Rewarded", "done", "open" }),
				"every subscriber sees the first show end (close, report, caller) before the next show opens");
			Assert.That(next.Done, Is.False);
			Assert.That(Service.IsShowing, Is.True);
		}

		[Test]
		public void DisposeInALoadFailedHandler_StopsTheRestOfTheTick()
		{
			Advance(58);
			Mediation.RaiseLoadFailed(AdFormat.Interstitial);
			Service.LoadFailed += failure =>
			{
				if (failure.Format == AdFormat.Rewarded)
				{
					Service.Dispose();
				}
			};

			// 60초에 보상형 로드 제한 시간과 전면 재시도가 같은 프레임에 옵니다
			Advance(2);

			Assert.That(LoadFailures.ConvertAll(f => f.Format), Is.EqualTo(new[] { AdFormat.Interstitial, AdFormat.Rewarded }));
			Assert.That(Mediation.LoadCount(AdFormat.Interstitial), Is.EqualTo(1), "the disposed service does not start the interstitial retry");
		}

		[Test]
		public void LateFailureAfterTheRetryStarted_CountsForTheRetry()
		{
			// SDK 가 로드 요청을 구분해 주지 않아(MAX 는 단위만 줌) 재시도 뒤에 온 앞 요청의 실패를 가려낼 수 없습니다(README 에 적은 한계)
			Advance(LoadTimeout);
			Advance(RetryDelay);
			Assert.That(Mediation.LoadCount(AdFormat.Rewarded), Is.EqualTo(2));

			Mediation.RaiseLoadFailed(AdFormat.Rewarded);

			Assert.That(LoadFailures.FindLast(f => f.Format == AdFormat.Rewarded).Attempt, Is.EqualTo(2));
		}

		private void AssertRejected(Action<AdServiceOptions> configure)
		{
			var options = new AdServiceOptions();
			configure(options);
			Assert.Throws<ArgumentOutOfRangeException>(() => new AdService(new FakeMediation(), options, Clock, false, true));
		}
	}
}
