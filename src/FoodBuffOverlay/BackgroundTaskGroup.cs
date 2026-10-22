using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodBuffOverlay
{
    internal sealed class BackgroundTaskGroup : IDisposable
    {
        private readonly object gate = new object();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly List<Task> tasks = new List<Task>();
        private bool stopping;

        public bool TryStart(Func<CancellationToken, Task> operation)
        {
            if (operation == null) throw new ArgumentNullException("operation");
            lock (gate)
            {
                if (stopping) return false;
                var token = cancellation.Token;
                var task = Task.Run(() => operation(token), token);
                tasks.Add(task);
                task.ContinueWith(completed =>
                {
                    if (completed.IsFaulted) { var ignored = completed.Exception; }
                    lock (gate) tasks.Remove(completed);
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                return true;
            }
        }

        public void StopAndWait()
        {
            Task[] pending;
            lock (gate)
            {
                if (stopping && cancellation.IsCancellationRequested) pending = tasks.ToArray();
                else { stopping = true; pending = tasks.ToArray(); }
            }
            try { cancellation.Cancel(); } catch (ObjectDisposedException) { }
            try { Task.WhenAll(pending).GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { }
            catch (AggregateException) { }
            catch (Exception) { }
        }

        public void Dispose()
        {
            StopAndWait();
            cancellation.Dispose();
        }
    }
}
