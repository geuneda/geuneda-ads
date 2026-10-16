using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Geuneda.Core;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable once CheckNamespace

namespace Geuneda.Ads
{
	/// <summary>
	/// <see cref="IAdService"/> 의 기본 구현입니다. 미디에이션 어댑터(<see cref="IAdMediation"/>)를 받아 광고를 미리 불러 두고, 표시 요청마다 결과를 한 번만 정합니다.
	/// </summary>
	/// <remarks>
	/// <para>
	/// 결과 판정: 보상형은 보상 콜백을 받고 닫히면 성공입니다. 보상 전에 닫히면 <see cref="AdServiceOptions.RewardGrace"/>(2초) 동안 보상 콜백을 기다립니다(SDK 는 순서를 보장하지 않음).
	/// 닫힘 콜백이 오지 않으면 앱이 앞에 있는 시간으로 판정해 시간 초과로 끝냅니다(표시 콜백 없음, 앱을 멈추는 미디에이션에서 복귀 뒤 콜백 없음, 절대 상한). 시간 초과 전에 보상을 받았으면 성공입니다.
	/// 결과를 실패로 정한 뒤 온 보상은 <see cref="LateRewarded"/> 로 넘깁니다.
	/// </para>
	/// <para>
	/// 늦은 콜백: 결과를 정한 표시를 최근 몇 개 들고 있다가, 콜백의 광고 식별 값(<see cref="AdDetails.Identity"/>)이 지금 표시와 다르고 그중 하나와 같으면 그 표시의 것으로 봅니다.
	/// 식별 값을 모르거나 아무것과도 맞지 않으면 지금 표시 중인 광고의 것으로 봅니다(지금 표시가 없으면 가장 최근 표시, 단 식별 값이 둘 다 있는데 다르면 버림).
	/// 이미 표시된 광고에 온 표시 실패는 그 광고의 실패로 보지 않습니다.
	/// </para>
	/// <para>
	/// 로드: SDK 초기화가 끝나면 설정된 형식을 모두 불러오고, 닫힘·표시 실패·시간 초과 뒤 다시 불러옵니다. 실패하면 <see cref="AdServiceOptions.RetryDelay"/>(2초)부터 두 배씩
	/// <see cref="AdServiceOptions.MaxRetryDelay"/>(30초)까지 기다립니다. 기다리는 동안의 표시 요청은 대기를 건너뛰지 않고 <see cref="AdsResultCodes.NotReady"/> 입니다.
	/// </para>
	/// </remarks>
	public sealed partial class AdService : IAdService
	{
		private static readonly ModuleLogger Logger = new ModuleLogger("Geuneda.Ads");

		private static readonly TimeSpan MaxDelaySetting = TimeSpan.FromDays(1);

		/// <summary>
		/// 이만큼 프레임이 돌지 않았으면 플레이어 루프가 멈췄다가(광고 SDK 의 UnityPause 등) 돌아왔을 수 있다고 봅니다. 앱을 멈추는 미디에이션의 복귀 뒤 닫힘 대기를
		/// 그때부터 세고, 그 뒤 <see cref="ResumeSettle"/> 동안의 시간 기준 확정은 확인을 거칩니다. 앱이 앞에 온 시각(일시정지·포커스 메시지)은 바꾸지 않아
		/// 느린 프레임이 시간 초과를 끝없이 미루지 않습니다
		/// </summary>
		internal static readonly TimeSpan FrameGap = TimeSpan.FromSeconds(1);

		/// <summary>
		/// 프레임 간격 뒤 이 시간 안에 시간 기준 확정(시간 초과, 보상 없음) 조건이 생기면 바로 끝내지 않고, 조건이 이만큼 그리고 <see cref="ResumeSettleFrames"/> 프레임 더
		/// 이어질 때 끝냅니다. 멈췄던 동안 SDK 가 보낸 콜백이 복귀 뒤 늦게 큐에 들어와도 먼저 끝내지 않게 합니다. 확인을 시작한 시각은 그 뒤의 간격으로 다시 잡지 않습니다
		/// </summary>
		internal static readonly TimeSpan ResumeSettle = TimeSpan.FromSeconds(1);

