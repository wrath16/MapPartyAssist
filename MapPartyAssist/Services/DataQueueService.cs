using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MapPartyAssist.Services {
    internal class DataQueueService {

        //coordinates all data sequence-sensitive operations
        private ConcurrentQueue<Task> DataTaskQueue { get; init; } = new();
        private SemaphoreSlim DataLock { get; init; } = new SemaphoreSlim(1, 1);
        private Plugin _plugin;

        internal DataQueueService(Plugin plugin) {
            _plugin = plugin;
        }

        internal void Dispose() {
            DataTaskQueue.Clear();
        }

        internal Task QueueDataOperation<T>(Func<T> action) {
#if DEBUG
            var x = new StackFrame(1, true).GetMethod();
            //_plugin.Log.Verbose($"adding data operation from: {x.Name} {x.DeclaringType} tasks queued: {DataTaskQueue.Count + 1}");
#endif
            Task<T> t = new(action);
            return AddToTaskQueue(t);
        }

        internal Task QueueDataOperation(Action action) {
#if DEBUG
            //var x = new StackFrame(1, true).GetMethod();
            //_plugin.Log.Debug($"adding data operation from: {x.Name} {x.DeclaringType} tasks queued: {DataTaskQueue.Count + 1}");
#endif
            Task t = new(action);
            return AddToTaskQueue(t);
        }

        private Task AddToTaskQueue(Task task) {
            DataTaskQueue.Enqueue(task);
            RunNextTask();
            return task;
        }

        private Task RunNextTask() {
            return Task.Run(async () => {
                try {
                    await DataLock.WaitAsync();
                    if(DataTaskQueue.TryDequeue(out Task? nextTask)) {
                        nextTask.Start();
                        await nextTask;
                    } else {
                        throw new Exception("Unable to dequeue task!");
                        //Log.Warning($"Unable to dequeue next task. Tasks remaining: {DataTaskQueue.Count}");
                    }
                } catch(Exception e) {
                    _plugin.Log.Error($"Exception in data task: {e.Message}");
                    _plugin.Log.Error(e.StackTrace ?? "");
                } finally {
                    DataLock.Release();
                }
            });
        }
    }
}
