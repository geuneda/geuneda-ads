using System;

// ReSharper disable once CheckNamespace

namespace Geuneda.Ads
{
	/// <summary>광고 로드 실패와 다음 시도까지의 대기입니다(<see cref="IAdService.LoadFailed"/>).</summary>
	public sealed class AdLoadFailure
	{
		internal AdLoadFailure(AdFormat format, AdError? error, bool timedOut, int attempt, TimeSpan retryDelay)
		{
			Format = format;
			Error = error;
			TimedOut = timedOut;
			Attempt = attempt;
			RetryDelay = retryDelay;
		}

		public AdFormat Format { get; }

		/// <summary>SDK 가 알린 실패. 시간 초과면 null</summary>
		public AdError? Error { get; }

		/// <summary>로드 제한 시간(<see cref="AdServiceOptions.LoadTimeout"/>) 안에 SDK 가 답하지 않았는지</summary>
		public bool TimedOut { get; }

		/// <summary>성공 뒤 몇 번째 연속 실패인지(1부터)</summary>
		public int Attempt { get; }

		/// <summary>다시 불러오기 전까지 기다리는 시간</summary>
		public TimeSpan RetryDelay { get; }
	}
}
