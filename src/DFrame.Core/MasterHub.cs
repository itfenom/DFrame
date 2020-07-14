﻿using DFrame.Collections;
using DFrame.Core.Internal;
using DFrame.Internal;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Client;
using MagicOnion.Server;
using MagicOnion.Server.Hubs;
using MessagePack;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DFrame.Core
{
    public class WorkerContext
    {
        readonly Channel masterChannel;
        public string WorkerId { get; }

        public WorkerContext(Channel masterChannel)
        {
            this.masterChannel = masterChannel;
            this.WorkerId = Guid.NewGuid().ToString();
        }

        public IDistributedQueue<T> CreateDistributedQueue<T>(string key)
        {
            var client = MagicOnionClient.Create<IDistributedQueueService>(
                new DefaultCallInvoker(masterChannel),
                MessagePackSerializer.Typeless.DefaultOptions,
                new IClientFilter[] { new AddHeaderFilter(DistributedQueueService.Key, key) });
            return new DistributedQueue<T>(client);
        }
    }

    public abstract class Worker
    {
        // public Dis Create
        public abstract Task ExecuteAsync(WorkerContext context);

        public virtual Task SetupAsync(WorkerContext context)
        {
            return Task.CompletedTask;
        }

        public virtual Task TeardownAsync(WorkerContext context)
        {
            return Task.CompletedTask;
        }
    }

    public class AddHeaderFilter : IClientFilter
    {
        readonly string key;
        readonly string value;

        public AddHeaderFilter(string key, string value)
        {
            this.key = key;
            this.value = value;
        }

        public ValueTask<ResponseContext> SendAsync(RequestContext context, Func<RequestContext, ValueTask<ResponseContext>> next)
        {
            context.CallOptions.Headers.Add(key, value);
            return next(context);
        }
    }








    public interface IWorkerReceiver
    {
        void CreateCoWorker(int createCount, string typeName);
        void Setup();
        void Execute(int executeCount);
        void Teardown();
        void Shutdown();
    }

    public class WorkerReceiver : IWorkerReceiver
    {
        // readonly ILogger<WorkerReceiver> logger;
        readonly Channel channel;
        readonly TaskCompletionSource<object?> receiveShutdown;
        (WorkerContext context, Worker worker)[] coWorkers = default!;

        public WorkerReceiver(Channel channel)
        {
            // this.logger = logger;
            this.channel = channel;
            this.receiveShutdown = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public IMasterHub Client { get; set; } = default!;

        public Task WaitShutdown => receiveShutdown.Task;

        public void CreateCoWorker(int createCount, string typeName)
        {
            // TODO:Entry?
            var type = Assembly.GetEntryAssembly().GetType(typeName);

            this.coWorkers = new (WorkerContext, Worker)[createCount];
            for (int i = 0; i < coWorkers.Length; i++)
            {
                // TODO: ExpressionTree Lambda
                // register to DI.
                //var coWorker = typeof(IServiceLocator).GetMethod("GetService").MakeGenericMethod(type)
                //    .Invoke(this.Context.ServiceLocator, null);
                var coWorker = Activator.CreateInstance(type);
                coWorkers[i] = (new WorkerContext(channel), (Worker)coWorker);
            }

            Client.CreateCoWorkerCompleteAsync().Forget();
        }

        public async void Setup()
        {
            await Task.WhenAll(coWorkers.Select(x => x.worker.SetupAsync(x.context)));
            await Client.SetupCompleteAsync();
        }

        public async void Execute(int executeCount)
        {
            var result = await Task.WhenAll(coWorkers.Select(async x =>
            {
                var list = new List<ExecuteResult>(executeCount);
                for (int i = 0; i < executeCount; i++)
                {
                    string? errorMsg = null;
                    var sw = ValueStopwatch.StartNew();
                    try
                    {
                        await x.worker.ExecuteAsync(x.context);
                    }
                    catch (Exception ex)
                    {
                        errorMsg = ex.ToString();
                    }

                    var executeResult = new ExecuteResult(x.context.WorkerId, sw.Elapsed, i, (errorMsg != null), errorMsg);
                    list.Add(executeResult);
                }
                return list;
            }));

            await Client.ExecuteCompleteAsync(result.SelectMany(xs => xs).ToArray());
        }

        public async void Teardown()
        {
            await Task.WhenAll(coWorkers.Select(x => x.worker.TeardownAsync(x.context)));
            await Client.TeardownCompleteAsync();
        }

        public void Shutdown()
        {
            receiveShutdown.TrySetResult(null);
        }
    }

    [MessagePackObject]
    public class ExecuteResult
    {
        [Key(0)]
        public string WorkerId { get; }
        [Key(1)]
        public TimeSpan Elapsed { get; }
        [Key(2)]
        public int ExecutionNo { get; }
        [Key(3)]
        public bool HasError { get; }
        [Key(4)]
        public string? ErrorMessage { get; }

        public ExecuteResult(string workerId, TimeSpan elapsed, int executionNo, bool hasError, string? errorMessage)
        {
            WorkerId = workerId;
            Elapsed = elapsed;
            ExecutionNo = executionNo;
            HasError = hasError;
            ErrorMessage = errorMessage;
        }
    }


    public interface IMasterHub : IStreamingHub<IMasterHub, IWorkerReceiver>
    {
        Task ConnectCompleteAsync();
        Task CreateCoWorkerCompleteAsync();
        Task SetupCompleteAsync();
        Task ExecuteCompleteAsync(ExecuteResult[] result);
        Task TeardownCompleteAsync();
    }

    public sealed class MasterHub : StreamingHubBase<IMasterHub, IWorkerReceiver>, IMasterHub
    {
        readonly Reporter reporter;

        public MasterHub(Reporter reporter)
        {
            this.reporter = reporter;
        }

        protected override async ValueTask OnConnecting()
        {
            var group = await Group.AddAsync("global-masterhub-group");
            var broadcaster = group.CreateBroadcaster<IWorkerReceiver>();
            reporter.Broadcaster = broadcaster;
        }

        public Task ConnectCompleteAsync()
        {
            reporter.OnConnected.IncrementCount();
            return Task.CompletedTask;
        }

        public Task CreateCoWorkerCompleteAsync()
        {
            reporter.OnCreateCoWorker.IncrementCount();
            return Task.CompletedTask;
        }

        public Task SetupCompleteAsync()
        {
            reporter.OnSetup.IncrementCount();
            return Task.CompletedTask;
        }

        public Task ExecuteCompleteAsync(ExecuteResult[] result)
        {
            reporter.AddExecuteResult(result);
            reporter.OnExecute.IncrementCount();
            return Task.CompletedTask;
        }

        public Task TeardownCompleteAsync()
        {
            reporter.OnTeardown.IncrementCount();
            return Task.CompletedTask;
        }
    }
}