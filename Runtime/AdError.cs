// ReSharper disable once CheckNamespace

namespace Geuneda.Ads
{
	/// <summary>미디에이션 SDK 가 알린 로드·표시 실패입니다. 코드는 SDK 의 값 그대로입니다(MAX 는 <c>MaxSdkBase.ErrorCode</c>).</summary>
	public readonly struct AdError
	{
		public AdError(int code, string message)
		{
			Code = code;
			Message = string.IsNullOrEmpty(message) ? null : message;
		}

		public int Code { get; }

		public string Message { get; }

		public override string ToString()
		{
			return Message == null ? Code.ToString() : $"{Code} {Message}";
		}
	}
}
