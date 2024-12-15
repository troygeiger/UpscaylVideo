using System;
using System.Threading;
using System.Threading.Tasks;

namespace UpscaylVideo.Helpers;

public static class TaskHelpers
    {
        public static Action<Exception>? GlobalOnException { get; set; }

        private static bool HandleException(Exception? exception, Action<Exception>? handler)
        {
            if (exception == null)
                return false;
            GlobalOnException?.Invoke(exception);
            handler?.Invoke(exception);
            return true;
        }

        /// <summary>
        /// Waits for TimeSpan and returns true if there was an exception. Does not throw exception your code if canceled.
        /// </summary>
        /// <param name="timeout">How long to wait.</param>
        /// <param name="token">Cancellation token to cancel the wait.</param>
        /// <param name="handleExceptionAction">An optional action to call if there is an exception.</param>
        /// <returns>Task of boolean.</returns>
        public static Task<bool> Wait(TimeSpan timeout, CancellationToken token = default, Action<Exception>? handleExceptionAction = null) =>
            Task.Delay(timeout, token).ContinueWith(tsk => HandleException(tsk.Exception, handleExceptionAction));

        /// <summary>
        /// Waits for TimeSpan and returns true if there was an exception. Does not throw exception your code if canceled.
        /// </summary>
        /// <param name="timeoutMs">How long to wait in milliseconds.</param>
        /// <param name="token">Cancellation token to cancel the wait.</param>
        /// <param name="handleExceptionAction">An optional action to call if there is an exception.</param>
        /// <returns>Task of boolean.</returns>
        public static Task<bool> Wait(int timeoutMs, CancellationToken token = default, Action<Exception>? handleExceptionAction = null) =>
            Task.Delay(timeoutMs, token).ContinueWith(tsk => HandleException(tsk.Exception, handleExceptionAction));

        /// <summary>
        /// Runs a task without the need to await and invokes optional actions for handling exceptions.
        /// </summary>
        /// <param name="task">The task to run</param>
        /// <param name="configureAwait">true to attempt to marshal the continuation back to the original context captured; otherwise, false. Default true.</param>
        /// <param name="handleExceptionAction">An action to call if there is an exception.</param>
        public static async void FireAndForget(this Task task, bool configureAwait = true, Action<Exception>? handleExceptionAction = null)
        {
            try
            {
                await task.ConfigureAwait(configureAwait);
            }
            catch (Exception e)
            {
                HandleException(e, handleExceptionAction);
            }
        }

        /// <summary>
        /// Runs a task without the need to await and invokes optional actions for handling exceptions.
        /// </summary>
        /// <param name="task">The task to run</param>
        /// <param name="configureAwait">true to attempt to marshal the continuation back to the original context captured; otherwise, false. Default true.</param>
        /// <param name="completedAction">The action to call with the returned object of T.</param>
        /// <param name="handleExceptionAction">An action to call if there is an exception.</param>
        public static async void FireAndForget<T>(this Task<T> task, bool configureAwait = true, Action<T>? completedAction = null,  Action<Exception>? handleExceptionAction = null)
        {
            try
            {
                T result = await task.ConfigureAwait(configureAwait);
                completedAction?.Invoke(result);
            }
            catch (Exception e)
            {
                HandleException(e, handleExceptionAction);
            }
        }
        
    }