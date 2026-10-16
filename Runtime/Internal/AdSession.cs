using System;
using Cysharp.Threading.Tasks;
using Geuneda.Core;

// ReSharper disable once CheckNamespace

namespace Geuneda.Ads
{
	/// <summary>표시 요청 하나입니다. 결과(<see cref="Settled"/>)는 한 번만 정하고, 정한 뒤에도 늦은 콜백을 받으려고 서비스가 잠시 들고 있습니다.</summary>
	internal sealed class AdSession
	{
		public AdSession(long id, AdFormat format, string placement, AdDetails ad, TimeSpan shownAt)
		{
			Id = id;
			Format = format;
			Placement = placement;
			Ad = ad;
			LastAd = ad;
			ShownAt = shownAt;
		}

		public long Id { get; }

		public AdFormat Format { get; }

		public string Placement { get; }

		/// <summary>표시 요청 때 불러와 있던 광고. 늦은 콜백을 가려내는 식별 값(<see cref="AdDetails.Identity"/>)을 씁니다</summary>
		public AdDetails Ad { get; }

		/// <summary>마지막으로 받은 콜백의 광고 정보(결과 보고에 씀)</summary>
		public AdDetails LastAd { get; set; }

		public TimeSpan ShownAt { get; }

		public UniTaskCompletionSource<Result> Completion { get; } = new UniTaskCompletionSource<Result>();

		public bool Displayed { get; set; }

		public bool Rewarded { get; set; }

		/// <summary>닫힘이나 표시 실패 콜백을 받았는지</summary>
		public bool Ended { get; set; }

		/// <summary>보상 없이 닫힌 시각. 보상 콜백을 기다리는 중이면 값이 있습니다</summary>
		public TimeSpan? HiddenAt { get; set; }

		public AdError? Error { get; set; }

		/// <summary>프레임 간격 직후 시간 기준 확정 조건이 처음 보인 시각. 확인 중이 아니면 null</summary>
		public TimeSpan? TimedOutSince { get; set; }

		/// <summary>확인을 시작한 뒤 지난 프레임 수</summary>
		public int TimedOutFrames { get; set; }

		public bool Settled { get; private set; }

		public Result Result { get; private set; }

		public bool LateRewardRaised { get; set; }

		public bool ClickReported { get; set; }

		public string Identity => Ad.Identity;

		/// <summary>결과를 정합니다. 이미 정했으면 false</summary>
		public bool TrySettle(Result result)
		{
			if (Settled)
			{
				return false;
			}

			Settled = true;
			Result = result;
			return true;
		}
	}
}
