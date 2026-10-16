using System;

// ReSharper disable once CheckNamespace

namespace Geuneda.Ads
{
	public sealed partial class AdService
	{
		/// <summary>불러온 광고가 없고 불러오는 중도, 재시도를 기다리는 중도 아니면 불러옵니다.</summary>
		private void RequestLoad(AdLoadLane lane)
		{
			if (_disposed || !_initialized || !_mediation.IsAvailable(lane.Format))
			{
				return;
			}

			switch (lane.State)
			{
				case AdLoadState.Loading:
				case AdLoadState.Waiting:
					return;
				case AdLoadState.Loaded when _mediation.IsReady(lane.Format):
					return;
			}

			StartLoad(lane);
		}

		private void StartLoad(AdLoadLane lane)
		{
			if (_disposed)
			{
				return;
			}

			lane.State = AdLoadState.Loading;
			lane.LoadStartedAt = _clock.Elapsed;
			lane.LoadedAd = null;
			Logger.Debug($"Loading {lane.Format} ad");

			try
			{
				_mediation.Load(lane.Format);
			}
			catch (Exception exception)
			{
				// 어댑터 버그입니다. 로그로 드러내고 재시도 대기로 넘겨 로드가 멈추지 않게 합니다
				Logger.Error(exception, $"Mediation Load({lane.Format}) threw");

				if (lane.State == AdLoadState.Loading)
				{
					FailLoad(lane, new AdError(-1, $"Load threw {exception.GetType().Name}: {exception.Message}"), false);
				}
			}
		}

		private void FailLoad(AdLoadLane lane, AdError? error, bool timedOut)
		{
			lane.Attempt++;
			var delay = RetryDelayFor(lane.Attempt);
			lane.State = AdLoadState.Waiting;
			lane.RetryAt = _clock.Elapsed + delay;
			lane.LoadedAd = null;
			Logger.Info($"{lane.Format} ad load failed ({(timedOut ? "timed out" : error.ToString())}), retry {lane.Attempt} in {delay.TotalSeconds:F0}s");
			Notify(LoadFailed, new AdLoadFailure(lane.Format, error, timedOut, lane.Attempt, delay), nameof(LoadFailed));
		}

		private void TickLane(AdLoadLane lane, TimeSpan now)
		{
			if (lane.State == AdLoadState.Waiting && now >= lane.RetryAt)
			{
				StartLoad(lane);
			}
			else if (lane.State == AdLoadState.Loading && now - lane.LoadStartedAt >= _loadTimeout)
			{
				Logger.Warning($"{lane.Format} ad load got no answer in {_loadTimeout.TotalSeconds:F0}s");
				FailLoad(lane, null, true);
			}
		}

		private TimeSpan RetryDelayFor(int attempt)
		{
			var delay = _retryDelay;

			for (var i = 1; i < attempt && delay < _maxRetryDelay; i++)
			{
				delay += delay;
			}

			return delay > _maxRetryDelay ? _maxRetryDelay : delay;
		}
	}
}