		internal const int ResumeSettleFrames = 2;

		/// <summary>늦은 콜백을 받으려고 들고 있는 결과를 정한 표시 수</summary>
		internal const int RetainedSettledShows = 4;

		private readonly IAdMediation _mediation;
		private readonly IMonotonicClock _clock;
		private readonly int _threadId;
		private readonly AdLoadLane _rewarded = new AdLoadLane(AdFormat.Rewarded);
		private readonly AdLoadLane _interstitial = new AdLoadLane(AdFormat.Interstitial);
		private readonly TimeSpan _retryDelay;
		private readonly TimeSpan _maxRetryDelay;
		private readonly TimeSpan _loadTimeout;
		private readonly TimeSpan _foregroundGrace;
		private readonly TimeSpan _absoluteTimeout;
		private readonly TimeSpan _rewardGrace;
		private readonly bool _adPausesApp;
		private readonly List<AdSession> _settled = new List<AdSession>();
		private readonly Queue<Action> _notifications = new Queue<Action>();

		private AdSession _current;
		private GameObject _lifecycleObject;
		private long _lastShowId;
		private TimeSpan _foregroundSince;
		private TimeSpan _lastTickAt;
		private TimeSpan _lastGapAt;
		private bool _notifying;
		private bool _paused;
		private bool _focused;
		private bool _initialized;
		private bool _disposed;

		/// <summary>서비스를 만듭니다. 메인 스레드에서 부르고, 끝나면 <see cref="Dispose"/> 합니다(미디에이션 어댑터는 직접 정리).</summary>
		/// <param name="mediation">미디에이션 어댑터</param>
		/// <param name="options">설정. null 이면 기본값</param>
		/// <exception cref="ArgumentNullException"><paramref name="mediation"/> 이 null</exception>
		/// <exception cref="ArgumentOutOfRangeException">시간 설정이 범위를 벗어남</exception>
		public AdService(IAdMediation mediation, AdServiceOptions options = null)
			: this(mediation, options, new MonotonicClock(), Application.isPlaying, Application.isFocused)
		{
		}

		internal AdService(IAdMediation mediation, AdServiceOptions options, IMonotonicClock clock, bool bindLifecycle, bool focused)
		{
			_mediation = mediation ?? throw new ArgumentNullException(nameof(mediation));
			options ??= new AdServiceOptions();
			Validate(options);

			_clock = clock;
			_threadId = Thread.CurrentThread.ManagedThreadId;
			_retryDelay = options.RetryDelay;
			_maxRetryDelay = options.MaxRetryDelay;
			_loadTimeout = options.LoadTimeout;
			_foregroundGrace = options.ForegroundGrace;
			_absoluteTimeout = options.AbsoluteTimeout;
			_rewardGrace = options.RewardGrace;
			_adPausesApp = options.AdPausesApp ?? _mediation.PausesApp;
			_focused = focused;
			_foregroundSince = _clock.Elapsed;
			_lastTickAt = _foregroundSince;
			_lastGapAt = _foregroundSince - ResumeSettle;

			Subscribe();

			if (bindLifecycle)
			{
				_lifecycleObject = new GameObject(nameof(AdLifecycle)) { hideFlags = HideFlags.HideInHierarchy };
				Object.DontDestroyOnLoad(_lifecycleObject);
				_lifecycleObject.AddComponent<AdLifecycle>().Bind(this);
			}

			if (_mediation.IsInitialized)
			{
				OnMediationInitialized();
			}
		}

		public bool IsInitialized => _initialized;

		public bool IsShowing => _current != null;

		public event Action<bool> ShowingChanged;

		public event Action<AdShowEvent> Displayed;

		public event Action<AdShowEvent> Clicked;

		public event Action<AdShowReport> ShowSettled;

		public event Action<AdLateReward> LateRewarded;

