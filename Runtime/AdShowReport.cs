using System;
using Geuneda.Core;

// ReSharper disable once CheckNamespace

namespace Geuneda.Ads
{
	/// <summary>표시 요청 하나의 결과입니다(<see cref="IAdService.ShowSettled"/>). 거절한 요청을 포함해 요청마다 한 번 옵니다.</summary>
	public sealed class AdShowReport
	{
		internal AdShowReport(long showId, AdFormat format, string placement, Result result, AdSettleReason reason, bool displayed,
			TimeSpan elapsed, AdDetails ad, AdError? error)
		{
			ShowId = showId;
			Format = format;
			Placement = placement;
			Result = result;
			Reason = reason;
			Displayed = displayed;
			Elapsed = elapsed;
			Ad = ad;
			Error = error;
		}

		/// <summary>표시마다 1부터 늘어나는 번호. 표시 전에 거절한 요청은 0</summary>
		public long ShowId { get; }

		public AdFormat Format { get; }

		/// <summary>게임 안 광고 위치. 없으면 null</summary>
		public string Placement { get; }

		/// <summary>호출자가 받은 결과</summary>
		public Result Result { get; }

		public AdSettleReason Reason { get; }

		/// <summary>결과가 정해지기 전에 표시 콜백을 받았는지</summary>
		public bool Displayed { get; }

		/// <summary>표시 요청부터 결과까지 걸린 시간. 거절은 0</summary>
		public TimeSpan Elapsed { get; }

		/// <summary>표시한 광고. 모르면 <see cref="AdDetails.Unknown"/></summary>
		public AdDetails Ad { get; }

		/// <summary>SDK 가 알린 표시 실패. 없으면 null</summary>
		public AdError? Error { get; }
	}
}
