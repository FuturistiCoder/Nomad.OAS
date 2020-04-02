using HashiCorp.Nomad;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Polly;

using NomadTask = HashiCorp.Nomad.Task;

namespace Nomad.Net.Test
{
    public class ApiTestBase
    {
        protected NomadApi Nomad { get; }

        public ApiTestBase()
        {
            Nomad = new NomadApi(new NomadApiConfiguration
            {
                BaseUrl = "http://127.0.0.1:4646/v1"
            });
        }

        public Job CreateTestJob()
        {
            var task = new NomadTask
            {
                Name = "task1",
                Driver = "mock_driver",
                Config = { { "run_for", "20s" } },
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
                Tasks = { task },
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
                Datacenters = { "dc1" },
                TaskGroups = { taskGroup }
            };
        }

        public async Task<Evaluation> RegisterTestJobAndPollUntilEvaluationCompletesSuccessfully(Job job)
        {
            var result = await Nomad.RegisterJobAsync(new RegisterJobRequest { 
                Job = job
            });
            result.EvalID.Should().NotBe("0");

            var evalutation = await Policy
                .HandleResult<Evaluation>(eval => eval.Status == "complete")
                .WaitAndRetryAsync(20, i => TimeSpan.FromSeconds(5))
                .ExecuteAsync(async () =>
                {
                    return await Nomad.GetEvaluationAsync(result.EvalID);
                });

            evalutation.BlockedEval.Should().NotBeNullOrEmpty();
            return evalutation;
        }
    }
}
