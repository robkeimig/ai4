//
using AI;
using System.Text;

var outputBuffer = new IOBuffer(5, IOBufferAccess.ReadWrite, true);

var network = new Network(
    neuronCount: 50_000,
    ioBuffers: new List<IOBuffer>
    {
        new IOBuffer("Hello! Can you respond with 'World' please?", IOBufferAccess.Read, false),
        outputBuffer
    });

Console.WriteLine(network.Statistics());

var top100Networks = new Dictionary<Network, double>();
var expectedOutput = Encoding.UTF8.GetBytes("World");

for (int x = 0; x < 100; x++)
{
    var clone = new Network(network);
    clone.Mutate();
    top100Networks.Add(clone, double.MaxValue);
}

while (true)
{
    Parallel.ForEach(top100Networks, (network) =>
    {
        network.Key.Simulate(10_000_000);
        var outputBuffer = network.Key.IOBuffers.First(x => x.Access == IOBufferAccess.ReadWrite);
        var inputBuffer = network.Key.IOBuffers.First(x => x.Access == IOBufferAccess.Read);
        var output = network.Key.IOBuffers.First(x => x.Access == IOBufferAccess.ReadWrite).Buffer;
        Console.WriteLine(Encoding.UTF8.GetString(output));
    });
}

//    network.Simulate(10_000_000);
//Console.WriteLine(network.TotalSpikes);
//Console.WriteLine(network.SpikedPercentage);