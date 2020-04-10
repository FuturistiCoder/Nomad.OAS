using FluentAssertions;
using HashiCorp.Nomad;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace Nomad.Client.Test
{
    public class EvaluationsApiShould : ApiTestBase
    {
        public EvaluationsApiShould(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task GetEvaluationsFromNewestToOldest()
        {
            using var agent = NewServer();
            var api = agent.CreateNomadApi();

            var initialResponse = await api.GetEvaluationsAsync(null);
            initialResponse.Should().BeEmpty();

            var jobRegisterResponse = await api.RegisterJobAsync(new HashiCorp.Nomad.RegisterJobRequest
            {
                Job = CreateTestJob()
            });

            var updatedResponse = await api.GetEvaluationsAsync(null);
            updatedResponse.Should().NotBeEmpty();
            foreach (var evaluation in updatedResponse)
            {
                evaluation.CreateTime.Should().NotBeNull();
                evaluation.ModifyTime.Should().NotBeNull();
            }

            // if the eval fails fast there can be more than 1
            // order is unknown
            updatedResponse.Any(eval => eval.ID == jobRegisterResponse.EvalID).Should().BeTrue();
        }

        [Fact]
        public async Task GetEvaluationsWithPrefix()
        {
            using var agent = NewServer();
            var api = agent.CreateNomadApi();

            var initialResponse = await api.GetEvaluationsAsync("abcdef");
            initialResponse.Should().BeEmpty();

            var jobRegisterResponse = await api.RegisterJobAsync(new HashiCorp.Nomad.RegisterJobRequest
            {
                Job = CreateTestJob()
            });

            var updatedResponse = await api.GetEvaluationsAsync(jobRegisterResponse.EvalID.Substring(0, 4));

            // if the eval fails fast there can be more than 1
            // order is unknown
            updatedResponse.Any(eval => eval.ID == jobRegisterResponse.EvalID).Should().BeTrue();
        }

        [Fact]
        public async Task GetEvaluationInfo()
        {
            using var agent = NewServer();
            var api = agent.CreateNomadApi();

            // not found
            Func<Task<Evaluation>> func = () => api.GetEvaluationAsync("8E231CF4-CA48-43FF-B694-5801E69E22FA");
            func.Should().Throw<ApiException>();

            var jobRegisterResponse = await api.RegisterJobAsync(new HashiCorp.Nomad.RegisterJobRequest
            {
                Job = CreateTestJob()
            });

            var response = await api.GetEvaluationAsync(jobRegisterResponse.EvalID);
            response.ID.Should().Be(jobRegisterResponse.EvalID);
        }

        [Fact]
        public async Task GetAllocationsForAnEvaluation()
        {
            using var agent = NewClientServer();
            var api = agent.CreateNomadApi();

            var initialResponse = await api.GetAllocationsForEvaluationAsync("8E231CF4-CA48-43FF-B694-5801E69E22FA");
            initialResponse.Should().BeEmpty();

            var jobRegisterResponse = await api.RegisterJobAsync(new RegisterJobRequest
            {
                Job = CreateTestJob()
            });

            var response = await Policy
                .HandleResult<ICollection<AllocationListStub>>(result => !result.Any())
                .WaitAndRetryAsync(20, i => TimeSpan.FromSeconds(0.3))
                .ExecuteAsync(() => api.GetAllocationsForEvaluationAsync(jobRegisterResponse.EvalID));

            response.First().EvalID.Should().Be(jobRegisterResponse.EvalID);
        }
    }
}