		public event Action<AdRevenue> RevenuePaid;

		public event Action<AdLoadFailure> LoadFailed;

		internal GameObject LifecycleObject => _lifecycleObject;

		private bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _threadId;

		private bool IsForeground => !_paused && _focused;

		public bool IsReady(AdFormat format)
		{
			ThrowIfUnusable();
			LaneOf(format);
			return _initialized && _mediation.IsAvailable(format) && _mediation.IsReady(format);
		}

		public UniTask<Result> ShowRewardedAsync(string placement = null)
		{
			return Show(AdFormat.Rewarded, placement);
		}

		public UniTask<Result> ShowInterstitialAsync(string placement = null)
		{
			return Show(AdFormat.Interstitial, placement);
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			ThrowIfWrongThread();
			_disposed = true;
			Unsubscribe();
			DestroyLifecycle();

			var session = _current;

			if (session != null)
			{
				Settle(session, session.Rewarded ? Result.Ok() : Result.Fail(ResultCodes.Cancelled, "The ad service was disposed"),
					AdSettleReason.Disposed);
			}
		}

		/// <summary>
		/// 로드 재시도·시간 제한과 표시의 시간 초과를 판정합니다. 수명 주기 컴포넌트가 매 프레임 부릅니다.
		/// 처리기가 그 자리에서 서비스를 정리할 수 있어 단계마다 다시 확인합니다.
		/// </summary>
		internal void Tick()
		{
			if (_disposed)
			{
				return;
			}

			var now = _clock.Elapsed;

			// 광고 SDK 가 일시정지 메시지 없이 플레이어 루프를 멈출 수 있습니다(MAX iOS 의 UnityPause)
			var gap = now - _lastTickAt >= FrameGap;
			_lastTickAt = now;

			if (gap)
			{
				_lastGapAt = now;
			}

			TickLane(_rewarded, now);

			if (!_disposed)
			{
				TickLane(_interstitial, now);
			}

			if (!_disposed)
			{
				TickSession(now);
			}
		}

		internal void HandlePause(bool paused)
		{
			var wasForeground = IsForeground;
			_paused = paused;
			MarkForegroundIfReturned(wasForeground);
		}

		internal void HandleFocus(bool focused)
		{
			var wasForeground = IsForeground;
			_focused = focused;
			MarkForegroundIfReturned(wasForeground);
		}

		private UniTask<Result> Show(AdFormat format, string placement)
		{
			ThrowIfUnusable();
			placement = string.IsNullOrEmpty(placement) ? null : placement;

			if (!_mediation.IsAvailable(format))
			{
				return Reject(format, placement, AdsResultCodes.Unavailable, $"No {format} ad unit is set for this platform");
			}

			if (!_initialized && _mediation.IsInitialized)
			{
				OnMediationInitialized();
			}

			if (!_initialized)
			{
				return Reject(format, placement, AdsResultCodes.NotInitialized, "The mediation SDK is not initialized");
			}

			if (_current != null)
			{
				return Reject(format, placement, ResultCodes.InProgress, $"{_current.Format} ad #{_current.Id} is showing");
			}

			var lane = LaneOf(format);

			if (!_mediation.IsReady(format))
			{
				if (lane.State == AdLoadState.Loaded)
				{
					// SDK 가 광고를 버렸습니다(만료 등). 다시 불러옵니다
					lane.State = AdLoadState.Idle;
					lane.LoadedAd = null;
				}

				RequestLoad(lane);
				return Reject(format, placement, AdsResultCodes.NotReady, $"No {format} ad is loaded ({lane.State})");
			}

			var session = new AdSession(++_lastShowId, format, placement, lane.LoadedAd ?? AdDetails.Unknown, _clock.Elapsed);
			lane.State = AdLoadState.Idle;
			lane.LoadedAd = null;
			_current = session;
			Logger.Info($"Showing {format} ad #{session.Id} at {placement ?? "(no placement)"}");

			// SDK 표시도 큐를 지나 열림 알림이 모든 구독자에게 간 뒤에 합니다. 처리기 안에서 요청한 표시라도 열림 처리기가 정리하면 SDK 를 부르지 않습니다
			Enqueue(ShowingChanged, true, nameof(ShowingChanged));
			_notifications.Enqueue(() => StartShow(session, lane));
			Drain();
			return session.Completion.Task;
		}

