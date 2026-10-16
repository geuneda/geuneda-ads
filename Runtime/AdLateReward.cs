using Geuneda.Core;

// ReSharper disable once CheckNamespace

namespace Geuneda.Ads
{
	/// <summary>
	/// 결과를 실패로 정한 뒤에 온 보상입니다(<see cref="IAdService.LateRewarded"/>). 호출자는 이미 실패(대개 <see cref="ResultCodes.Timeout"/>)를 받았으므로
	/// 지급할지는 게임이 정합니다. 표시마다 많아야 한 번입니다.
	/// </summary>
	public sealed class AdLateReward
	{
		internal AdLateReward(long showId, string placement, AdDetails ad, Result settledResult)
		{
			ShowId = showId;
			Placement = placement;
			Ad = ad;
			SettledResult = settledResult;
		}

		/// <summary>표시 번호(<see cref="AdShowReport.ShowId"/>)</summary>
		public long ShowId { get; }

		/// <summary>게임 안 광고 위치. 없으면 null</summary>
		public string Placement { get; }

		public AdDetails Ad { get; }

		/// <summary>호출자가 받았던 결과</summary>
		public Result SettledResult { get; }
	}
}
