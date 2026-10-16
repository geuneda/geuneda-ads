using System;
using Geuneda.Core;

// ReSharper disable once CheckNamespace

namespace Geuneda.Ads
{
	public sealed partial class AdService
	{
		private void Subscribe()
		{
			_mediation.Initialized += OnMediationInitialized;
			_mediation.Loaded += OnLoaded;
			_mediation.LoadFailed += OnLoadFailed;
			_mediation.Displayed += OnDisplayed;
			_mediation.DisplayFailed += OnDisplayFailed;
			_mediation.Rewarded += OnRewarded;
			_mediation.Clicked += OnClicked;
			_mediation.Hidden += OnHidden;
			_mediation.RevenuePaid += OnRevenuePaid;
		}

		private void Unsubscribe()
		{
			_mediation.Initialized -= OnMediationInitialized;
			_mediation.Loaded -= OnLoaded;
			_mediation.LoadFailed -= OnLoadFailed;
			_mediation.Displayed -= OnDisplayed;
			_mediation.DisplayFailed -= OnDisplayFailed;
			_mediation.Rewarded -= OnRewarded;
			_mediation.Clicked -= OnClicked;
			_mediation.Hidden -= OnHidden;
			_mediation.RevenuePaid -= OnRevenuePaid;
		}

		private void OnMediationInitialized()
		{
			if (!IsMainThread)
			{
				MainThreadDispatcher.Post(OnMediationInitialized);
				return;
			}

			if (_disposed || _initialized)
			{
				return;
			}

			_initialized = true;
			Logger.Info($"{_mediation.Platform} initialized");
			RequestLoad(_rewarded);
			RequestLoad(_interstitial);
		}

		private void OnLoaded(AdFormat format, AdDetails ad)
		{
			if (!IsMainThread)
			{
				MainThreadDispatcher.Post(() => OnLoaded(format, ad));
				return;
			}

			if (_disposed || !TryGetLane(format, out var lane))
			{
				return;
			}

			lane.State = AdLoadState.Loaded;
			lane.Attempt = 0;
			lane.LoadedAd = ad ?? AdDetails.Unknown;
			Logger.Debug($"{format} ad loaded ({lane.LoadedAd})");
		}

		private void OnLoadFailed(AdFormat format, AdError error)
		{
			if (!IsMainThread)
			{
				MainThreadDispatcher.Post(() => OnLoadFailed(format, error));
				return;
			}

			if (_disposed || !TryGetLane(format, out var lane))
			{
				return;
			}

			if (lane.State != AdLoadState.Loading)
			{
				// 제한 시간이 지나 이미 실패로 본 로드가 재시도 전에 늦게 답했습니다. 재시도를 시작한 뒤에 온 앞 요청의 실패는
				// SDK 가 요청을 구분해 주지 않아(MAX 는 단위만 줌) 지금 요청의 실패로 셉니다
				Logger.Debug($"Ignoring a late {format} load failure ({error})");
				return;
			}

			FailLoad(lane, error, false);
		}

		private void OnDisplayed(AdFormat format, AdDetails ad)
		{
			if (!IsMainThread)
			{
				MainThreadDispatcher.Post(() => OnDisplayed(format, ad));
				return;
			}

			ad ??= AdDetails.Unknown;

			if (!TryRoute(format, ad, "displayed", out var session))
			{
				return;
			}

			if (!session.Settled)
			{
				session.Displayed = true;
				Remember(session, ad);
			}

			Notify(Displayed, new AdShowEvent(session.Id, format, session.Placement, ad, session.Settled), nameof(Displayed));
		}

		private void OnDisplayFailed(AdFormat format, AdError error, AdDetails ad)
		{
			if (!IsMainThread)
			{
				MainThreadDispatcher.Post(() => OnDisplayFailed(format, error, ad));
				return;
			}

			ad ??= AdDetails.Unknown;

			if (_disposed || !TryGetLane(format, out var lane))
			{
				return;
			}

			// 표시 실패한 광고는 다시 쓸 수 없어 새로 불러옵니다
			RequestLoad(lane);

			if (!TryRoute(format, ad, "display failed", out var session))
			{
				return;
			}

			if (session == _current && session.Displayed)
			{
				// 표시된 광고는 표시에 실패할 수 없습니다. 식별 값으로 가려내지 못한 앞 광고의 늦은 실패라 지금 광고를 끝내지 않습니다
				Logger.Warning($"Ignoring a {format} display failure ({error}) that arrived after ad #{session.Id} was displayed ({ad})");
				return;
			}

			session.Ended = true;

			if (session.Settled)
			{
				return;
			}

			session.Error = error;
			Remember(session, ad);
			Settle(session, session.Rewarded ? Result.Ok() : Result.Fail(AdsResultCodes.DisplayFailed, error.ToString()), AdSettleReason.DisplayFailed);
		}

		private void OnRewarded(AdFormat format, AdDetails ad)
		{
			if (!IsMainThread)
			{
				MainThreadDispatcher.Post(() => OnRewarded(format, ad));
				return;
			}

			ad ??= AdDetails.Unknown;

			if (!TryRoute(format, ad, "rewarded", out var session) || session.Format != AdFormat.Rewarded)
			{
				return;
			}

			if (session.Settled)
			{
				if (!session.Result.IsSuccess && !session.LateRewardRaised)
				{
					session.LateRewardRaised = true;
					Logger.Warning($"Rewarded ad #{session.Id} granted its reward after it ended with {session.Result}");
					Notify(LateRewarded, new AdLateReward(session.Id, session.Placement, ad, session.Result), nameof(LateRewarded));
				}

				return;
			}

			session.Rewarded = true;
			Remember(session, ad);

			if (session.Ended)
			{
				// 닫힘 뒤 보상 콜백을 기다리던 중이었습니다
				Settle(session, Result.Ok(), AdSettleReason.Closed);
			}
		}

