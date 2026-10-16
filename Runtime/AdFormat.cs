// ReSharper disable once CheckNamespace

namespace Geuneda.Ads
{
	/// <summary>전체 화면 광고 형식입니다. 0 은 쓰지 않습니다(채우지 않은 값이 실제 형식으로 보이지 않게)</summary>
	public enum AdFormat
	{
		/// <summary>보상형. 끝까지 보면 보상 콜백이 옵니다</summary>
		Rewarded = 1,

		/// <summary>전면. 보상이 없습니다</summary>
		Interstitial = 2
	}
}
