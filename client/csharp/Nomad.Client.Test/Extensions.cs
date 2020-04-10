namespace Nomad.Client.Test
{
    internal static class Extensions
    {
        public static Ports Add(this Ports ports, int num) =>
            new Ports
            {
                Http = ports.Http + num,
                Rpc = ports.Rpc + num,
                Serf = ports.Serf + num
            };
    }
}