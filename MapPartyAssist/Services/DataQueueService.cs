using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace MapPartyAssist.Services {
    internal class DataQueueService {

        //coordinates all data sequence-sensitive operations
        internal int Count => DataTaskQueue.Count;
        internal bool Active => DataLock.CurrentCount == 0;
        private ConcurrentQueue<(Task, DateTime)> DataTaskQueue { get; init; } = new();
        private SemaphoreSlim DataLock { get; init; } = new SemaphoreSlim(1, 1);
        internal DateTime LastTaskTime { get; set; }

        internal void Dispose() {
            DataTaskQueue.Clear();
        }

        internal Task<T> QueueDataOperation<T>(Func<T> action) {
            Task<T> t = new(action);
            AddToTaskQueue(t);
            return t;
        }

        internal Task QueueDataOperation(Action action) {
            Task t = new(action);
            AddToTaskQueue(t);
            return t;
        }

        private Task AddToTaskQueue(Task task) {
            DataTaskQueue.Enqueue((task, DateTime.UtcNow));
            RunNextTask();
            return task;
        }

        private Task RunNextTask() {
            return Task.Run(async () => {
                try {
                    await DataLock.WaitAsync();
                    if(DataTaskQueue.TryDequeue(out (Task task, DateTime timestamp) nextTask)) {
                        LastTaskTime = nextTask.timestamp;
                        nextTask.task.Start();
                        await nextTask.task;
                        if(nextTask.task.GetType().IsAssignableTo(typeof(Task<Task>))) {
                            var nestedTask = nextTask.task as Task<Task>;
                            await nestedTask!.Result;
                        }
                    } else {
                        throw new InvalidOperationException("Unable to dequeue task!");
                    }
                } catch(Exception e) {
                    Plugin.Log.Error(e, $"Exception in data task.");
                } finally {
                    DataLock.Release();
                }
            });
        }
    }
}
