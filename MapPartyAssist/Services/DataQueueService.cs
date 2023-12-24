using Dalamud.Logging;
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

        internal Task QueueDataOperation<T>(Func<T> action) {
#if DEBUG
            var x = new StackFrame(1, true).GetMethod();
            PluginLog.Verbose($"adding data operation from: {x.Name} {x.DeclaringType}");
#endif
            Task<T> t = new(action);
            return AddToTaskQueue(t);
        }

        internal Task QueueDataOperation(Action action) {
#if DEBUG
            var x = new StackFrame(1, true).GetMethod();
            PluginLog.Verbose($"adding data operation from: {x.Name} {x.DeclaringType}");
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
                } finally {
                    DataLock.Release();
                }
            });
        }
    }
}
