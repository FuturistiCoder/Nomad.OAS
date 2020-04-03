using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Nomad.Net.Test
{
    internal class NomadAgentConfiguration
    {
        public static int Count = 0;
        public const string DefaultAddress = "127.0.0.1";

        public NomadAgentConfiguration()
        {
            Interlocked.Increment(ref Count);
        }

        [JsonProperty("region")]
        public string Region { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("data_dir")]
        public string DataDir { get; set; }

        [JsonProperty("log_level")]
        public string LogLevel { get; set; }

        [JsonProperty("bind_addr")]
        public string BindAddr { get; set; } = DefaultAddress;

        [JsonProperty("advertise")]
        public AdvertiseAddrs Advertise { get; set; } = new AdvertiseAddrs();

        [JsonProperty("ports")]
        public Ports Ports { get; set; } = new Ports();

        [JsonProperty("server")]
        public Server Server { get; set; } = new Server();

        [JsonProperty("client")]
        public Client Client { get; set; } = new Client();

        [JsonProperty("acl")]
        public Acl Acl { get; set; } = new Acl();

        [JsonProperty("disable_update_check")]
        public bool DisableCheckpoint { get; set; }

        [JsonProperty("consul")]
        public Consul Consul { get; set; } = new Consul();

        [JsonProperty("tls")]
        public Tls Tls { get; set; } = new Tls();
    }

    public class AdvertiseAddrs
    {
        [JsonProperty("http")]
        public string Http { get; set; } = NomadAgentConfiguration.DefaultAddress;

        [JsonProperty("rpc")]
        public string Rpc { get; set; } = NomadAgentConfiguration.DefaultAddress;

        [JsonProperty("serf")]
        public string Serf { get; set; } = NomadAgentConfiguration.DefaultAddress;
    }

    public class Ports
    {
        [JsonProperty("http")]
        public int Http { get; set; }

        [JsonProperty("rpc")]
        public int Rpc { get; set; }

        [JsonProperty("serf")]
        public int Serf { get; set; }
    }

    public class Server
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty("raft_protocol")]
        public int RaftProtocol { get; set; } = 2;

        [JsonProperty("bootstrap_expect")]
        public int BootstrapExpect { get; set; } = 1;

        [JsonProperty("start_join")]
        public List<string> StartJoin { get; set; }
    }

    public class Client
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("options")]
        public Dictionary<string, string> Options { get; set; }
    }

    public class Acl
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; }
    }

    public class Consul
    {
        [JsonProperty("auto_advertise")]
        public bool AutoAdvertise { get; set; }

        [JsonProperty("server_auto_join")]
        public bool ServerAutoJoin { get; set; }

        [JsonProperty("client_auto_join")]
        public bool ClientAutoJoin { get; set; }
    }

    public class Tls
    {
        [JsonProperty("http")]
        public bool Http { get; set; }

        [JsonProperty("ca_file")]
        public string CaFile { get; set; }

        [JsonProperty("cert_file")]
        public string CertFile { get; set; }

        [JsonProperty("key_file")]
        public string KeyFile { get; set; }
    }
}