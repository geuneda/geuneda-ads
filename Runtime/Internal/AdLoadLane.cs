using System;

// ReSharper disable once CheckNamespace

namespace Geuneda.Ads
{
	internal enum AdLoadState
	{
		/// <summary>불러온 광고가 없고 불러오는 중도 아님(처음, 또는 표시로 광고를 썼음)</summary>
		Idle = 1,

		Loading = 2,

		Loaded = 3,

		/// <summary>실패 뒤 다시 불러오기를 기다림</summary>
		Waiting = 4
	}

	/// <summary>형식 하나의 로드 상태입니다.</summary>
	internal sealed class AdLoadLane
	{
		public AdLoadLane(AdFormat format)
		{
			Format = format;
		}

		public AdFormat Format { get; }

		public AdLoadState State { get; set; } = AdLoadState.Idle;

		/// <summary>성공 뒤 연속 실패 수</summary>
		public int Attempt { get; set; }

		public TimeSpan LoadStartedAt { get; set; }

		public TimeSpan RetryAt { get; set; }

		/// <summary>불러와 있는 광고. 없으면 null</summary>
		public AdDetails LoadedAd { get; set; }
	}
}