		private void OnClicked(AdFormat format, AdDetails ad)
		{
			if (!IsMainThread)
			{
				MainThreadDispatcher.Post(() => OnClicked(format, ad));
				return;
			}

			ad ??= AdDetails.Unknown;

			// 네트워크에 따라 한 번의 노출에서 클릭 콜백이 수십 번 올 수 있어 표시마다 한 번만 알립니다
			if (!TryRoute(format, ad, "clicked", out var session) || session.ClickReported)
			{
				return;
			}

			session.ClickReported = true;
			Notify(Clicked, new AdShowEvent(session.Id, format, session.Placement, ad, session.Settled), nameof(Clicked));
		}

		private void OnHidden(AdFormat format, AdDetails ad)
		{
			if (!IsMainThread)
			{
				MainThreadDispatcher.Post(() => OnHidden(format, ad));
				return;
			}

			ad ??= AdDetails.Unknown;

			if (_disposed || !TryGetLane(format, out var lane))
			{
				return;
			}

			// 닫힌 광고는 다시 쓸 수 없어 다음 광고를 불러옵니다
			RequestLoad(lane);

			if (!TryRoute(format, ad, "hidden", out var session) || session.Ended)
			{
				return;
			}

			session.Ended = true;

			if (session.Settled)
			{
				return;
			}

			Remember(session, ad);

			if (session.Format == AdFormat.Interstitial || session.Rewarded)
			{
				Settle(session, Result.Ok(), AdSettleReason.Closed);
			}
			else if (_rewardGrace <= TimeSpan.Zero)
			{
				Settle(session, Result.Fail(AdsResultCodes.NotRewarded, "The ad was closed before the reward"), AdSettleReason.Closed);
			}
			else
			{
				// SDK 는 보상과 닫힘 콜백의 순서를 보장하지 않습니다. 시간 판정(TickSession)이 대기 뒤 끝냅니다
				session.HiddenAt = _clock.Elapsed;
			}
		}

		private void OnRevenuePaid(AdFormat format, AdDetails ad, AdPaid paid)
		{
			if (!IsMainThread)
			{
				MainThreadDispatcher.Post(() => OnRevenuePaid(format, ad, paid));
				return;
			}

			ad ??= AdDetails.Unknown;

			if (_disposed || !TryGetLane(format, out _))
			{
				return;
			}

			var amount = paid.Amount;

			if (amount.HasValue && (double.IsNaN(amount.Value) || double.IsInfinity(amount.Value) || amount.Value < 0))
			{
				Logger.Warning($"{format} ad revenue {amount.Value} is not a valid amount; reporting it as unknown");
				amount = null;
			}

			var session = Route(format, ad);
			Notify(RevenuePaid, new AdRevenue(session?.Id ?? 0, format, session?.Placement, _mediation.Platform, ad, amount, paid.Currency, paid.Precision),
				nameof(RevenuePaid));
		}

		private bool TryRoute(AdFormat format, AdDetails ad, string callback, out AdSession session)
		{
			session = null;

			if (_disposed || !TryGetLane(format, out _))
			{
				return false;
			}

			session = Route(format, ad);

			if (session == null)
			{
				Logger.Debug($"Ignoring {format} {callback} callback with no matching show ({ad})");
				return false;
			}

			return true;
		}

		/// <summary>
		/// 콜백이 속한 표시를 찾습니다. 식별 값이 있으면 지금 표시, 그다음 결과를 정한 최근 표시(최신부터)에서 같은 것을 찾습니다.
		/// 맞는 것이 없거나 식별 값을 모르면 지금 표시입니다(지금 광고의 콜백을 잃지 않는 쪽). 지금 표시가 없으면 가장 최근 표시이고,
		/// 식별 값이 둘 다 있는데 다르면 다른 광고라 버립니다.
		/// </summary>
		private AdSession Route(AdFormat format, AdDetails ad)
		{
			var current = _current != null && _current.Format == format ? _current : null;
			var identity = ad.Identity;

			if (identity != null)
			{
				if (current != null && identity == current.Identity)
				{
					return current;
				}

				for (var i = _settled.Count - 1; i >= 0; i--)
				{
					var settled = _settled[i];

					if (settled.Format == format && identity == settled.Identity)
					{
						return settled;
					}
				}
			}

			if (current != null)
			{
				return current;
			}

			for (var i = _settled.Count - 1; i >= 0; i--)
			{
				var latest = _settled[i];

				if (latest.Format == format)
				{
					return identity == null || latest.Identity == null ? latest : null;
				}
			}

			return null;
		}

		private bool TryGetLane(AdFormat format, out AdLoadLane lane)
		{
			switch (format)
			{
				case AdFormat.Rewarded:
					lane = _rewarded;
					return true;
				case AdFormat.Interstitial:
					lane = _interstitial;
					return true;
				default:
					Logger.Warning($"The mediation reported an unknown ad format {(int) format}");
					lane = null;
					return false;
			}
		}

		private static void Remember(AdSession session, AdDetails ad)
		{
			if (ad != AdDetails.Unknown)
			{
				session.LastAd = ad;
			}
		}
	}
}
