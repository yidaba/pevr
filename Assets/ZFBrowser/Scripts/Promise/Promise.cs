using ZenFulcrum.EmbeddedBrowser.Promises;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZenFulcrum.EmbeddedBrowser
{
	/// <summary>
	/// Implements a C# promise.
	/// https://developer.mozilla.org/en/docs/Web/JavaScript/Reference/Global_Objects/Promise
	/// 
	/// This can also be waited on in a Unity coroutine and queried for its value.
	/// </summary>
	public interface IPromise<PromisedT>
	{
		/// <summary>
		/// Set the name of the promise, useful for debugging.
		/// </summary>
		IPromise<PromisedT> WithName(string name);

		/// <summary>
		/// Completes the promise. 
		/// onResolved is called on successful completion.
		/// onRejected is called on error.
		/// </summary>
        void Done(System.Action<PromisedT> onResolved, System.Action<Exception> onRejected);

		/// <summary>
		/// Completes the promise. 
		/// onResolved is called on successful completion.
		/// Adds a default error handler.
		/// </summary>
        void Done(System.Action<PromisedT> onResolved);

		/// <summary>
		/// Complete the promise. Adds a default error handler.
		/// </summary>
		void Done();

		/// <summary>
		/// Handle errors for the promise. 
		/// </summary>
        IPromise<PromisedT> Catch(System.Action<Exception> onRejected);

		/// <summary>
		/// Add a resolved callback that chains a value promise (optionally converting to a different value type).
		/// </summary>
		IPromise<ConvertedT> Then<ConvertedT>(Func<PromisedT, IPromise<ConvertedT>> onResolved);

		/// <summary>
		/// Add a resolved callback that chains a non-value promise.
		/// </summary>
		IPromise Then(Func<PromisedT, IPromise> onResolved);

		/// <summary>
		/// Add a resolved callback.
		/// </summary>
        IPromise<PromisedT> Then(System.Action<PromisedT> onResolved);

		/// <summary>
		/// Add a resolved callback and a rejected callback.
		/// The resolved callback chains a value promise (optionally converting to a different value type).
		/// </summary>
        IPromise<ConvertedT> Then<ConvertedT>(Func<PromisedT, IPromise<ConvertedT>> onResolved, System.Action<Exception> onRejected);

		/// <summary>
		/// Add a resolved callback and a rejected callback.
		/// The resolved callback chains a non-value promise.
		/// </summary>
        IPromise Then(Func<PromisedT, IPromise> onResolved, System.Action<Exception> onRejected);

		/// <summary>
		/// Add a resolved callback and a rejected callback.
		/// </summary>
        IPromise<PromisedT> Then(System.Action<PromisedT> onResolved, System.Action<Exception> onRejected);

		/// <summary>
		/// Return a new promise with a different value.
		/// May also change the type of the value.
		/// </summary>
		IPromise<ConvertedT> Then<ConvertedT>(Func<PromisedT, ConvertedT> transform);

		/// <summary>
		/// Return a new promise with a different value.
		/// May also change the type of the value.
		/// </summary>
		[Obsolete("Use Then instead")]
		IPromise<ConvertedT> Transform<ConvertedT>(Func<PromisedT, ConvertedT> transform);

		/// <summary>
		/// Chain an enumerable of promises, all of which must resolve.
		/// Returns a promise for a collection of the resolved results.
		/// The resulting promise is resolved when all of the promises have resolved.
		/// It is rejected as soon as any of the promises have been rejected.
		/// </summary>
		IPromise<IEnumerable<ConvertedT>> ThenAll<ConvertedT>(Func<PromisedT, IEnumerable<IPromise<ConvertedT>>> chain);

		/// <summary>
		/// Chain an enumerable of promises, all of which must resolve.
		/// Converts to a non-value promise.
		/// The resulting promise is resolved when all of the promises have resolved.
		/// It is rejected as soon as any of the promises have been rejected.
		/// </summary>
		IPromise ThenAll(Func<PromisedT, IEnumerable<IPromise>> chain);

		/// <summary>
		/// Takes a function that yields an enumerable of promises.
		/// Returns a promise that resolves when the first of the promises has resolved.
		/// Yields the value from the first promise that has resolved.
		/// </summary>
		IPromise<ConvertedT> ThenRace<ConvertedT>(Func<PromisedT, IEnumerable<IPromise<ConvertedT>>> chain);

		/// <summary>
		/// Takes a function that yields an enumerable of promises.
		/// Converts to a non-value promise.
		/// Returns a promise that resolves when the first of the promises has resolved.
		/// Yields the value from the first promise that has resolved.
		/// </summary>
		IPromise ThenRace(Func<PromisedT, IEnumerable<IPromise>> chain);

		/// <summary>
		/// Returns the resulting value if resolved.
		/// Throws the rejection if rejected.
		/// Throws an exception if not settled.
		/// </summary>
		PromisedT Value { get; }

		/// <summary>
		/// Returns an enumerable that yields null until the promise is settled.
		/// ("To WaitFor" like the WaitForXXYY functions Unity provides.)
		/// Suitable for use with a Unity coroutine's "yield return promise.ToWaitFor()"
		/// Once it finishes, use promise.Value to retrieve the value/error.
		/// 
		/// If throwOnFail is true, the coroutine will abort on promise rejection.
		/// </summary>
		/// <returns></returns>
		IEnumerator ToWaitFor(bool abortOnFail = false);
	}

	/// <summary>
	/// Interface for a promise that can be rejected.
	/// </summary>
	public interface IRejectable
	{
		/// <summary>
		/// Reject the promise with an exception.
		/// </summary>
		void Reject(Exception ex);
	}

	/// <summary>
	/// Interface for a promise that can be rejected or resolved.
	/// </summary>
	public interface IPendingPromise<PromisedT> : IRejectable
	{
		/// <summary>
		/// Resolve the promise with a particular value.
		/// </summary>
		void Resolve(PromisedT value);
	}

	/// <summary>
	/// Specifies the state of a promise.
	/// </summary>
	public enum PromiseState
	{
		Pending,    // The promise is in-flight.
		Rejected,   // The promise has been rejected.
		Resolved    // The promise has been resolved.
	};

	/// <summary>
	/// Implements a C# promise.
	/// https://developer.mozilla.org/en/docs/Web/JavaScript/Reference/Global_Objects/Promise
	/// </summary>
	public class Promise<PromisedT> : IPromise<PromisedT>, IPendingPromise<PromisedT>, IPromiseInfo
	{
		/// <summary>
		/// The exception when the promise is rejected.
		/// </summary>
		private Exception rejectionException;

		/// <summary>
		/// The value when the promises is resolved.
		/// </summary>
		private PromisedT resolveValue;

		/// <summary>
		/// Error handler.
		/// </summary>
		private List<RejectHandler> rejectHandlers;

		/// <summary>
		/// Completed handlers that accept a value.
		/// </summary>
        private List<System.Action<PromisedT>> resolveCallbacks;
		private List<IRejectable> resolveRejectables;

		/// <summary>
		/// ID of the promise, useful for debugging.
		/// </summary>
		public int Id { get; private set; }

		/// <summary>
		/// Name of the promise, when set, useful for debugging.
		/// </summary>
		public string Name { get; private set; }

		/// <summary>
		/// Tracks the current state of the promise.
		/// </summary>
		public PromiseState CurState { get; private set; }

		public Promise()
		{
			this.CurState = PromiseState.Pending;
			this.Id = ++Promise.nextPromiseId;

			if (Promise.EnablePromiseTracking)
			{
				Promise.pendingPromises.Add(this);
			}
		}

        public Promise(System.Action<System.Action<PromisedT>, System.Action<Exception>> resolver)
		{
			this.CurState = PromiseState.Pending;
			this.Id = ++Promise.nextPromiseId;

			if (Promise.EnablePromiseTracking)
			{
				Promise.pendingPromises.Add(this);
			}

			try
			{
				resolver(
					// Resolve
					value => Resolve(value),

					// Reject
					ex => Reject(ex)
				);
			}
			catch (Exception ex)
			{
				Reject(ex);
			}
		}

		/// <summary>
		/// Add a rejection handler for this promise.
		/// </summary>
        private void AddRejectHandler(System.Action<Exception> onRejected, IRejectable rejectable)
		{
			if (rejectHandlers == null)
			{
				rejectHandlers = new List<RejectHandler>();
			}

			rejectHandlers.Add(new RejectHandler() { callback = onRejected, rejectable = rejectable }); ;
		}

		/// <summary>
		/// Add a resolve handler for this promise.
		/// </summary>
        private void AddResolveHandler(System.Action<PromisedT> onResolved, IRejectable rejectable)
		{
			if (resolveCallbacks == null)
			{
                resolveCallbacks = new List<System.Action<PromisedT>>();
			}

			if (resolveRejectables == null)
			{
				resolveRejectables = new List<IRejectable>();
			}

			resolveCallbacks.Add(onResolved);
			resolveRejectables.Add(rejectable);
		}

		/// <summary>
		/// Invoke a single handler.
		/// </summary>
        private void InvokeHandler<T>(System.Action<T> callback, IRejectable rejectable, T value)
		{
//            Argument.NotNull(() => callback);
//            Argument.NotNull(() => rejectable);            

			try
			{
				callback(value);
			}
			catch (Exception ex)
			{
				rejectable.Reject(ex);
			}
		}

		/// <summary>
		/// Helper function clear out all handlers after resolution or rejection.
		/// </summary>
		private void ClearHandlers()
		{
			rejectHandlers = null;
			resolveCallbacks = null;
			resolveRejectables = null;
		}

		/// <summary>
		/// Invoke all reject handlers.
		/// </summary>
		private void InvokeRejectHandlers(Exception ex)
		{
//            Argument.NotNull(() => ex);

			if (rejectHandlers != null)
			{
				rejectHandlers.Each(handler => InvokeHandler(handler.callback, handler.rejectable, ex));
			}

			ClearHandlers();
		}

		/// <summary>
		/// Invoke all resolve handlers.
		/// </summary>
		private void InvokeResolveHandlers(PromisedT value)
		{
			if (resolveCallbacks != null)
			{
				for (int i = 0, maxI = resolveCallbacks.Count; i < maxI; i++) {
					InvokeHandler(resolveCallbacks[i], resolveRejectables[i], value);
				}
			}

			ClearHandlers();
		}

		/// <summary>
		/// Reject the promise with an exception.
		/// </summary>
		public void Reject(Exception ex)
		{
//            Argument.NotNull(() => ex);

			if (CurState != PromiseState.Pending)
			{
				throw new ApplicationException("Attempt to reject a promise that is already in state: " + CurState + ", a promise can only be rejected when it is still in state: " + PromiseState.Pending);
			}

			rejectionException = ex;
			CurState = PromiseState.Rejected;

			if (Promise.EnablePromiseTracking)
			{
				Promise.pendingPromises.Remove(this);
			}

			InvokeRejectHandlers(ex);
		}

		/// <summary>
		/// Resolve the promise with a particular value.
		/// </summary>
		public void Resolve(PromisedT value)
		{
			if (CurState != PromiseState.Pending)
			{
				throw new ApplicationException("Attempt to resolve a promise that is already in state: " + CurState + ", a promise can only be resolved when it is still in state: " + PromiseState.Pending);
			}

			resolveValue = value;
			CurState = PromiseState.Resolved;

			if (Promise.EnablePromiseTracking)
			{
				Promise.pendingPromises.Remove(this);
			}

			InvokeResolveHandlers(value);
		}

		/// <summary>
		/// Completes the promise. 
		/// onResolved is called on successful completion.
		/// onRejected is called on error.
		/// </summary>
        public void Done(System.Action<PromisedT> onResolved, System.Action<Exception> onRejected)
		{
			Then(onResolved, onRejected)
				.Catch(ex =>
					Promise.PropagateUnhandledException(this, ex)
				);
		}

		/// <summary>
		/// Completes the promise. 
		/// onResolved is called on successful completion.
		/// Adds a default error handler.
		/// </summary>
        public void Done(System.Action<PromisedT> onResolved)
		{
			Then(onResolved)
				.Catch(ex =>
					Promise.PropagateUnhandledException(this, ex)
				);
		}

		/// <summary>
		/// Complete the promise. Adds a default error handler.
		/// </summary>
		public void Done()
		{
			Catch(ex =>
				Promise.PropagateUnhandledException(this, ex)
			);
		}

		/// <summary>
		/// Set the name of the promise, useful for debugging.
		/// </summary>
		public IPromise<PromisedT> WithName(string name)
		{
			this.Name = name;
			return this;
		}

		/// <summary>
		/// Handle errors for the promise. 
		/// </summary>
        public IPromise<PromisedT> Catch(System.Action<Exception> onRejected)
		{
//            Argument.NotNull(() => onRejected);

			var resultPromise = new Promise<PromisedT>();
			resultPromise.WithName(Name);

            System.Action<PromisedT> resolveHandler = v =>
			{
				resultPromise.Resolve(v);
			};

            System.Action<Exception> rejectHandler = ex =>
			{
				onRejected(ex);

				resultPromise.Reject(ex);
			};

			ActionHandlers(resultPromise, resolveHandler, rejectHandler);

			return resultPromise;
		}

		/// <summary>
		/// Add a resolved callback that chains a value promise (optionally converting to a different value type).
		/// </summary>
		public IPromise<ConvertedT> Then<ConvertedT>(Func<PromisedT, IPromise<ConvertedT>> onResolved)
		{
			return Then(onResolved, null);
		}

		/// <summary>
		/// Add a resolved callback that chains a non-value promise.
		/// </summary>
		public IPromise Then(Func<PromisedT, IPromise> onResolved)
		{
			return Then(onResolved, null);
		}

		/// <summary>
		/// Add a resolved callback.
		/// </summary>
        public IPromise<PromisedT> Then(System.Action<PromisedT> onResolved)
		{
			return Then(onResolved, null);
		}

		/// <summary>
		/// Add a resolved callback and a rejected callback.
		/// The resolved callback chains a value promise (optionally converting to a different value type).
		/// </summary>
        public IPromise<ConvertedT> Then<ConvertedT>(Func<PromisedT, IPromise<ConvertedT>> onResolved, System.Action<Exception> onRejected)
		{
			// This version of the function must supply an onResolved.
			// Otherwise there is now way to get the converted value to pass to the resulting promise.
//            Argument.NotNull(() => onResolved); 

			var resultPromise = new Promise<ConvertedT>();
			resultPromise.WithName(Name);

            System.Action<PromisedT> resolveHandler = v =>
			{
				onResolved(v)
					.Then(
						// Should not be necessary to specify the arg type on the next line, but Unity (mono) has an internal compiler error otherwise.
						(ConvertedT chainedValue) => resultPromise.Resolve(chainedValue),
						ex => resultPromise.Reject(ex)
					);
			};

            System.Action<Exception> rejectHandler = ex =>
			{
				if (onRejected != null)
				{
					onRejected(ex);
				}

				resultPromise.Reject(ex);
			};

			ActionHandlers(resultPromise, resolveHandler, rejectHandler);

			return resultPromise;
		}

		/// <summary>
		/// Add a resolved callback and a rejected callback.
		/// The resolved callback chains a non-value promise.
		/// </summary>
        public IPromise Then(Func<PromisedT, IPromise> onResolved, System.Action<Exception> onRejected)
		{
			var resultPromise = new Promise();
			resultPromise.WithName(Name);

            System.Action<PromisedT> resolveHandler = v =>
			{
				if (onResolved != null)
				{
					onResolved(v)
						.Then(
							() => resultPromise.Resolve(),
							ex => resultPromise.Reject(ex)
						);
				}
				else
				{
					resultPromise.Resolve();
				}
			};

            System.Action<Exception> rejectHandler = ex =>
			{
				if (onRejected != null)
				{
					onRejected(ex);
				}

				resultPromise.Reject(ex);
			};

			ActionHandlers(resultPromise, resolveHandler, rejectHandler);

			return resultPromise;
		}

		/// <summary>
		/// Add a resolved callback and a rejected callback.
		/// </summary>
        public IPromise<PromisedT> Then(System.Action<PromisedT> onResolved, System.Action<Exception> onRejected)
		{
			var resultPromise = new Promise<PromisedT>();
			resultPromise.WithName(Name);

            System.Action<PromisedT> resolveHandler = v =>
			{
				if (onResolved != null)
				{
					onResolved(v);
				}

				resultPromise.Resolve(v);
			};

            System.Action<Exception> rejectHandler = ex =>
			{
				if (onRejected != null)
				{
					onRejected(ex);
				}

				resultPromise.Reject(ex);
			};

			ActionHandlers(resultPromise, resolveHandler, rejectHandler);

			return resultPromise;
		}

		/// <summary>
		/// Return a new promise with a different value.
		/// May also change the type of the value.
		/// </summary>
		public IPromise<ConvertedT> Then<ConvertedT>(Func<PromisedT, ConvertedT> transform)
		{
//            Argument.NotNull(() => transform);
			return Then(value => Promise<ConvertedT>.Resolved(transform(value)));
		}

		/// <summary>
		/// Return a new promise with a different value.
		/// May also change the type of the value.
		/// </summary>
		[Obsolete("Use Then instead")]
		public IPromise<ConvertedT> Transform<ConvertedT>(Func<PromisedT, ConvertedT> transform)
		{
//            Argument.NotNull(() => transform);
			return Then(value => Promise<ConvertedT>.Resolved(transform(value)));
		}

		/// <summary>
		/// Helper function to invoke or register resolve/reject handlers.
		/// </summary>
        private void ActionHandlers(IRejectable resultPromise, System.Action<PromisedT> resolveHandler, System.Action<Exception> rejectHandler)
		{
			if (CurState == PromiseState.Resolved)
			{
				InvokeHandler(resolveHandler, resultPromise, resolveValue);
			}
			else if (CurState == PromiseState.Rejected)
			{
				InvokeHandler(rejectHandler, resultPromise, rejectionException);
			}
			else
			{
				AddResolveHandler(resolveHandler, resultPromise);
				AddRejectHandler(rejectHandler, resultPromise);
			}
		}

		/// <summary>
		/// Chain an enumerable of promises, all of which must resolve.
		/// Returns a promise for a collection of the resolved results.
		/// The resulting promise is resolved when all of the promises have resolved.
		/// It is rejected as soon as any of the promises have been rejected.
		/// </summary>
		public IPromise<IEnumerable<ConvertedT>> ThenAll<ConvertedT>(Func<PromisedT, IEnumerable<IPromise<ConvertedT>>> chain)
		{
			return Then(value => Promise<ConvertedT>.All(chain(value)));
		}

		/// <summary>
		/// Chain an enumerable of promises, all of which must resolve.
		/// Converts to a non-value promise.
		/// The resulting promise is resolved when all of the promises have resolved.
		/// It is rejected as soon as any of the promises have been rejected.
		/// </summary>
		public IPromise ThenAll(Func<PromisedT, IEnumerable<IPromise>> chain)
		{
			return Then(value => Promise.All(chain(value)));
		}

		/// <summary>
		/// Returns a promise that resolves when all of the promises in the enumerable argument have resolved.
		/// Returns a promise of a collection of the resolved results.
		/// </summary>
		public static IPromise<IEnumerable<PromisedT>> All(params IPromise<PromisedT>[] promises)
		{
			return All((IEnumerable<IPromise<PromisedT>>)promises); // Cast is required to force use of the other All function.
		}

		/// <summary>
		/// Returns a promise that resolves when all of the promises in the enumerable argument have resolved.
		/// Returns a promise of a collection of the resolved results.
		/// </summary>
		public static IPromise<IEnumerable<PromisedT>> All(IEnumerable<IPromise<PromisedT>> promises)
		{
			var promisesArray = promises.ToArray();
			if (promisesArray.Length == 0)
			{
				return Promise<IEnumerable<PromisedT>>.Resolved(EnumerableExt.Empty<PromisedT>());
			}

			var remainingCount = promisesArray.Length;
			var results = new PromisedT[remainingCount];
			var resultPromise = new Promise<IEnumerable<PromisedT>>();
			resultPromise.WithName("All");

			promisesArray.Each((promise, index) =>
			{
				promise
					.Catch(ex =>
					{
						if (resultPromise.CurState == PromiseState.Pending)
						{
							// If a promise errorred and the result promise is still pending, reject it.
							resultPromise.Reject(ex);
						}
					})
					.Then(result =>
					{
						results[index] = result;

						--remainingCount;
						if (remainingCount <= 0)
						{
							// This will never happen if any of the promises errorred.
							resultPromise.Resolve(results);
						}
					})
					.Done();
			});

			return resultPromise;
		}

		/// <summary>
		/// Takes a function that yields an enumerable of promises.
		/// Returns a promise that resolves when the first of the promises has resolved.
		/// Yields the value from the first promise that has resolved.
		/// </summary>
		public IPromise<ConvertedT> ThenRace<ConvertedT>(Func<PromisedT, IEnumerable<IPromise<ConvertedT>>> chain)
		{
			return Then(value => Promise<ConvertedT>.Race(chain(value)));
		}

		/// <summary>
		/// Takes a function that yields an enumerable of promises.
		/// Converts to a non-value promise.
		/// Returns a promise that resolves when the first of the promises has resolved.
		/// Yields the value from the first promise that has resolved.
		/// </summary>
		public IPromise ThenRace(Func<PromisedT, IEnumerable<IPromise>> chain)
		{
			return Then(value => Promise.Race(chain(value)));
		}

		public PromisedT Value 
		{
			get 
			{
				if (CurState == PromiseState.Pending) throw new InvalidOperationException("Promise not settled");
				else if (CurState == PromiseState.Rejected) throw rejectionException;
				return resolveValue;
			}
		}

		class Enumerated<T> : IEnumerator {
			private Promise<T> promise;
			private bool abortOnFail;

			public Enumerated(Promise<T> promise, bool abortOnFail) {
				this.promise = promise;
				this.abortOnFail = abortOnFail;
			}

			public bool MoveNext() {
				if (abortOnFail && promise.CurState == PromiseState.Rejected) {
					throw promise.rejectionException;
				}
				return promise.CurState == PromiseState.Pending;
			}

			public void Reset() { }

			public object Current { get { return null; } }
		}

		public IEnumerator ToWaitFor(bool abortOnFail) {
			var ret = new Enumerated<PromisedT>(this, abortOnFail);
			//someone will poll for completion, so act like we've been terminated
			Done(x => {}, ex => {});
			return ret;
		}


		/// <summary>
		/// Returns a promise that resolves when the first of the promises in the enumerable argument have resolved.
		/// Returns the value from the first promise that has resolved.
		/// </summary>
		public static IPromise<PromisedT> Race(params IPromise<PromisedT>[] promises)
		{
			return Race((IEnumerable<IPromise<PromisedT>>)promises); // Cast is required to force use of the other function.
		}

		/// <summary>
		/// Returns a promise that resolves when the first of the promises in the enumerable argument have resolved.
		/// Returns the value from the first promise that has resolved.
		/// </summary>
		public static IPromise<PromisedT> Race(IEnumerable<IPromise<PromisedT>> promises)
		{
			var promisesArray = promises.ToArray();
			if (promisesArray.Length == 0)
			{
				throw new ApplicationException("At least 1 input promise must be provided for Race");
			}

			var resultPromise = new Promise<PromisedT>();
			resultPromise.WithName("Race");

			promisesArray.Each((promise, index) =>
			{
				promise
					.Catch(ex =>
					{
						if (resultPromise.CurState == PromiseState.Pending)
						{
							// If a promise errorred and the result promise is still pending, reject it.
							resultPromise.Reject(ex);
						}
					})
					.Then(result =>
					{
						if (resultPromise.CurState == PromiseState.Pending)
						{
							resultPromise.Resolve(result);
						}
					})
					.Done();
			});

			return resultPromise;
		}

		/// <summary>
		/// Convert a simple value directly into a resolved promise.
		/// </summary>
		public static IPromise<PromisedT> Resolved(PromisedT promisedValue)
		{
			var promise = new Promise<PromisedT>();
			promise.Resolve(promisedValue);
			return promise;
		}

		/// <summary>
		/// Convert an exception directly into a rejected promise.
		/// </summary>
		public static IPromise<PromisedT> Rejected(Exception ex)
		{
//            Argument.NotNull(() => ex);

			var promise = new Promise<PromisedT>();
			promise.Reject(ex);
			return promise;
		}

	}
}
