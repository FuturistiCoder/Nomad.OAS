using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using HashiCorp.Nomad;
using Xunit;
using Task = System.Threading.Tasks.Task;
using NomadTask = HashiCorp.Nomad.Task;
using System.Linq;

namespace Nomad.Net.Test
{
    public class JobsApiShould : ApiTestBase
    {
        [Fact]
        public async Task RegisterANewJob()
        {
            await AssertThatNoJobsHaveBeenRun();
            var job = CreateTestJob();

            var registerResponse = await Nomad.RegisterJobAsync(new RegisterJobRequest
            {
                Job = job
            });

            registerResponse.EvalID.Should().NotBeNullOrEmpty();
            var jobs = await Nomad.GetJobsAsync(null);  
            jobs.Count.Should().Be(1);
            jobs.First().ID.Should().Be(job.ID);
            foreach (var j in jobs)
            {
                j.Datacenters.Should().NotBeNullOrEmpty();
            }
        }

        private async Task AssertThatNoJobsHaveBeenRun()
        {
            var result = await Nomad.GetJobsAsync(null);
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task ValidateJob()
        {
            var job = CreateTestJob();
            var result = await Nomad.ValidateJobAsync(job);
            result.ValidationErrors.Should().BeNullOrEmpty();

            job.ID = null;
            result = await Nomad.ValidateJobAsync(job);
            result.ValidationErrors.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task RevertJob()
        {
            var job = CreateTestJob();
            _ = await RegisterTestJobAndPollUntilEvaluationCompletesSuccessfully(job);
            job.Meta.Add("foo", "bar");
            _ = await RegisterTestJobAndPollUntilEvaluationCompletesSuccessfully(job);

            // Fails at incorrect version
            _ = await Nomad.RevertJobAsync(job.ID, new JobRevertRequest
            {
                JobID = job.ID,
                JobVersion = 0,
                EnforcePriorVersion = 10
            });

            var revertedResponse = await Nomad.RevertJobAsync(job.ID, new JobRevertRequest
            {
                JobID = job.ID,
                JobVersion = 0,
                EnforcePriorVersion = 1
            });
        }
    }
}
