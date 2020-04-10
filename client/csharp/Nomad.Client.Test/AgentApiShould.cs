using System;
using Xunit;
using HashiCorp.Nomad;
using FluentAssertions;
using Task = System.Threading.Tasks.Task;
using Xunit.Abstractions;

namespace Nomad.Client.Test
{
    public class AgentApiShould : ApiTestBase
    {
        public AgentApiShould(ITestOutputHelper output)
            : base(output)
        {
            BasePorts.Http = 20000;
            BasePorts.Rpc = 21000;
            BasePorts.Serf = 22000;
        }

        [Fact]
        public async Task ReturnSelfInfo()
        {
            using var agent = NewServer();
            var api = agent.CreateNomadApi();

            var result = await api.GetSelfAsync();
            result.Member.Name.Should().NotBeNullOrEmpty();
            result.Config["Region"].Should().Be("test-region");
            result.Config["Datacenter"].Should().Be("dc1");
        }

    }
}
