using System;
using Xunit;
using HashiCorp.Nomad;
using FluentAssertions;
using Task = System.Threading.Tasks.Task;

namespace Nomad.Net.Test
{
    public class AgentApiShould : ApiTestBase
    {
        [Fact]
        public async Task ReturnSelfInfo()
        {
            var result = await Nomad.GetSelfAsync();
            result.Member.Name.Should().NotBeNullOrEmpty();
            result.Config["region"].Should().Be("test-region");
            result.Config["Datacenter"].Should().Be("dc1");
        }

    }
}
