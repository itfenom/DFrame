﻿using Cysharp.Text;
using DFrame.Hosting.Data;
using DFrame.Hosting.Infrastructure;
using DFrame.Hosting.Models;
using Microsoft.Extensions.DependencyInjection;
using ZLogger;

namespace DFrame.Hosting
{
    public static class DFrameServerServiceCollectionExtensions
    {
        public static IServiceCollection AddDFrameHosting(this IServiceCollection services)
        {
            services.AddSingleton<ExecuteService>();
            services.AddSingleton<ISummaryService, SummaryService>();
            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddSingleton<IStatisticsService<AbStatistic>, AbStatisticsService>();
            services.AddSingleton<IStatisticsService<Statistic>, StatisticsMockService>();
            services.AddSingleton<IWorkersService, WorkersService>();
            services.AddSingleton<LogProcessorOptions>(new LogProcessorOptions()
            {
                LoggerOptions = new ZLoggerOptions
                {
                    ExceptionFormatter = (writer, ex) => ZString.Utf8Format(writer, "{0} {1}", ex.Message, ex.StackTrace),
                },
            });
            return services;
        }
    }
}