using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Ads
{
	/// <summary><see cref="AdService"/> 가 플레이 중에 만드는 숨은 컴포넌트입니다. 매 프레임 시간 판정을 돌리고 앱 일시정지·포커스를 넘깁니다.</summary>
	[AddComponentMenu("")]
	internal sealed class AdLifecycle : MonoBehaviour
	{
		private AdService _service;

		public void Bind(AdService service)
		{
			_service = service;
		}

		public void Unbind()
		{
			_service = null;
		}

		private void Update()
		{
			_service?.Tick();
		}

		private void OnApplicationPause(bool paused)
		{
			_service?.HandlePause(paused);
		}

		private void OnApplicationFocus(bool focused)
		{
			_service?.HandleFocus(focused);
		}
	}
}
