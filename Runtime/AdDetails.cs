// ReSharper disable once CheckNamespace

namespace Geuneda.Ads
{
	/// <summary>미디에이션 SDK 가 콜백에 담아 준 광고 정보입니다. 모르는 값은 null 입니다.</summary>
	public sealed class AdDetails
	{
		/// <summary>아무 정보도 없는 광고. 어댑터가 정보를 주지 않으면 서비스가 이 값을 씁니다</summary>
		public static readonly AdDetails Unknown = new AdDetails(null, null, null, null, null);

		/// <param name="unitId">광고 단위 ID</param>
		/// <param name="networkName">광고를 낸 네트워크</param>
		/// <param name="networkPlacement">네트워크 쪽 광고 위치 ID</param>
		/// <param name="creativeId">광고 소재 ID</param>
		/// <param name="identity">
		/// 불러온 광고 하나를 가리키는 값. 같은 광고의 로드·표시·닫힘 콜백에서 같아야 하고 다음에 불러온 광고와는 달라야 합니다.
		/// 서비스는 시간 초과로 끝낸 앞 광고의 늦은 콜백을 이 값으로 가려냅니다. 보장할 수 없으면 null
		/// </param>
		public AdDetails(string unitId, string networkName, string networkPlacement, string creativeId, string identity)
		{
			UnitId = Empty(unitId);
			NetworkName = Empty(networkName);
			NetworkPlacement = Empty(networkPlacement);
			CreativeId = Empty(creativeId);
			Identity = Empty(identity);
		}

		public string UnitId { get; }

		public string NetworkName { get; }

		public string NetworkPlacement { get; }

		public string CreativeId { get; }

		/// <summary>불러온 광고 하나를 가리키는 값. 모르면 null</summary>
		public string Identity { get; }

		public override string ToString()
		{
			return $"{NetworkName ?? "?"}/{CreativeId ?? "?"}";
		}

		private static string Empty(string value)
		{
			return string.IsNullOrEmpty(value) ? null : value;
		}
	}
}
