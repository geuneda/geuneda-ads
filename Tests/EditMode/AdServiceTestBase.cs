using System;
using System.Collections.Generic;
using Geuneda.Core;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace Geuneda.Ads.Tests
{
	/// <summary>
	/// 가짜 미디에이션·시계로 서비스를 만듭니다. 수명 주기 컴포넌트 없이 테스트가 <see cref="Tick"/> 과 일시정지·포커스를 직접 넘깁니다.
	/// 기본은 광고 중에도 앱이 도는 미디에이션입니다(<see cref="CreatePausingApp"/> 는 MAX 처럼 표시 중 앱을 멈추는 미디에이션).
	/// <see cref="Advance(TimeSpan)"/> 는 0.5초 프레임마다 Tick 하고, <see cref="FreezeFrames"/> 는 플레이어 루프가 멈춘 것처럼 Tick 없이 시간만 보냅니다.
	/// </summary>
	public abstract class AdServiceTestBase
	{
		protected static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);
		protected static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);
		protected static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(60);
		protected static readonly TimeSpan ForegroundGrace = TimeSpan.FromSeconds(20);
		protected static readonly TimeSpan AbsoluteTimeout = TimeSpan.FromSeconds(300);
		protected static readonly TimeSpan RewardGrace = TimeSpan.FromSeconds(2);

		protected FakeMediation Mediation;
		protected FakeMonotonicClock Clock;
		protected AdService Service;
		protected List<AdShowReport> Reports;
		protected List<AdLateReward> LateRewards;
		protected List<AdRevenue> Revenues;
		protected List<AdLoadFailure> LoadFailures;
		protected List<AdShowEvent> DisplayedEvents;
		protected List<AdShowEvent> ClickedEvents;
		protected List<bool> Showing;

		[SetUp]
		public void SetUpService()
		{
			Mediation = new FakeMediation();
			Clock = new FakeMonotonicClock();
			Service = Create();
		}

		[TearDown]
		public void TearDownService()
		{
			Service?.Dispose();
		}

		protected static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(500);

		protected AdService Create(Action<AdServiceOptions> configure = null)
		{
			var options = new AdServiceOptions
			{
				RetryDelay = RetryDelay,
				MaxRetryDelay = MaxRetryDelay,
				LoadTimeout = LoadTimeout,
				ForegroundGrace = ForegroundGrace,
				AbsoluteTimeout = AbsoluteTimeout,
				RewardGrace = RewardGrace
			};

			configure?.Invoke(options);
			var service = new AdService(Mediation, options, Clock, false, true);
			Reports = new List<AdShowReport>();
			LateRewards = new List<AdLateReward>();
			Revenues = new List<AdRevenue>();
			LoadFailures = new List<AdLoadFailure>();
			DisplayedEvents = new List<AdShowEvent>();
			ClickedEvents = new List<AdShowEvent>();
			Showing = new List<bool>();
			service.ShowSettled += Reports.Add;
			service.LateRewarded += LateRewards.Add;
			service.RevenuePaid += Revenues.Add;
			service.LoadFailed += LoadFailures.Add;
			service.Displayed += DisplayedEvents.Add;
			service.Clicked += ClickedEvents.Add;
			service.ShowingChanged += Showing.Add;
			return service;
		}

		/// <summary>표시 중 앱을 멈추는 미디에이션(MAX 의 Android·iOS)의 서비스로 바꿉니다</summary>
		protected void CreatePausingApp(Action<AdServiceOptions> configure = null)
		{
			Service.Dispose();
			Mediation = new FakeMediation { PausesApp = true };
			Service = Create(configure);
		}

		protected static AdDetails Ad(string identity, string network = "applovin")
		{
			return new AdDetails("unit", network, "network-placement", "creative-" + identity, identity);
		}

		/// <summary>프레임(0.5초)마다 Tick 하며 시간을 보냅니다</summary>
		protected void Advance(TimeSpan time)
		{
			while (time > TimeSpan.Zero)
			{
				var step = time < Frame ? time : Frame;
				Clock.Elapsed += step;
				time -= step;
				Service.Tick();
			}
		}

		/// <summary>프레임마다 <paramref name="frameSeconds"/> 가 걸리는 느린 프레임으로 <paramref name="frames"/> 번 Tick 합니다</summary>
		protected void SlowFrames(double frameSeconds, int frames)
		{
			for (var i = 0; i < frames; i++)
			{
				Clock.Elapsed += TimeSpan.FromSeconds(frameSeconds);
				Service.Tick();
			}
		}

		/// <summary>플레이어 루프가 멈춘 것처럼 Tick 도, 일시정지 메시지도 없이 시간만 보낸 뒤 루프가 다시 돈 첫 프레임을 Tick 합니다(MAX iOS 의 UnityPause)</summary>
		protected void FreezeFrames(double seconds)
		{
			Clock.Elapsed += TimeSpan.FromSeconds(seconds);
			Service.Tick();
		}

		protected void Advance(double seconds)
		{
			Advance(TimeSpan.FromSeconds(seconds));
		}

		protected void Tick()
		{
			Service.Tick();
		}

		/// <summary>광고를 불러온 상태로 만듭니다(초기화 때 요청한 로드에 성공을 답함)</summary>
		protected void LoadReady(AdFormat format, AdDetails ad = null)
		{
			Mediation.RaiseLoaded(format, ad);
		}

		protected Captured<Result> ShowRewarded(string placement = "revive")
		{
			return Captured<Result>.Of(Service.ShowRewardedAsync(placement));
		}

		protected Captured<Result> ShowInterstitial(string placement = "stage_end")
		{
			return Captured<Result>.Of(Service.ShowInterstitialAsync(placement));
		}

		/// <summary>Android 처럼 광고 동안 앱이 멈췄다가(그동안 프레임 없음) 돌아온 뒤 첫 프레임을 Tick 합니다</summary>
		protected void PauseAndResume(double pausedSeconds)
		{
			Service.HandleFocus(false);
			Service.HandlePause(true);
			Clock.Elapsed += TimeSpan.FromSeconds(pausedSeconds);
			Service.HandlePause(false);
			Service.HandleFocus(true);
			Service.Tick();
		}
	}
}
