using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Geuneda.Core;

// ReSharper disable once CheckNamespace

namespace Geuneda.Ads.Tests
{
	public sealed class FakeMonotonicClock : IMonotonicClock
	{
		public TimeSpan Elapsed { get; set; } = TimeSpan.FromHours(1);
	}

	/// <summary>호출을 기록하고 테스트가 콜백을 직접 올리는 미디에이션입니다. 기본은 두 형식 모두 단위가 있고, 초기화됐고, 준비되지 않았습니다.</summary>
	public sealed class FakeMediation : IAdMediation
	{
		public readonly HashSet<AdFormat> Available = new HashSet<AdFormat> { AdFormat.Rewarded, AdFormat.Interstitial };
		public readonly HashSet<AdFormat> Ready = new HashSet<AdFormat>();
		public readonly List<AdFormat> Loads = new List<AdFormat>();
		public readonly List<(AdFormat Format, string Placement)> Shows = new List<(AdFormat, string)>();

		public string Platform { get; set; } = "FakeMax";

		public bool IsInitialized { get; set; } = true;

		public bool PausesApp { get; set; }

		/// <summary>표시 호출 안에서 할 일(바로 콜백을 올리는 SDK 흉내). 기본은 준비 표시만 지움</summary>
		public Action<AdFormat, string> OnShow { get; set; }

		public Exception ShowThrows { get; set; }

		public Exception LoadThrows { get; set; }

		public event Action Initialized;

		public event Action<AdFormat, AdDetails> Loaded;

		public event Action<AdFormat, AdError> LoadFailed;

		public event Action<AdFormat, AdDetails> Displayed;

		public event Action<AdFormat, AdError, AdDetails> DisplayFailed;

		public event Action<AdFormat, AdDetails> Rewarded;

		public event Action<AdFormat, AdDetails> Clicked;

		public event Action<AdFormat, AdDetails> Hidden;

		public event Action<AdFormat, AdDetails, AdPaid> RevenuePaid;

		public bool HasSubscribers => Initialized != null || Loaded != null || LoadFailed != null || Displayed != null || DisplayFailed != null ||
		                              Rewarded != null || Clicked != null || Hidden != null || RevenuePaid != null;

		public int LoadCount(AdFormat format)
		{
			return Loads.FindAll(f => f == format).Count;
		}

		public bool IsAvailable(AdFormat format)
		{
			return Available.Contains(format);
		}

		public bool IsReady(AdFormat format)
		{
			return Ready.Contains(format);
		}

		public void Load(AdFormat format)
		{
			Loads.Add(format);

			if (LoadThrows != null)
			{
				throw LoadThrows;
			}
		}

		public void Show(AdFormat format, string placement)
		{
			Shows.Add((format, placement));

			if (ShowThrows != null)
			{
				throw ShowThrows;
			}

			Ready.Remove(format);
			OnShow?.Invoke(format, placement);
		}

		public void RaiseInitialized()
		{
			Initialized?.Invoke();
		}

		/// <summary>로드 성공을 올리고 준비 상태로 둡니다</summary>
		public void RaiseLoaded(AdFormat format, AdDetails ad = null)
		{
			Ready.Add(format);
			Loaded?.Invoke(format, ad);
		}

		public void RaiseLoadFailed(AdFormat format, int code = 204, string message = "No fill")
		{
			LoadFailed?.Invoke(format, new AdError(code, message));
		}

		public void RaiseDisplayed(AdFormat format, AdDetails ad = null)
		{
			Displayed?.Invoke(format, ad);
		}

		public void RaiseDisplayFailed(AdFormat format, AdDetails ad = null, int code = -23, string message = "Already showing")
		{
			DisplayFailed?.Invoke(format, new AdError(code, message), ad);
		}

		public void RaiseRewarded(AdFormat format, AdDetails ad = null)
		{
			Rewarded?.Invoke(format, ad);
		}

		public void RaiseClicked(AdFormat format, AdDetails ad = null)
		{
			Clicked?.Invoke(format, ad);
		}

		public void RaiseHidden(AdFormat format, AdDetails ad = null)
		{
			Hidden?.Invoke(format, ad);
		}

		public void RaiseRevenue(AdFormat format, AdDetails ad, double? amount, string currency = "USD", string precision = "exact")
		{
			RevenuePaid?.Invoke(format, ad, new AdPaid(amount, currency, precision));
		}
	}

	public sealed class Captured<T>
	{
		public bool Done { get; private set; }

		public T Value { get; private set; }

		public Exception Error { get; private set; }

		public static Captured<T> Of(UniTask<T> task)
		{
			var captured = new Captured<T>();
			captured.RunAsync(task).Forget();
			return captured;
		}

		private async UniTaskVoid RunAsync(UniTask<T> task)
		{
			try
			{
				Value = await task;
			}
			catch (Exception exception)
			{
				Error = exception;
			}

			Done = true;
		}
	}
}
