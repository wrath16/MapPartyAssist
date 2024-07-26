using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MapPartyAssist.Services {
    internal class DataQueueService {

        //coordinates all data sequence-sensitive operations
        private ConcurrentQueue<(Task, DateTime)> DataTaskQueue { get; init; } = new();
        private SemaphoreSlim DataLock { get; init; } = new SemaphoreSlim(1, 1);
        private Plugin _plugin;

        internal DateTime LastTaskTime { get; set; }

        internal DataQueueService(Plugin plugin) {
            _plugin = plugin;
        }

        internal void Dispose() {
            DataTaskQueue.Clear();
        }

        internal Task<T> QueueDataOperation<T>(Func<T> action) {
#if DEBUG
            var x = new StackFrame(1, true).GetMethod();
            _plugin.Log.Verbose($"adding data operation from: {x.Name} {x.DeclaringType} tasks queued: {DataTaskQueue.Count + 1}");
#endif
            Task<T> t = new(action);
            AddToTaskQueue(t);
            return t;
        }

        internal Task QueueDataOperation(Action action) {
#if DEBUG
            var x = new StackFrame(1, true).GetMethod();
            _plugin.Log.Verbose($"adding data operation from: {x.Name} {x.DeclaringType} tasks queued: {DataTaskQueue.Count + 1}");
#endif
            Task t = new(action);
            AddToTaskQueue(t);
            return t;
        }

        private Task AddToTaskQueue(Task task) {
            DataTaskQueue.Enqueue((task, DateTime.Now));
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
                    _plugin.Log.Error(e, $"Exception in data task.");
                    //_plugin.Log.Error(e.StackTrace ?? "");
                } finally {
                    DataLock.Release();
                }
            });
        }
    }
}
