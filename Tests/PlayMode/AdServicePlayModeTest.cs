using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace Geuneda.Ads.Tests.PlayMode
{
	public sealed class AdServicePlayModeTest
	{
		private PlayModeMediation _mediation;
		private AdService _service;

		[SetUp]
		public void SetUp()
		{
			_mediation = new PlayModeMediation();
			_service = new AdService(_mediation, new AdServiceOptions { RetryDelay = TimeSpan.FromMilliseconds(100) });
		}

		[TearDown]
		public void TearDown()
		{
			_service?.Dispose();
		}

		[UnityTest]
		public IEnumerator PublicConstructor_TicksFromAHiddenLifecycleObject()
		{
			var lifecycle = _service.LifecycleObject;
			Assert.That(lifecycle, Is.Not.Null);
			Assert.That(lifecycle.hideFlags, Is.EqualTo(HideFlags.HideInHierarchy));
			Assert.That(_mediation.Loads, Has.Count.EqualTo(2));

			_mediation.RaiseLoadFailed(AdFormat.Rewarded);
			var until = Time.realtimeSinceStartup + 3;

			while (_mediation.Loads.Count < 3 && Time.realtimeSinceStartup < until)
			{
				yield return null;
			}

			Assert.That(_mediation.Loads, Has.Count.EqualTo(3), "the retry ran from Update");

			_service.Dispose();
			yield return null;
			Assert.That(lifecycle == null, Is.True);
		}

		[UnityTest]
		public IEnumerator CallbacksFromAnotherThread_AreHandledOnTheMainThread()
		{
			var displayed = new List<AdShowEvent>();
			_service.Displayed += displayed.Add;
			_mediation.RaiseLoaded(AdFormat.Rewarded);
			_service.ShowRewardedAsync("revive");

			Task.Run(() => _mediation.RaiseDisplayed(AdFormat.Rewarded)).Wait();

			Assert.That(displayed, Is.Empty, "not handled on the SDK thread");

			yield return null;
			yield return null;

			Assert.That(displayed, Has.Count.EqualTo(1));
			Assert.That(displayed[0].Placement, Is.EqualTo("revive"));
		}

		private sealed class PlayModeMediation : IAdMediation
		{
			private readonly HashSet<AdFormat> _ready = new HashSet<AdFormat>();

			public readonly List<AdFormat> Loads = new List<AdFormat>();

			public string Platform => "PlayModeFake";

			public bool IsInitialized => true;

			public bool PausesApp => false;

			public event Action Initialized;

			public event Action<AdFormat, AdDetails> Loaded;

			public event Action<AdFormat, AdError> LoadFailed;

			public event Action<AdFormat, AdDetails> Displayed;

			public event Action<AdFormat, AdError, AdDetails> DisplayFailed;

			public event Action<AdFormat, AdDetails> Rewarded;

			public event Action<AdFormat, AdDetails> Clicked;

			public event Action<AdFormat, AdDetails> Hidden;

			public event Action<AdFormat, AdDetails, AdPaid> RevenuePaid;

			public bool IsAvailable(AdFormat format)
			{
				return true;
			}

			public bool IsReady(AdFormat format)
			{
				return _ready.Contains(format);
			}

			public void Load(AdFormat format)
			{
				Loads.Add(format);
			}

			public void Show(AdFormat format, string placement)
			{
				_ready.Remove(format);
			}

			public void RaiseLoaded(AdFormat format)
			{
				_ready.Add(format);
				Loaded?.Invoke(format, null);
			}

			public void RaiseLoadFailed(AdFormat format)
			{
				LoadFailed?.Invoke(format, new AdError(204, "No fill"));
			}

			public void RaiseDisplayed(AdFormat format)
			{
				Displayed?.Invoke(format, null);
			}

			// 계약의 나머지 이벤트는 이 테스트에서 쓰지 않습니다
			public void Unused()
			{
				Initialized?.Invoke();
				DisplayFailed?.Invoke(AdFormat.Rewarded, default, null);
				Rewarded?.Invoke(AdFormat.Rewarded, null);
				Clicked?.Invoke(AdFormat.Rewarded, null);
				Hidden?.Invoke(AdFormat.Rewarded, null);
				RevenuePaid?.Invoke(AdFormat.Rewarded, null, default);
			}
		}
	}
}
