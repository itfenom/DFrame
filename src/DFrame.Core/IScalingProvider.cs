﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace DFrame.Core
{
    public interface IScalingProvider : IAsyncDisposable
    {
        Task StartWorkerAsync(DFrameOptions options, int nodeCount, CancellationToken cancellationToken);
    }
}