		private void StartShow(AdSession session, AdLoadLane lane)
		{
			// 처리기가 서비스를 정리했으면 이미 끝난 표시입니다
			if (_disposed || _current != session || session.Settled)
			{
				return;
			}

			try
			{
				_mediation.Show(session.Format, session.Placement);
			}
			catch (Exception exception)
			{
				// 어댑터 버그입니다. 로그로 드러내고 표시 실패로 끝내 버튼이 잠기지 않게 합니다
				Logger.Error(exception, $"Mediation Show({session.Format}) threw");

				if (!session.Settled)
				{
					session.Ended = true;
					RequestLoad(lane);
					Settle(session, Result.Fail(AdsResultCodes.DisplayFailed, $"Show threw {exception.GetType().Name}: {exception.Message}"),
						AdSettleReason.DisplayFailed);
				}
			}
		}

		private UniTask<Result> Reject(AdFormat format, string placement, int code, string message)
		{
			var result = Result.Fail(code, message);
			var completion = new UniTaskCompletionSource<Result>();
			Logger.Info($"{format} ad at {placement ?? "(no placement)"} rejected: {message}");
			Enqueue(ShowSettled, new AdShowReport(0, format, placement, result, AdSettleReason.Rejected, false, TimeSpan.Zero, AdDetails.Unknown, null),
				nameof(ShowSettled));
			EnqueueCompletion(completion, result);
			Drain();
			return completion.Task;
		}

		/// <summary>
		/// 표시의 결과를 정합니다. 몇 번 불려도 한 번만 정합니다. 상태를 먼저 맞추고 이벤트를 올린 뒤 호출자를 깨웁니다(이어지는 코드가 바로 다음 광고를 요청할 수 있음).
		/// 처리기 안에서 불리면 알림과 완료는 지금 알림이 모든 구독자에게 간 뒤에 같은 순서로 나갑니다(<see cref="Notify{T}"/>).
		/// </summary>
		private void Settle(AdSession session, Result result, AdSettleReason reason)
		{
			if (!session.TrySettle(result))
			{
				return;
			}

			if (_current == session)
			{
				_current = null;
			}

			_settled.Add(session);

			if (_settled.Count > RetainedSettledShows)
			{
				_settled.RemoveAt(0);
			}

			var elapsed = _clock.Elapsed - session.ShownAt;

			if (result.IsSuccess)
			{
				Logger.Info($"{session.Format} ad #{session.Id} done ({reason}, {elapsed.TotalSeconds:F1}s)");
			}
			else if (result.Code == ResultCodes.Timeout)
			{
				Logger.Warning($"{session.Format} ad #{session.Id} timed out: {result.Message} (displayed {session.Displayed}, {elapsed.TotalSeconds:F1}s)");
			}
			else
			{
				Logger.Info($"{session.Format} ad #{session.Id} failed ({reason}): {result}");
			}

			var report = new AdShowReport(session.Id, session.Format, session.Placement, result, reason, session.Displayed, elapsed, session.LastAd,
				session.Error);
			// 세 알림을 함께 넣어, 처리기가 그 자리에서 연 다음 광고의 알림은 이 표시의 결과 보고와 완료 뒤에 나갑니다
			Enqueue(ShowingChanged, false, nameof(ShowingChanged));
			Enqueue(ShowSettled, report, nameof(ShowSettled));
			EnqueueCompletion(session.Completion, result);
			Drain();
		}

