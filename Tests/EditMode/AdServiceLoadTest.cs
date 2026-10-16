using System;
using Geuneda.Core;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace Geuneda.Ads.Tests
{
	public sealed class AdServiceLoadTest : AdServiceTestBase
	{
		[Test]
		public void InitializedMediation_LoadsEveryAvailableFormatOnce()
		{
			Assert.That(Service.IsInitialized, Is.True);
			Assert.That(Mediation.Loads, Is.EquivalentTo(new[] { AdFormat.Rewarded, AdFormat.Interstitial }));

			Advance(1);

			Assert.That(Mediation.Loads, Has.Count.EqualTo(2), "a pending load is not requested again");
		}

		[Test]
		public void LoadsStartWhenTheMediationFinishesInitializing_AndSkipUnavailableFormats()
		{
			Service.Dispose();
			Mediation = new FakeMediation { IsInitialized = false };
			Mediation.Available.Remove(AdFormat.Interstitial);
			Service = Create();

			Assert.That(Service.IsInitialized, Is.False);
			Assert.That(Mediation.Loads, Is.Empty);

			Mediation.IsInitialized = true;
			Mediation.RaiseInitialized();
			Mediation.RaiseInitialized();

			Assert.That(Service.IsInitialized, Is.True);
			Assert.That(Mediation.Loads, Is.EqualTo(new[] { AdFormat.Rewarded }));
		}

		[Test]
		public void Show_CatchesUpWhenTheInitializedEventWasMissed()
		{
			Service.Dispose();
			Mediation = new FakeMediation { IsInitialized = false };
			Service = Create();
			Mediation.IsInitialized = true;

			var result = ShowRewarded();

			Assert.That(result.Value.Code, Is.EqualTo(AdsResultCodes.NotReady));
			Assert.That(Service.IsInitialized, Is.True);
			Assert.That(Mediation.LoadCount(AdFormat.Rewarded), Is.EqualTo(1));
		}

		[Test]
		public void LoadFailures_BackOffFromTwoSecondsDoublingUpToThirty_AndSuccessResets()
		{
			LoadReady(AdFormat.Interstitial);
			var expected = new[] { 2, 4, 8, 16, 30, 30 };

			for (var i = 0; i < expected.Length; i++)
			{
				Mediation.RaiseLoadFailed(AdFormat.Rewarded);
				Assert.That(LoadFailures[i].RetryDelay, Is.EqualTo(TimeSpan.FromSeconds(expected[i])));
				Assert.That(LoadFailures[i].Attempt, Is.EqualTo(i + 1));
				Assert.That(LoadFailures[i].Error?.Code, Is.EqualTo(204));

				var loads = Mediation.LoadCount(AdFormat.Rewarded);
				Advance(expected[i] - 0.5);
				Assert.That(Mediation.LoadCount(AdFormat.Rewarded), Is.EqualTo(loads), "waits the whole delay");
				Advance(0.5);
				Assert.That(Mediation.LoadCount(AdFormat.Rewarded), Is.EqualTo(loads + 1));
			}

			LoadReady(AdFormat.Rewarded);
			Mediation.Ready.Remove(AdFormat.Rewarded);
			ShowRewarded();
			Mediation.RaiseLoadFailed(AdFormat.Rewarded);

			Assert.That(LoadFailures[expected.Length].RetryDelay, Is.EqualTo(RetryDelay), "a success resets the backoff");
		}

		[Test]
		public void ShowWhileWaitingToRetry_DoesNotSkipTheBackoff()
		{
			Mediation.RaiseLoadFailed(AdFormat.Rewarded);
			var loads = Mediation.LoadCount(AdFormat.Rewarded);

			var result = ShowRewarded();

			Assert.That(result.Value.Code, Is.EqualTo(AdsResultCodes.NotReady));
			Assert.That(Mediation.LoadCount(AdFormat.Rewarded), Is.EqualTo(loads));
		}

		[Test]
		public void ShowWhenTheSdkDroppedTheLoadedAd_LoadsAgain()
		{
			LoadReady(AdFormat.Rewarded);
			Mediation.Ready.Remove(AdFormat.Rewarded);

			var result = ShowRewarded();

			Assert.That(result.Value.Code, Is.EqualTo(AdsResultCodes.NotReady));
			Assert.That(Mediation.LoadCount(AdFormat.Rewarded), Is.EqualTo(2));
		}

		[Test]
		public void LoadWithNoAnswer_TimesOutAndRetries_AndALateFailureIsIgnored()
		{
			Advance(LoadTimeout - TimeSpan.FromSeconds(1));
			Assert.That(LoadFailures, Is.Empty);

			Advance(1);

			Assert.That(LoadFailures, Has.Count.EqualTo(2), "both formats were loading");
			Assert.That(LoadFailures[0].TimedOut, Is.True);
			Assert.That(LoadFailures[0].Error, Is.Null);

			Mediation.RaiseLoadFailed(AdFormat.Rewarded);
			Assert.That(LoadFailures, Has.Count.EqualTo(2), "the answer to the abandoned load does not count twice");

			// 포기한 로드가 늦게 성공하면 받아들이고 재시도하지 않습니다
			LoadReady(AdFormat.Interstitial);
			Advance(RetryDelay);

			Assert.That(Service.IsReady(AdFormat.Interstitial), Is.True);
			Assert.That(Mediation.LoadCount(AdFormat.Interstitial), Is.EqualTo(1));
			Assert.That(Mediation.LoadCount(AdFormat.Rewarded), Is.EqualTo(2));
		}

		[Test]
		public void LoadThatThrows_IsLoggedAndRetriedAfterTheBackoff()
		{
			Service.Dispose();
			Mediation = new FakeMediation { IsInitialized = false, LoadThrows = new InvalidOperationException("adapter bug") };
			Mediation.Available.Remove(AdFormat.Interstitial);
			Service = Create();
			UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(@"Load\(Rewarded\) threw"));
			UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Exception, new System.Text.RegularExpressions.Regex("adapter bug"));

			Mediation.IsInitialized = true;
			Mediation.RaiseInitialized();

			Assert.That(LoadFailures, Has.Count.EqualTo(1));
			Assert.That(LoadFailures[0].Error?.Message, Does.Contain("adapter bug"));
			Mediation.LoadThrows = null;
			Advance(RetryDelay);
			Assert.That(Mediation.LoadCount(AdFormat.Rewarded), Is.EqualTo(2));
		}

		[Test]
		public void Hidden_LoadsTheNextAd()
		{
			LoadReady(AdFormat.Rewarded);
			ShowRewarded();
			Mediation.RaiseDisplayed(AdFormat.Rewarded);
			Mediation.RaiseRewarded(AdFormat.Rewarded);

			Mediation.RaiseHidden(AdFormat.Rewarded);

			Assert.That(Mediation.LoadCount(AdFormat.Rewarded), Is.EqualTo(2));
		}

		[Test]
		public void DisplayFailed_LoadsTheNextAd()
		{
			LoadReady(AdFormat.Interstitial);
			ShowInterstitial();

			Mediation.RaiseDisplayFailed(AdFormat.Interstitial);

			Assert.That(Mediation.LoadCount(AdFormat.Interstitial), Is.EqualTo(2));
		}

		[Test]
		public void IsReady_FollowsTheMediation()
		{
			Assert.That(Service.IsReady(AdFormat.Rewarded), Is.False);

			LoadReady(AdFormat.Rewarded);

			Assert.That(Service.IsReady(AdFormat.Rewarded), Is.True);
			Assert.That(Service.IsReady(AdFormat.Interstitial), Is.False);
			Assert.Throws<ArgumentOutOfRangeException>(() => Service.IsReady((AdFormat) 0));
		}

		[Test]
		public void UnknownFormatFromTheMediation_IsIgnoredWithAWarning()
		{
			UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("unknown ad format 9"));

			Mediation.RaiseLoaded((AdFormat) 9);

			Assert.That(Service.IsReady(AdFormat.Rewarded), Is.False);
		}
	}
}
