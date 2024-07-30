//
using AI;

var network = new Network(
    neuronCount: 1_000_000,
    ioBuffers: new List<IOBuffer>
    {
        new IOBuffer("hello world!", IOBufferAccess.Read, false)
        //...
    });

Console.WriteLine(network.Statistics());

network.Simulate(10_000_000);
Console.WriteLine(network.TotalSpikes);
Console.WriteLine(network.SpikedPercentage);