		/// <summary>
		/// 닫힘 콜백이 오지 않은 표시를 앱이 앞에 있는 시간으로 판정합니다. 앱이 앞에 온 지(일시정지·포커스 메시지) <see cref="AdServiceOptions.ForegroundGrace"/> 가
		/// 지나기 전에는 끝내지 않아, Android 복귀 직후 SDK 큐에 쌓인 콜백보다 먼저 끝내는 일이 없습니다. 앱을 멈추는 미디에이션의 복귀 뒤 닫힘 대기는
		/// 메시지 없이 멈췄던 루프(MAX iOS 의 UnityPause)의 복귀부터도 셉니다. 그 대기는 프레임이 계속 느리면 늘어날 수 있지만 절대 상한이 끝을 보장합니다.
		/// </summary>
		private void TickSession(TimeSpan now)
		{
			var session = _current;

			if (session == null || !IsForeground)
			{
				return;
			}

			if (session.HiddenAt.HasValue)
			{
				if (now - Max(session.HiddenAt.Value, _foregroundSince) >= _rewardGrace)
				{
					if (Confirmed(session, now))
					{
						Settle(session, Result.Fail(AdsResultCodes.NotRewarded, "The ad was closed before the reward"), AdSettleReason.Closed);
					}
				}
				else
				{
					session.TimedOutSince = null;
				}

				return;
			}

			if (now - Max(_foregroundSince, session.ShownAt) < _foregroundGrace)
			{
				session.TimedOutSince = null;
				return;
			}

			AdSettleReason reason;

			if (now - session.ShownAt >= _absoluteTimeout)
			{
				reason = AdSettleReason.AbsoluteTimeout;
			}
			else if (!session.Displayed)
			{
				reason = AdSettleReason.DisplayTimeout;
			}
			else if (_adPausesApp && now - Max(_lastGapAt, Max(_foregroundSince, session.ShownAt)) >= _foregroundGrace)
			{
				reason = AdSettleReason.ForegroundTimeout;
			}
			else
			{
				session.TimedOutSince = null;
				return;
			}

			if (!Confirmed(session, now))
			{
				return;
			}

			RequestLoad(LaneOf(session.Format));
			Settle(session, session.Rewarded ? Result.Ok() : Result.Fail(ResultCodes.Timeout, $"No end callback ({reason})"), reason);
		}

		/// <summary>
		/// 시간 기준 확정 조건이 지금 끝내도 되는지입니다. 최근(<see cref="ResumeSettle"/> 안)에 프레임 간격이 없었으면 바로 끝냅니다. 있었으면 조건이 처음 보인 때부터
		/// <see cref="ResumeSettle"/> 와 <see cref="ResumeSettleFrames"/> 프레임이 지나야 끝냅니다. 그사이 콜백이 와 조건이 풀리면 호출자가 기록을 지웁니다.
		/// </summary>
		private bool Confirmed(AdSession session, TimeSpan now)
		{
			if (session.TimedOutSince == null)
			{
				if (now - _lastGapAt >= ResumeSettle)
				{
					return true;
				}

				session.TimedOutSince = now;
				session.TimedOutFrames = 0;
				return false;
			}

			session.TimedOutFrames++;
			return session.TimedOutFrames >= ResumeSettleFrames && now - session.TimedOutSince.Value >= ResumeSettle;
		}

		private void MarkForegroundIfReturned(bool wasForeground)
		{
			if (!wasForeground && IsForeground)
			{
				_foregroundSince = _clock.Elapsed;
			}
		}

		private AdLoadLane LaneOf(AdFormat format)
		{
			switch (format)
			{
				case AdFormat.Rewarded:
					return _rewarded;
				case AdFormat.Interstitial:
					return _interstitial;
				default:
					throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown ad format");
			}
		}

		private void ThrowIfUnusable()
		{
			if (_disposed)
			{
				throw new ObjectDisposedException(nameof(AdService));
			}

			ThrowIfWrongThread();
		}

		private void ThrowIfWrongThread()
		{
			if (!IsMainThread)
			{
				throw new InvalidOperationException("AdService must be used from the thread that created it");
			}
		}

