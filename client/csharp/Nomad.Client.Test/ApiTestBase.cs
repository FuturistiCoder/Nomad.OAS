using HashiCorp.Nomad;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Polly;

using NomadTask = HashiCorp.Nomad.Task;
using System.Diagnostics;
using Xunit.Abstractions;
using System.IO;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Nomad.Client.Test
{
    public class ApiTestBase
    {
        protected readonly ITestOutputHelper _output;
        protected string NONSENSE_GUID = "12345678-abcd-efab-cdef-123456789abc";

        protected Ports BasePorts { get; set; } = new Ports
        {
            Http = 20000,
            Rpc = 21000,
            Serf = 22000
        };

        public ApiTestBase(ITestOutputHelper output)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
        }

        public Job CreateTestJob()
        {
            var task = new NomadTask
            {
                Name = "task1",
                Driver = "mock_driver",
                Config = new Dictionary<string, object>{ { "run_for", "20s" } },
                Resources = new Resources
                {
                    Cpu = 100,
                    MemoryMb = 256
                },
                LogConfig = new LogConfig
                {
                    MaxFiles = 1,
                    MaxFileSizeMb = 2
                }
            };

            TaskGroup taskGroup = new TaskGroup
            {
                Name = "group1",
                Count = 1,
                Tasks = new[]{ task },
                EphemeralDisk = new EphemeralDisk
                {
                    SizeMb = 25
                }
            };

            return new Job
            {
                ID = "job1",
                Name = "redis",
                Region = "test-region",
                Type = "batch",
                Priority = 1,
                Datacenters = new[]{ "dc1" },
                TaskGroups = new[]{ taskGroup },
                Meta = new Dictionary<string, string>()
            };
        }

        public Task<Evaluation> PollForEvaluationCompletion(NomadApi api, string evalId)
             => Policy
                .HandleResult<Evaluation>(eval => eval.Status != "complete")
                .WaitAndRetryAsync(100, i => TimeSpan.FromSeconds(0.1))
                .ExecuteAsync(async () =>
                {
                    return await api.GetEvaluationAsync(evalId);
                });

        public async Task<Evaluation> RegisterTestJobAndPollUntilEvaluationCompletes(NomadApi api, Job job)
        {
            var result = await api.RegisterJobAsync(new RegisterJobRequest { 
                Job = job
            });
            result.EvalID.Should().NotBe("0");

            var evaluation = await PollForEvaluationCompletion(api, result.EvalID);
            evaluation.ID.Should().Be(result.EvalID);
            evaluation.NextEval.Should().BeNullOrEmpty();

            return evaluation;
        }

        public async Task<Evaluation> RegisterTestJobAndPollUntilEvaluationCompletesSuccessfully(NomadApi api, Job job)
        {
            var evaluation = await RegisterTestJobAndPollUntilEvaluationCompletes(api, job);
            evaluation.BlockedEval.Should().BeNullOrEmpty();
            return evaluation;
        }

        #region NomadAgentProcess Creation Helper

        internal NomadAgentProcess NewServer(Action<NomadAgentConfiguration> configure = null)
        {
            var type = GetType();
            var config = new NomadAgentConfiguration
            {
                Region = "test-region",
                Name = $"{type.Name}_{NomadAgentConfiguration.Count}",
                DataDir = $"{Path.GetTempPath()}nomaddir_{type.Name}_{NomadAgentConfiguration.Count}",
                Ports = BasePorts.Add(NomadAgentConfiguration.Count),
            };
            configure?.Invoke(config);
            var process = new NomadAgentProcess(config, _output);

            process.Start();
            return process;
        }

        internal NomadAgentProcess NewClientServer()
        {
            var type = GetType();
            var process = new NomadAgentProcess(new NomadAgentConfiguration
            {
                Region = "test-region",
                Name = $"{type.Name}_{NomadAgentConfiguration.Count}",
                DataDir = $"{Path.GetTempPath()}nomaddir_{type.Name}_{NomadAgentConfiguration.Count}",
                Ports = BasePorts.Add(NomadAgentConfiguration.Count),
                Client = new Client
                {
                    Enabled = true,
                    Options = new Dictionary<string, string> { { "driver.raw_exec.enable", "1" } }
                },
                LogLevel = "Trace"
            }, _output);

            process.Start();
            return process;
        }

        #endregion
    }

}