		private void DestroyLifecycle()
		{
			var lifecycleObject = _lifecycleObject;
			_lifecycleObject = null;

			if (lifecycleObject == null)
			{
				return;
			}

			// 파괴는 프레임 끝에 일어나므로 먼저 끊어 늦게 오는 메시지가 닫힌 서비스에 닿지 않게 합니다
			if (lifecycleObject.TryGetComponent<AdLifecycle>(out var lifecycle))
			{
				lifecycle.Unbind();
			}

			if (Application.isPlaying)
			{
				Object.Destroy(lifecycleObject);
			}
			else
			{
				Object.DestroyImmediate(lifecycleObject);
			}
		}

		private static TimeSpan Max(TimeSpan a, TimeSpan b)
		{
			return a > b ? a : b;
		}

		private static void Validate(AdServiceOptions options)
		{
			Require(options.RetryDelay >= AdServiceOptions.MinRetryDelay && options.RetryDelay <= MaxDelaySetting, options.RetryDelay,
				nameof(AdServiceOptions.RetryDelay), "must be between MinRetryDelay and one day");
			Require(options.MaxRetryDelay >= options.RetryDelay && options.MaxRetryDelay <= MaxDelaySetting, options.MaxRetryDelay,
				nameof(AdServiceOptions.MaxRetryDelay), "must be between RetryDelay and one day");
			Require(options.LoadTimeout >= AdServiceOptions.MinLoadTimeout, options.LoadTimeout, nameof(AdServiceOptions.LoadTimeout),
				"must be at least MinLoadTimeout");
			Require(options.ForegroundGrace > TimeSpan.Zero, options.ForegroundGrace, nameof(AdServiceOptions.ForegroundGrace), "must be positive");
			Require(options.AbsoluteTimeout >= options.ForegroundGrace, options.AbsoluteTimeout, nameof(AdServiceOptions.AbsoluteTimeout),
				"must be at least ForegroundGrace");
			Require(options.RewardGrace >= TimeSpan.Zero, options.RewardGrace, nameof(AdServiceOptions.RewardGrace), "must not be negative");
		}

		private static void Require(bool condition, TimeSpan value, string name, string message)
		{
			if (!condition)
			{
				throw new ArgumentOutOfRangeException(name, value, $"{name} {message}");
			}
		}

		/// <summary>
		/// 이벤트를 알림 큐에 넣고 내보냅니다. 처리기 안에서 다시 알리면(정리, 다음 광고 표시) 지금 알림이 모든 구독자에게 간 뒤에 나가,
		/// 구독자마다 상태 변화를 같은 순서로 받습니다.
		/// </summary>
		private void Notify<T>(Action<T> handlers, T argument, string name)
		{
			Enqueue(handlers, argument, name);
			Drain();
		}

		private void Enqueue<T>(Action<T> handlers, T argument, string name)
		{
			if (handlers != null)
			{
				_notifications.Enqueue(() => Raise(handlers, argument, name));
			}
		}

		/// <summary>호출자 완료도 알림 큐를 지나 그 앞의 이벤트보다 먼저 깨우지 않습니다.</summary>
		private void EnqueueCompletion(UniTaskCompletionSource<Result> completion, Result result)
		{
			_notifications.Enqueue(() => completion.TrySetResult(result));
		}

		private void Drain()
		{
			if (_notifying)
			{
				return;
			}

			_notifying = true;

			try
			{
				while (_notifications.Count > 0)
				{
					_notifications.Dequeue()();
				}
			}
			finally
			{
				_notifying = false;
			}
		}

		private static void Raise<T>(Action<T> handlers, T argument, string name)
		{
			if (handlers == null)
			{
				return;
			}

			foreach (var handler in handlers.GetInvocationList())
			{
				try
				{
					((Action<T>) handler)(argument);
				}
				catch (Exception exception)
				{
					// 게임 처리기의 예외가 결과 확정을 끊지 않게 로그로 드러내고 계속합니다(받지 못하면 버튼이 잠김)
					Logger.Error(exception, $"A {name} handler threw");
				}
			}
		}
	}
}
